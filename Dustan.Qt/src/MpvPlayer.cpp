#include "MpvPlayer.h"

#include <QGuiApplication>
#include <QHash>
#include <QMetaObject>
#include <QMutex>
#include <QOpenGLContext>
#include <QOpenGLFramebufferObject>
#include <QQuickOpenGLUtils>
#include <QQuickWindow>

#include <mpv/client.h>
#include <mpv/render_gl.h>

/// Owns the mpv core. Shared by the item (GUI thread) and its renderer (render thread), because Qt
/// destroys the renderer later than the item; whichever goes last terminates mpv.
struct MpvShared
{
    mpv_handle *mpv = nullptr;
    QMutex mutex;
    MpvPlayer *item = nullptr; // cleared when the item is destroyed; guarded by mutex
    void *waylandDisplay = nullptr;
    void *x11Display = nullptr;

    ~MpvShared()
    {
        if (mpv)
            mpv_terminate_destroy(mpv);
    }

    void post(const char *method)
    {
        QMutexLocker lock(&mutex);
        if (item)
            QMetaObject::invokeMethod(item, method, Qt::QueuedConnection);
    }
};

namespace {

enum Observed : uint64_t {
    TimePos = 1, Duration, Pause, Volume, Mute, Speed, PausedForCache, Seekable, EofReached,
    TrackList, Aid, Sid, HwdecCurrent, Width, Height, Seeking
};

void *getProcAddress(void *, const char *name)
{
    auto *context = QOpenGLContext::currentContext();
    return context ? reinterpret_cast<void *>(context->getProcAddress(QByteArray(name))) : nullptr;
}

int trackId(const char *value)
{
    bool ok = false;
    const int id = QByteArray(value).toInt(&ok);
    return ok ? id : 0;
}

QVariant nodeToVariant(const mpv_node &node)
{
    switch (node.format) {
    case MPV_FORMAT_STRING: return QString::fromUtf8(node.u.string);
    case MPV_FORMAT_FLAG: return bool(node.u.flag);
    case MPV_FORMAT_INT64: return qint64(node.u.int64);
    case MPV_FORMAT_DOUBLE: return node.u.double_;
    case MPV_FORMAT_NODE_ARRAY: {
        QVariantList list;
        for (int i = 0; i < node.u.list->num; ++i)
            list.append(nodeToVariant(node.u.list->values[i]));
        return list;
    }
    case MPV_FORMAT_NODE_MAP: {
        QVariantMap map;
        for (int i = 0; i < node.u.list->num; ++i)
            map.insert(QString::fromUtf8(node.u.list->keys[i]), nodeToVariant(node.u.list->values[i]));
        return map;
    }
    default: return {};
    }
}

class MpvRenderer : public QQuickFramebufferObject::Renderer
{
public:
    explicit MpvRenderer(std::shared_ptr<MpvShared> shared) : m_shared(std::move(shared)) {}

    ~MpvRenderer() override
    {
        // Runs on the render thread with the GL context current, as mpv requires.
        if (m_context)
            mpv_render_context_free(m_context);
    }

    QOpenGLFramebufferObject *createFramebufferObject(const QSize &size) override
    {
        if (!m_context && m_shared->mpv) {
            mpv_opengl_init_params gl{getProcAddress, nullptr};
            QList<mpv_render_param> params{
                {MPV_RENDER_PARAM_API_TYPE, const_cast<char *>(MPV_RENDER_API_TYPE_OPENGL)},
                {MPV_RENDER_PARAM_OPENGL_INIT_PARAMS, &gl},
            };
            // Display handles let mpv use zero-copy hardware decoding (VA-API/VDPAU) on Linux.
            if (m_shared->waylandDisplay)
                params.append({MPV_RENDER_PARAM_WL_DISPLAY, m_shared->waylandDisplay});
            if (m_shared->x11Display)
                params.append({MPV_RENDER_PARAM_X11_DISPLAY, m_shared->x11Display});
            params.append({MPV_RENDER_PARAM_INVALID, nullptr});
            if (mpv_render_context_create(&m_context, m_shared->mpv, params.data()) < 0) {
                m_context = nullptr;
                qWarning("mpv: could not create the OpenGL render context");
            } else {
                mpv_render_context_set_update_callback(m_context, [](void *ctx) {
                    static_cast<MpvShared *>(ctx)->post("update");
                }, m_shared.get());
                // vo=libmpv only initialises video for files opened after this point.
                m_shared->post("renderContextReady");
            }
        }
        QOpenGLFramebufferObjectFormat format;
        format.setAttachment(QOpenGLFramebufferObject::NoAttachment);
        return new QOpenGLFramebufferObject(size, format);
    }

