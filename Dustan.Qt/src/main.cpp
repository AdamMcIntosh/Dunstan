#include "BridgeClient.h"
#include "Platform.h"
#include "VideoThumbnailProvider.h"

#include <QCommandLineParser>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QFont>
#include <QFontDatabase>
#include <QGuiApplication>
#include <QIcon>
#include <QQmlApplicationEngine>
#include <QQuickStyle>
#include <QQuickWindow>

#include <clocale>

namespace {

QString findBridge(const QString &explicitPath)
{
    if (!explicitPath.isEmpty())
        return explicitPath;
    if (const auto fromEnv = qEnvironmentVariable("DUSTAN_BRIDGE"); !fromEnv.isEmpty())
        return fromEnv;
#ifdef Q_OS_WIN
    const QString name = QStringLiteral("Dustan.Bridge.exe");
#else
    const QString name = QStringLiteral("Dustan.Bridge");
#endif
    const QDir appDir(QCoreApplication::applicationDirPath());
    const QStringList candidates{
        appDir.filePath(QStringLiteral("bridge/") + name),                     // Linux/Windows layout
        appDir.filePath(QStringLiteral("../Resources/bridge/") + name),        // macOS bundle
        QStringLiteral(DUSTAN_SOURCE_DIR "/../Dustan.Bridge/bin/Release/net8.0/") + name, // development tree
        QStringLiteral(DUSTAN_SOURCE_DIR "/../Dustan.Bridge/bin/Debug/net8.0/") + name,
    };
    for (const auto &candidate : candidates)
        if (QFileInfo::exists(candidate))
            return QDir::cleanPath(candidate);
    return candidates.first();
}

} // namespace

int main(int argc, char *argv[])
{
    // libmpv renders through OpenGL; Qt Quick must use the same API on every platform.
    QQuickWindow::setGraphicsApi(QSGRendererInterface::OpenGL);
    QGuiApplication::setHighDpiScaleFactorRoundingPolicy(Qt::HighDpiScaleFactorRoundingPolicy::PassThrough);
    QGuiApplication app(argc, argv);
    QGuiApplication::setApplicationName(QStringLiteral("Dustan"));
    QGuiApplication::setOrganizationName(QStringLiteral("Dustan"));
    QGuiApplication::setApplicationVersion(QStringLiteral(DUSTAN_VERSION));
    QGuiApplication::setDesktopFileName(QStringLiteral("dustan"));
    QGuiApplication::setWindowIcon(QIcon(QStringLiteral(":/qt/qml/Dustan/packaging/dustan.svg")));
    // mpv requires the C numeric locale (decimal points in option strings).
    std::setlocale(LC_NUMERIC, "C");

    QCommandLineParser parser;
    parser.setApplicationDescription(QStringLiteral("Dustan — NAS music, video and TV channels"));
    parser.addHelpOption();
    parser.addVersionOption();
    QCommandLineOption pageOption(QStringLiteral("page"), QStringLiteral("Open on a page: music, videos, tv, nowplaying, settings."), QStringLiteral("page"));
    QCommandLineOption fullOption(QStringLiteral("fullscreen"), QStringLiteral("Start full screen (TV setups)."));
    QCommandLineOption sizeOption(QStringLiteral("size"), QStringLiteral("Interface size: compact, standard, large or tv."), QStringLiteral("size"));
    QCommandLineOption bridgeOption(QStringLiteral("bridge"), QStringLiteral("Path to Dustan.Bridge."), QStringLiteral("path"));
    QCommandLineOption tourOption(QStringLiteral("tour"), QStringLiteral("Development: visit every screen and save screenshots to a folder, then quit."), QStringLiteral("folder"));
    QCommandLineOption verifyOption(QStringLiteral("verify-playback"),
        QStringLiteral("Development: play each video listed in a text file (one path per line), test seeking, audio and subtitle switching, log results and save frames to the --tour folder, then quit."),
        QStringLiteral("list"));
    parser.addOptions({pageOption, fullOption, sizeOption, bridgeOption, tourOption, verifyOption});
    parser.process(app);
    Platform::setStartOptions(parser.value(pageOption), parser.value(tourOption), parser.isSet(fullOption), parser.value(sizeOption));
    if (parser.isSet(verifyOption)) {
        QFile list(parser.value(verifyOption));
        if (list.open(QIODevice::ReadOnly | QIODevice::Text)) {
            QStringList files;
            for (const auto &line : QString::fromUtf8(list.readAll()).split(QLatin1Char('\n'), Qt::SkipEmptyParts))
                if (!line.trimmed().isEmpty() && !line.startsWith(QLatin1Char('#')))
                    files << line.trimmed();
            Platform::setVerifyFiles(files);
        }
    }

    QQuickStyle::setStyle(QStringLiteral("Basic"));
    for (const auto *weight : {"Regular", "Medium", "SemiBold", "Bold"})
        QFontDatabase::addApplicationFont(QStringLiteral(":/qt/qml/Dustan/fonts/Inter-%1.ttf").arg(QLatin1String(weight)));
    QFont font(QStringLiteral("Inter"));
    font.setPixelSize(14);
    font.setHintingPreference(QFont::PreferNoHinting);
    QGuiApplication::setFont(font);

    BridgeClient bridge(findBridge(parser.value(bridgeOption)));
    QQmlApplicationEngine engine;
    BridgeClient::setInstance(&bridge, &engine);
    engine.addImageProvider(QStringLiteral("videothumb"), new VideoThumbnailProvider);
    QObject::connect(&engine, &QQmlApplicationEngine::objectCreationFailed, &app, [] { QCoreApplication::exit(1); }, Qt::QueuedConnection);
    bridge.start();
    engine.loadFromModule("Dustan", "Main");
    return QGuiApplication::exec();
}
