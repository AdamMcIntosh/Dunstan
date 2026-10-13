#include "VideoThumbnailProvider.h"

#include <QAtomicInt>
#include <QImage>
#include <atomic>
#include <memory>
#include <QCoreApplication>
#include <QCryptographicHash>
#include <QDateTime>
#include <QDir>
#include <QFileInfo>
#include <QImageReader>
#include <QMutex>
#include <QProcess>
#include <QRandomGenerator>
#include <QRunnable>
#include <QStandardPaths>

namespace {

QMutex s_dirMutex;
QString s_cacheDir;
QAtomicInt s_priority;

QString cacheDirectory()
{
    QMutexLocker lock(&s_dirMutex);
    if (s_cacheDir.isEmpty())
        s_cacheDir = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation) + QStringLiteral("/Dustan/video-thumbnails");
    return s_cacheDir;
}

QString ffmpegExecutable()
{
    static const QString path = [] {
        const auto bundled = QDir(QCoreApplication::applicationDirPath()).filePath(QStringLiteral("ffmpeg"));
        if (QFileInfo(bundled).isExecutable() || QFileInfo(bundled + QStringLiteral(".exe")).isExecutable())
            return bundled;
        return QStandardPaths::findExecutable(QStringLiteral("ffmpeg"));
    }();
    return path;
}

QImage extractFrame(const QString &path, double duration, const std::atomic_bool &cancelled)
{
    const QFileInfo file(path);
    if (!file.exists())
        return {};
    const QByteArray key = QCryptographicHash::hash(
        (path + QLatin1Char('|') + QString::number(file.size()) + QLatin1Char('|') + QString::number(file.lastModified().toMSecsSinceEpoch())).toUtf8(),
        QCryptographicHash::Sha1).toHex();
    const QDir dir(cacheDirectory());
    const QString target = dir.filePath(QString::fromLatin1(key) + QStringLiteral(".jpg"));
    const QString failed = dir.filePath(QString::fromLatin1(key) + QStringLiteral(".fail"));

    QImage image;
    if (QFileInfo::exists(target) && image.load(target))
        return image;
    // Remember failures for a day so unreadable files are not re-probed on every scroll.
    if (QFileInfo::exists(failed) && QFileInfo(failed).lastModified().secsTo(QDateTime::currentDateTime()) < 24 * 3600)
        return {};
    if (cancelled || ffmpegExecutable().isEmpty())
        return {};

    dir.mkpath(QStringLiteral("."));
    // A fifth of the way in avoids cold opens and title cards; keyframe seeking keeps it fast over SMB.
    const double at = duration > 30 ? qMin(duration * 0.2, duration - 10) : 8.0;
    // Unique per job: the same video can be requested by two cards at once (e.g. the TV guide).
    const QString temp = target + QLatin1Char('.') + QString::number(QRandomGenerator::global()->generate(), 16) + QStringLiteral(".part.jpg");
    QProcess ffmpeg;
    ffmpeg.setProcessChannelMode(QProcess::MergedChannels);
    ffmpeg.start(ffmpegExecutable(), {
        QStringLiteral("-hide_banner"), QStringLiteral("-loglevel"), QStringLiteral("error"), QStringLiteral("-nostdin"),
        QStringLiteral("-ss"), QString::number(at, 'f', 2), QStringLiteral("-i"), path,
        QStringLiteral("-map"), QStringLiteral("0:v:0"), QStringLiteral("-frames:v"), QStringLiteral("1"),
        QStringLiteral("-vf"), QStringLiteral("scale=640:-2:flags=lanczos"), QStringLiteral("-q:v"), QStringLiteral("3"),
        QStringLiteral("-y"), temp,
    });
    if (!ffmpeg.waitForStarted(5000))
        return {};
    // Poll so a cancelled request (card scrolled away) stops the child promptly.
    for (int waited = 0; !ffmpeg.waitForFinished(250); waited += 250) {
        if (cancelled || waited > 25000) {
            ffmpeg.kill();
            ffmpeg.waitForFinished(2000);
            QFile::remove(temp);
            if (!cancelled) {
                QFile marker(failed);
                if (marker.open(QIODevice::WriteOnly))
                    marker.write("timeout");
            }
            return {};
        }
    }
    if (ffmpeg.exitStatus() != QProcess::NormalExit || ffmpeg.exitCode() != 0 || !image.load(temp)) {
        QFile::remove(temp);
        if (QFileInfo::exists(target) && image.load(target))
            return image; // Another request for the same video finished first.
        QFile marker(failed);
        if (marker.open(QIODevice::WriteOnly))
            marker.write(ffmpeg.readAll().left(2000));
        return {};
    }
    if (!QFile::rename(temp, target))
        QFile::remove(temp); // Another request already saved this frame.
    return image;
}