    void render() override
    {
        if (!m_context)
            return;
        auto *fbo = framebufferObject();
        mpv_opengl_fbo target{int(fbo->handle()), fbo->width(), fbo->height(), 0};
        int flipY = 0; // Qt 6 samples FBO textures in GL orientation; no flip needed.
        mpv_render_param params[] = {
            {MPV_RENDER_PARAM_OPENGL_FBO, &target},
            {MPV_RENDER_PARAM_FLIP_Y, &flipY},
            {MPV_RENDER_PARAM_INVALID, nullptr},
        };
        mpv_render_context_render(m_context, params);
        QQuickOpenGLUtils::resetOpenGLState();
    }

private:
    std::shared_ptr<MpvShared> m_shared;
    mpv_render_context *m_context = nullptr;
};

} // namespace

MpvPlayer::MpvPlayer(QQuickItem *parent)
    : QQuickFramebufferObject(parent), m_shared(std::make_shared<MpvShared>())
{
#if QT_CONFIG(wayland)
    if (auto *wayland = qGuiApp->nativeInterface<QNativeInterface::QWaylandApplication>())
        m_shared->waylandDisplay = wayland->display();
#endif
#if QT_CONFIG(xcb)
    if (auto *x11 = qGuiApp->nativeInterface<QNativeInterface::QX11Application>())
        m_shared->x11Display = x11->display();
#endif

    auto *mpv = mpv_create();
    if (!mpv) {
        m_error = tr("The video engine (libmpv) could not start.");
        return;
    }
    m_shared->mpv = mpv;
    m_shared->item = this;
    const std::pair<const char *, const char *> options[] = {
        {"vo", "libmpv"},
        {"hwdec", "auto-safe"},
        {"keep-open", "yes"},       // Hold the last frame at the end so the UI can offer what's next.
        {"idle", "yes"},
        {"config", "no"},
        {"load-scripts", "no"},
        {"osc", "no"},
        {"input-default-bindings", "no"},
        {"input-vo-keyboard", "no"},
        {"terminal", "no"},
        {"msg-level", "all=warn"},
        {"audio-client-name", "Dustan"},
        {"ytdl", "no"},
        {"sub-auto", "fuzzy"},      // Pick up subtitle files beside the video, as VLC did.
        {"sub-font-size", "38"},
        {"sub-border-size", "2.5"},
        {"sub-shadow-offset", "1"},
        {"sub-ass-override", "no"},
        {"cache", "yes"},           // Generous read-ahead smooths NAS stalls.
        {"demuxer-max-bytes", "256MiB"},
        {"demuxer-max-back-bytes", "64MiB"},
        {"demuxer-readahead-secs", "30"},
    };
    for (const auto &[name, value] : options)
        mpv_set_option_string(mpv, name, value);

    mpv_set_wakeup_callback(mpv, [](void *ctx) { static_cast<MpvShared *>(ctx)->post("processEvents"); }, m_shared.get());
    if (mpv_initialize(mpv) < 0) {
        m_error = tr("The video engine (libmpv) could not initialise.");
        return;
    }
    mpv_observe_property(mpv, TimePos, "time-pos", MPV_FORMAT_DOUBLE);
    mpv_observe_property(mpv, Duration, "duration", MPV_FORMAT_DOUBLE);
    mpv_observe_property(mpv, Pause, "pause", MPV_FORMAT_FLAG);
    mpv_observe_property(mpv, Volume, "volume", MPV_FORMAT_DOUBLE);
    mpv_observe_property(mpv, Mute, "mute", MPV_FORMAT_FLAG);
    mpv_observe_property(mpv, Speed, "speed", MPV_FORMAT_DOUBLE);
    mpv_observe_property(mpv, PausedForCache, "paused-for-cache", MPV_FORMAT_FLAG);
    mpv_observe_property(mpv, Seeking, "seeking", MPV_FORMAT_FLAG);
    mpv_observe_property(mpv, Seekable, "seekable", MPV_FORMAT_FLAG);
    mpv_observe_property(mpv, EofReached, "eof-reached", MPV_FORMAT_FLAG);
    mpv_observe_property(mpv, TrackList, "track-list", MPV_FORMAT_NODE);
    mpv_observe_property(mpv, Aid, "aid", MPV_FORMAT_STRING);
    mpv_observe_property(mpv, Sid, "sid", MPV_FORMAT_STRING);
    mpv_observe_property(mpv, HwdecCurrent, "hwdec-current", MPV_FORMAT_STRING);
    mpv_observe_property(mpv, Width, "width", MPV_FORMAT_INT64);
    mpv_observe_property(mpv, Height, "height", MPV_FORMAT_INT64);
}

