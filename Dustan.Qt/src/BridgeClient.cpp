#include "BridgeClient.h"
#include "JsonListModel.h"

#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QQmlEngine>

namespace {
BridgeClient *s_instance = nullptr;
QQmlEngine *s_engine = nullptr;
}

BridgeClient::BridgeClient(QString executable, QObject *parent)
    : QObject(parent), m_executable(std::move(executable))
{
}

BridgeClient::~BridgeClient()
{
    m_stopping = true;
    if (m_process && m_process->state() != QProcess::NotRunning) {
        // Closing stdin asks the bridge to finish in-flight work and release LibVLC cleanly.
        m_process->closeWriteChannel();
        if (!m_process->waitForFinished(5000)) {
            m_process->kill();
            m_process->waitForFinished(2000);
        }
    }
}

BridgeClient *BridgeClient::create(QQmlEngine *, QJSEngine *)
{
    QJSEngine::setObjectOwnership(s_instance, QJSEngine::CppOwnership);
    return s_instance;
}

void BridgeClient::setInstance(BridgeClient *instance, QQmlEngine *engine)
{
    s_instance = instance;
    s_engine = engine;
}

void BridgeClient::start()
{
    if (m_process)
        m_process->deleteLater();
    m_ready = false;
    m_buffer.clear();
    m_process = new QProcess(this);
    // stdout carries the protocol; bridge diagnostics (LibVLC, ffprobe) go straight to our stderr.
    m_process->setProcessChannelMode(QProcess::ForwardedErrorChannel);
    m_process->setReadChannel(QProcess::StandardOutput);

    connect(m_process, &QProcess::readyReadStandardOutput, this, &BridgeClient::readOutput);
    connect(m_process, &QProcess::started, this, [this] {
        for (const auto &line : std::as_const(m_queue))
            m_process->write(line);
        m_queue.clear();
    });
    connect(m_process, &QProcess::errorOccurred, this, [this](QProcess::ProcessError error) {
        if (error == QProcess::FailedToStart)
            setFatal(tr("Dustan could not start its library service (%1). Reinstall Dustan or set DUSTAN_BRIDGE to Dustan.Bridge.")
                         .arg(m_executable));
    });
    connect(m_process, &QProcess::finished, this, [this](int exitCode, QProcess::ExitStatus status) {
        if (m_stopping)
            return;
        m_ready = false;
        emit readyChanged();
        auto pending = std::exchange(m_pending, {});
        for (auto &p : pending)
            finishPending(p, {}, tr("The library service stopped."));
        if (m_fatalError.isEmpty())
            setFatal(status == QProcess::CrashExit
                         ? tr("The library service stopped unexpectedly. Restart it to continue; your libraries are unchanged.")
                         : tr("The library service exited (code %1).").arg(exitCode));
    });

    if (!QFileInfo::exists(m_executable)) {
        setFatal(tr("The library service is missing: %1").arg(m_executable));
        return;
    }
    m_process->start(m_executable, {});
}

void BridgeClient::restart()
{
    m_fatalError.clear();
    emit fatalErrorChanged();
    if (m_process && m_process->state() != QProcess::NotRunning) {
        m_stopping = true;
        m_process->closeWriteChannel();
        m_process->waitForFinished(5000);
        m_stopping = false;
    }
    start();
}

void BridgeClient::call(const QString &method, const QVariantMap &params, const QJSValue &callback)
{
    send(method, params, {callback, nullptr, {}});
}

void BridgeClient::load(JsonListModel *model, const QString &method, const QVariantMap &params,
                        const QString &arrayKey, const QJSValue &callback)
{
    if (model)
        model->setLoading(true);
    send(method, params, {callback, model, arrayKey});
}

void BridgeClient::send(const QString &method, const QVariantMap &params, Pending pending)
{
    const qint64 id = m_nextId++;
    QJsonObject message{{"id", id}, {"method", method}, {"params", QJsonObject::fromVariantMap(params)}};
    QByteArray line = QJsonDocument(message).toJson(QJsonDocument::Compact);
    line.append('\n');
    m_pending.insert(id, std::move(pending));
    if (m_process && m_process->state() == QProcess::Running)
        m_process->write(line);
    else
        m_queue.append(line);
}

void BridgeClient::readOutput()
{
    m_buffer.append(m_process->readAllStandardOutput());
    qsizetype start = 0;
    for (qsizetype newline; (newline = m_buffer.indexOf('\n', start)) >= 0; start = newline + 1)
        handleLine(m_buffer.mid(start, newline - start));
    m_buffer.remove(0, start);
}

void BridgeClient::handleLine(const QByteArray &line)
{
    QJsonParseError parseError;
    const auto document = QJsonDocument::fromJson(line, &parseError);
    if (parseError.error != QJsonParseError::NoError || !document.isObject()) {
        qWarning("Ignoring malformed bridge message: %s", qPrintable(parseError.errorString()));
        return;
    }
    const auto message = document.object();
    if (message.contains("event")) {
        const auto name = message.value("event").toString();
        const auto data = message.value("data");
        if (name == QLatin1String("ready")) {
            m_ready = true;
            emit readyChanged();
        } else if (name == QLatin1String("fatal")) {
            setFatal(data.toObject().value("message").toString());
        }
        emit event(name, data.toVariant());
        return;
    }
    const auto id = message.value("id").toInteger(-1);
    auto it = m_pending.find(id);
    if (it == m_pending.end())
        return;
    Pending pending = std::move(*it);
    m_pending.erase(it);
    finishPending(pending, message.value("result"), message.value("error").toString());
}

void BridgeClient::finishPending(Pending &pending, const QJsonValue &result, const QString &error)
{
    if (pending.model) {
        const auto array = pending.arrayKey.isEmpty() ? result.toArray() : result.toObject().value(pending.arrayKey).toArray();
        if (error.isEmpty())
            pending.model->setJsonArray(array);
        pending.model->setLoading(false);
        pending.model->setError(error);
    }
    if (!pending.callback.isCallable() || !s_engine)
        return;
    QVariant value;
    if (pending.model && pending.arrayKey.isEmpty())
        value = QVariant(); // The model already holds the list; skip a large JavaScript copy.
    else if (pending.model) {
        auto object = result.toObject();
        object.remove(pending.arrayKey);
        value = object.toVariantMap();
    } else {
        value = result.toVariant();
    }
    const auto callResult = pending.callback.call({s_engine->toScriptValue(value), QJSValue(error)});
    if (callResult.isError())
        qWarning("Bridge callback failed: %s", qPrintable(callResult.toString()));
}

void BridgeClient::setFatal(const QString &message)
{
    if (m_fatalError == message)
        return;
    m_fatalError = message;
    emit fatalErrorChanged();
}