// Runs ffmpeg on a pool thread. It owns everything it touches: Qt may drop a cancelled response
// before the job starts, so results come back through a queued signal that is disconnected
// automatically if the response no longer exists.
class ThumbnailJob : public QObject, public QRunnable
{
    Q_OBJECT
public:
    ThumbnailJob(QString path, double duration, QSize requested, std::shared_ptr<std::atomic_bool> cancelled)
        : m_path(std::move(path)), m_duration(duration), m_requested(requested), m_cancelled(std::move(cancelled))
    {
        setAutoDelete(true);
    }

    void run() override
    {
        QImage image;
        if (!*m_cancelled)
            image = extractFrame(m_path, m_duration, *m_cancelled);
        if (!image.isNull() && m_requested.width() > 0 && m_requested.width() < image.width())
            image = image.scaledToWidth(m_requested.width(), Qt::SmoothTransformation);
        emit done(image);
    }

signals:
    void done(const QImage &image);

private:
    QString m_path;
    double m_duration;
    QSize m_requested;
    std::shared_ptr<std::atomic_bool> m_cancelled;
};

class ThumbnailResponse : public QQuickImageResponse
{
public:
    ThumbnailResponse(const QString &path, double duration, QSize requested, QThreadPool *pool)
        : m_name(QFileInfo(path).fileName())
    {
        auto *job = new ThumbnailJob(path, duration, requested, m_cancelled);
        connect(job, &ThumbnailJob::done, this, [this](const QImage &image) {
            m_image = image;
            emit finished();
        }, Qt::QueuedConnection);
        // Newest requests first: the cards currently on screen matter more than ones scrolled past.
        pool->start(job, s_priority.fetchAndAddRelaxed(1));
    }

    QQuickTextureFactory *textureFactory() const override
    {
        return QQuickTextureFactory::textureFactoryForImage(m_image);
    }

    QString errorString() const override
    {
        // Cancelled requests (cards scrolled away) are not errors.
        return m_image.isNull() && !*m_cancelled ? QStringLiteral("No preview frame for %1").arg(m_name) : QString();
    }

    void cancel() override { *m_cancelled = true; }

private:
    std::shared_ptr<std::atomic_bool> m_cancelled = std::make_shared<std::atomic_bool>(false);
    QString m_name;
    QImage m_image;
};

} // namespace

VideoThumbnailProvider::VideoThumbnailProvider()
{
    // Two concurrent extractions keep the NAS and CPU responsive while the grid fills in.
    m_pool.setMaxThreadCount(2);
}

void VideoThumbnailProvider::setCacheDirectory(const QString &directory)
{
    QMutexLocker lock(&s_dirMutex);
    s_cacheDir = directory;
}

QString VideoThumbnailProvider::makeId(const QString &path, double durationSeconds)
{
    return QString::fromLatin1(path.toUtf8().toBase64(QByteArray::Base64UrlEncoding | QByteArray::OmitTrailingEquals))
        + QStringLiteral("/") + QString::number(qRound(durationSeconds));
}

QQuickImageResponse *VideoThumbnailProvider::requestImageResponse(const QString &id, const QSize &requestedSize)
{
    const auto slash = id.lastIndexOf(QLatin1Char('/'));
    const auto path = QString::fromUtf8(QByteArray::fromBase64(id.left(slash).toLatin1(),
        QByteArray::Base64UrlEncoding | QByteArray::OmitTrailingEquals));
    const double duration = id.mid(slash + 1).toDouble();
    return new ThumbnailResponse(path, duration, requestedSize, &m_pool);
}

#include "VideoThumbnailProvider.moc"