MpvPlayer::~MpvPlayer()
{
    {
        QMutexLocker lock(&m_shared->mutex);
        m_shared->item = nullptr;
    }
    // Silence playback now; the core itself is released with the last owner (possibly the renderer).
    if (m_shared->mpv) {
        const char *args[] = {"stop", nullptr};
        mpv_command(m_shared->mpv, args);
    }
}

QQuickFramebufferObject::Renderer *MpvPlayer::createRenderer() const
{
    return new MpvRenderer(m_shared);
}

QString MpvPlayer::mpvVersion()
{
    const auto version = mpv_client_api_version();
    return QStringLiteral("libmpv client API %1.%2").arg(version >> 16).arg(version & 0xffff);
}

void MpvPlayer::command(const QList<QByteArray> &args)
{
    if (!m_shared->mpv)
        return;
    QList<const char *> raw;
    for (const auto &arg : args)
        raw.append(arg.constData());
    raw.append(nullptr);
    mpv_command_async(m_shared->mpv, 0, raw.data());
}

void MpvPlayer::setPropertyString(const char *name, const QByteArray &value)
{
    if (m_shared->mpv)
        mpv_set_property_string(m_shared->mpv, name, value.constData());
}

void MpvPlayer::load(const QString &path, double start)
{
    if (!m_shared->mpv) {
        emit playbackFailed(m_error);
        return;
    }
    setError({});
    setLoaded(false);
    m_source = path;
    emit sourceChanged();
    m_ended = false;
    emit endedChanged();
    m_position = start;
    emit positionChanged();
    if (!m_renderReady) {
        // Opening a file before the render context exists leaves mpv without a video output
        // (audio plays over a black frame), so wait for the scene graph to create it.
        m_pendingPath = path;
        m_pendingStart = start;
        update();
        return;
    }
    startLoad(path, start);
}

void MpvPlayer::startLoad(const QString &path, double start)
{
    // "start" is read when the file opens, so resuming never flashes the first frame.
    setPropertyString("start", start > 0.5 ? QByteArray::number(start, 'f', 3) : QByteArray("none"));
    setPropertyString("pause", "no");
    command({"loadfile", path.toUtf8(), "replace"});
}

void MpvPlayer::renderContextReady()
{
    m_renderReady = true;
    if (!m_pendingPath.isEmpty())
        startLoad(std::exchange(m_pendingPath, {}), m_pendingStart);
}

void MpvPlayer::stop()
{
    m_pendingPath.clear();
    command({"stop"});
    setLoaded(false);
}

void MpvPlayer::togglePause()
{
    if (m_ended) {
        seek(0);
        setPaused(false);
        return;
    }
    setPaused(!m_paused);
}

