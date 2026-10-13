#pragma once

#include "JsonListModel.h"

#include <QHash>
#include <QJSValue>
#include <QObject>
#include <QPointer>
#include <QProcess>
#include <QVariant>
#include <QtQml/qqmlregistration.h>

class QQmlEngine;
class QJSEngine;


/// Talks to Dustan.Bridge, the .NET process that owns the libraries, schedules and music playback.
/// Each line on its stdin/stdout is one JSON message (see Dustan.Bridge/BridgeHost.cs).
class BridgeClient : public QObject
{
    Q_OBJECT
    QML_NAMED_ELEMENT(Bridge)
    QML_SINGLETON
    Q_PROPERTY(bool ready READ ready NOTIFY readyChanged)
    Q_PROPERTY(QString fatalError READ fatalError NOTIFY fatalErrorChanged)
    Q_PROPERTY(QString executable READ executable CONSTANT)

public:
    explicit BridgeClient(QString executable, QObject *parent = nullptr);
    ~BridgeClient() override;

    static BridgeClient *create(QQmlEngine *, QJSEngine *);
    static void setInstance(BridgeClient *instance, QQmlEngine *engine);

    bool ready() const { return m_ready; }
    QString fatalError() const { return m_fatalError; }
    QString executable() const { return m_executable; }

    void start();

    /// Sends a request; `callback(result, error)` runs on the GUI thread when the reply arrives.
    Q_INVOKABLE void call(const QString &method, const QVariantMap &params = {}, const QJSValue &callback = {});
    /// Like call(), but puts the array result (or result[arrayKey]) straight into a list model,
    /// avoiding a JavaScript copy of large lists. The callback receives the full result.
    Q_INVOKABLE void load(JsonListModel *model, const QString &method, const QVariantMap &params = {},
                          const QString &arrayKey = {}, const QJSValue &callback = {});
    Q_INVOKABLE void restart();

signals:
    void readyChanged();
    void fatalErrorChanged();
    void event(const QString &name, const QVariant &data);

private:
    struct Pending {
        QJSValue callback;
        QPointer<JsonListModel> model;
        QString arrayKey;
    };

    void send(const QString &method, const QVariantMap &params, Pending pending);
    void readOutput();
    void handleLine(const QByteArray &line);
    void finishPending(Pending &pending, const QJsonValue &result, const QString &error);
    void setFatal(const QString &message);

    QString m_executable;
    QProcess *m_process = nullptr;
    QHash<qint64, Pending> m_pending;
    QList<QByteArray> m_queue;
    qint64 m_nextId = 1;
    bool m_ready = false;
    bool m_stopping = false;
    QString m_fatalError;
    QByteArray m_buffer;
};
