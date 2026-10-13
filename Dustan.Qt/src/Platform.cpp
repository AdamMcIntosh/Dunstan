#include "Platform.h"
#include "MpvPlayer.h"
#include "VideoThumbnailProvider.h"

#include <QDir>
#include <QFileInfo>
#include <QQuickWindow>
#include <QSysInfo>

namespace {
QString s_startPage;
QString s_tourDirectory;
bool s_fullScreen = false;
QString s_sizeOverride;
QStringList s_verifyFiles;
}

void Platform::setVerifyFiles(const QStringList &files) { s_verifyFiles = files; }
QStringList Platform::verifyFiles() const { return s_verifyFiles; }

void Platform::setStartOptions(const QString &page, const QString &tourDirectory, bool fullScreen, const QString &sizeOverride)
{
    s_startPage = page;
    s_tourDirectory = tourDirectory;
    s_fullScreen = fullScreen;
    s_sizeOverride = sizeOverride;
}

QString Platform::appVersion() const { return QStringLiteral(DUSTAN_VERSION); }
QString Platform::qtVersion() const { return QString::fromLatin1(qVersion()); }
QString Platform::mpvVersion() const { return MpvPlayer::mpvVersion(); }
QString Platform::os() const { return QSysInfo::prettyProductName(); }
QString Platform::startPage() const { return s_startPage; }
QString Platform::tourDirectory() const { return s_tourDirectory; }
bool Platform::startFullScreen() const { return s_fullScreen; }
QString Platform::interfaceSizeOverride() const { return s_sizeOverride; }

QUrl Platform::fileUrl(const QString &path) const
{
    return path.isEmpty() ? QUrl() : QUrl::fromLocalFile(path);
}

QString Platform::localPath(const QUrl &url) const
{
    return url.isLocalFile() ? QDir::toNativeSeparators(url.toLocalFile()) : url.toString();
}

QUrl Platform::thumbnailUrl(const QString &videoPath, double durationSeconds) const
{
    if (videoPath.isEmpty())
        return {};
    return QUrl(QStringLiteral("image://videothumb/") + VideoThumbnailProvider::makeId(videoPath, durationSeconds));
}

void Platform::setDataDirectory(const QString &directory)
{
    if (!directory.isEmpty())
        VideoThumbnailProvider::setCacheDirectory(QDir(directory).filePath(QStringLiteral("video-thumbnails")));
}

bool Platform::exists(const QString &path) const
{
    return !path.isEmpty() && QFileInfo::exists(path);
}

QString Platform::fileName(const QString &path) const
{
    return QFileInfo(path).fileName();
}

QString Platform::homeDirectory() const
{
    return QDir::homePath();
}

bool Platform::saveWindow(QObject *window, const QString &path) const
{
    auto *quickWindow = qobject_cast<QQuickWindow *>(window);
    if (!quickWindow)
        return false;
    QDir().mkpath(QFileInfo(path).absolutePath());
    return quickWindow->grabWindow().save(path);
}