void MpvPlayer::setPaused(bool paused)
{
    setPropertyString("pause", paused ? "yes" : "no");
}

void MpvPlayer::setSubtitleLift(double lift)
{
    lift = qBound(0.0, lift, 0.5);
    if (qFuzzyCompare(lift, m_subtitleLift))
        return;
    m_subtitleLift = lift;
    // sub-margin-y is measured in mpv's 720-line subtitle space; 22 is mpv's default.
    setPropertyString("sub-margin-y", QByteArray::number(qMax(22, qRound(lift * 720))));
    emit subtitleLiftChanged();
}

void MpvPlayer::setVolume(double volume)
{
    setPropertyString("volume", QByteArray::number(qBound(0.0, volume, 130.0), 'f', 1));
}

void MpvPlayer::setMuted(bool muted)
{
    setPropertyString("mute", muted ? "yes" : "no");
}

void MpvPlayer::setSpeed(double speed)
{
    setPropertyString("speed", QByteArray::number(qBound(0.25, speed, 4.0), 'f', 2));
}

void MpvPlayer::seek(double seconds)
{
    command({"seek", QByteArray::number(qMax(0.0, seconds), 'f', 3), "absolute"});
}

void MpvPlayer::seekRelative(double seconds)
{
    command({"seek", QByteArray::number(seconds, 'f', 3), "relative"});
}

void MpvPlayer::setAudioTrack(int id)
{
    setPropertyString("aid", id > 0 ? QByteArray::number(id) : QByteArray("no"));
}

void MpvPlayer::setSubtitleTrack(int id)
{
    setPropertyString("sid", id > 0 ? QByteArray::number(id) : QByteArray("no"));
}

void MpvPlayer::cycleAudio()
{
    command({"cycle", "audio"});
}

void MpvPlayer::cycleSubtitles()
{
    command({"cycle", "sub"});
}

void MpvPlayer::addSubtitleFile(const QString &path)
{
    command({"sub-add", path.toUtf8(), "select"});
}

void MpvPlayer::setPreferences(const QString &audioLanguages, const QString &subtitleLanguages, bool subtitlesOn)
{
    setPropertyString("alang", audioLanguages.toUtf8());
    setPropertyString("slang", subtitleLanguages.toUtf8());
    setPropertyString("sid", subtitlesOn ? "auto" : "no");
}

QString MpvPlayer::videoInfo() const
{
    QStringList parts;
    static const QHash<QString, QString> names{
        {QStringLiteral("h264"), QStringLiteral("H.264")}, {QStringLiteral("hevc"), QStringLiteral("HEVC")},
        {QStringLiteral("vc1"), QStringLiteral("VC-1")}, {QStringLiteral("mpeg2video"), QStringLiteral("MPEG-2")},
        {QStringLiteral("mpeg4"), QStringLiteral("MPEG-4")}, {QStringLiteral("vp9"), QStringLiteral("VP9")},
        {QStringLiteral("av1"), QStringLiteral("AV1")}, {QStringLiteral("wmv3"), QStringLiteral("WMV")},
    };
    if (!m_videoCodec.isEmpty())
        parts << names.value(m_videoCodec, m_videoCodec.toUpper());
    if (m_width > 0 && m_height > 0)
        parts << QStringLiteral("%1×%2").arg(m_width).arg(m_height);
    parts << (m_hwdec.isEmpty() || m_hwdec == QLatin1String("no") ? tr("software decoding") : tr("hardware decoding (%1)").arg(m_hwdec));
    return parts.join(QStringLiteral(" · "));
}

void MpvPlayer::setError(const QString &error)
{
    if (m_error == error)
        return;
    m_error = error;
    emit errorStringChanged();
}

void MpvPlayer::setLoaded(bool loaded)
{
    if (m_loaded == loaded)
        return;
    m_loaded = loaded;
    emit loadedChanged();
}

