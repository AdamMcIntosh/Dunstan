#pragma once

#include <QQuickAsyncImageProvider>
#include <QThreadPool>

/// Serves image://videothumb/<id> frames for videos without poster art.
/// Frames are extracted by an ffmpeg child process (never in-process, so a bad file cannot crash
/// Dustan), cached as JPEG under <data>/video-thumbnails, and requested lazily as cards scroll into view.
class VideoThumbnailProvider : public QQuickAsyncImageProvider
{
public:
    VideoThumbnailProvider();
    QQuickImageResponse *requestImageResponse(const QString &id, const QSize &requestedSize) override;

    static void setCacheDirectory(const QString &directory);
    /// The id for Platform.thumbnailUrl(): base64url(path) plus a seek hint in seconds.
    static QString makeId(const QString &path, double durationSeconds);

private:
    QThreadPool m_pool;
};