void MpvPlayer::processEvents()
{
    auto *mpv = m_shared->mpv;
    if (!mpv)
        return;
    while (true) {
        mpv_event *event = mpv_wait_event(mpv, 0);
        if (event->event_id == MPV_EVENT_NONE)
            break;
        switch (event->event_id) {
        case MPV_EVENT_FILE_LOADED:
            setLoaded(true);
            emit fileLoaded();
            break;
        case MPV_EVENT_END_FILE: {
            auto *end = static_cast<mpv_event_end_file *>(event->data);
            if (end->reason == MPV_END_FILE_REASON_ERROR) {
                const auto message = tr("This video could not be played (%1). The file may be unavailable on the NAS or use an unsupported codec.")
                                         .arg(QString::fromUtf8(mpv_error_string(end->error)));
                setLoaded(false);
                setError(message);
                emit playbackFailed(message);
            }
            break;
        }
        case MPV_EVENT_PROPERTY_CHANGE: {
            auto *property = static_cast<mpv_event_property *>(event->data);
            const bool has = property->format != MPV_FORMAT_NONE && property->data;
            switch (event->reply_userdata) {
            case TimePos:
                m_position = has ? *static_cast<double *>(property->data) : 0;
                emit positionChanged();
                break;
            case Duration:
                m_duration = has ? *static_cast<double *>(property->data) : 0;
                emit durationChanged();
                break;
            case Pause:
                m_paused = has && *static_cast<int *>(property->data);
                emit pausedChanged();
                break;
            case Volume:
                if (has) { m_volume = *static_cast<double *>(property->data); emit volumeChanged(); }
                break;
            case Mute:
                m_muted = has && *static_cast<int *>(property->data);
                emit mutedChanged();
                break;
            case Speed:
                if (has) { m_speed = *static_cast<double *>(property->data); emit speedChanged(); }
                break;
            case PausedForCache:
                m_cacheWait = has && *static_cast<int *>(property->data);
                updateBuffering();
                break;
            case Seeking:
                m_seeking = has && *static_cast<int *>(property->data);
                updateBuffering();
                break;
            case Seekable:
                m_seekable = has && *static_cast<int *>(property->data);
                emit seekableChanged();
                break;
            case EofReached: {
                const bool ended = has && *static_cast<int *>(property->data);
                if (ended != m_ended) {
                    m_ended = ended;
                    emit endedChanged();
                    if (ended)
                        emit endReached();
                }
                break;
            }
            case TrackList: {
                m_audioTracks.clear();
                m_subtitleTracks.clear();
                if (has) {
                    const auto tracks = nodeToVariant(*static_cast<mpv_node *>(property->data)).toList();
                    for (const auto &value : tracks) {
                        const auto track = value.toMap();
                        const auto type = track.value("type").toString();
                        if (type == QLatin1String("audio"))
                            m_audioTracks.append(track);
                        else if (type == QLatin1String("sub"))
                            m_subtitleTracks.append(track);
                        else if (type == QLatin1String("video") && track.value("selected").toBool())
                            m_videoCodec = track.value("codec").toString();
                    }
                }
                emit tracksChanged();
                emit videoInfoChanged();
                break;
            }
            case Aid:
                m_audioTrack = has ? trackId(*static_cast<char **>(property->data)) : 0;
                emit audioTrackChanged();
                break;
            case Sid:
                m_subtitleTrack = has ? trackId(*static_cast<char **>(property->data)) : 0;
                emit subtitleTrackChanged();
                break;
            case HwdecCurrent:
                m_hwdec = has ? QString::fromUtf8(*static_cast<char **>(property->data)) : QString();
                emit videoInfoChanged();
                break;
            case Width:
                m_width = has ? *static_cast<int64_t *>(property->data) : 0;
                emit videoInfoChanged();
                break;
            case Height:
                m_height = has ? *static_cast<int64_t *>(property->data) : 0;
                emit videoInfoChanged();
                break;
            }
            break;
        }
        default:
            break;
        }
    }
}

void MpvPlayer::updateBuffering()
{
    const bool buffering = m_cacheWait || m_seeking;
    if (buffering == m_buffering)
        return;
    m_buffering = buffering;
    emit bufferingChanged();
}
