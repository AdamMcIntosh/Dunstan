#pragma once

#include <QQuickFramebufferObject>
#include <QVariantList>
#include <QtQml/qqmlregistration.h>
#include <memory>

struct MpvShared;

/// Video playback through libmpv, rendered into the Qt Quick scene graph (OpenGL).
/// mpv decodes on its own threads; this item only forwards commands and mirrors properties.
class MpvPlayer : public QQuickFramebufferObject
{
    Q_OBJECT
    QML_ELEMENT
    Q_PROPERTY(QString source READ source NOTIFY sourceChanged)
    Q_PROPERTY(double position READ position NOTIFY positionChanged)
    Q_PROPERTY(double duration READ duration NOTIFY durationChanged)
    Q_PROPERTY(bool paused READ paused WRITE setPaused NOTIFY pausedChanged)
    Q_PROPERTY(double volume READ volume WRITE setVolume NOTIFY volumeChanged)
    Q_PROPERTY(bool muted READ muted WRITE setMuted NOTIFY mutedChanged)
    Q_PROPERTY(double speed READ speed WRITE setSpeed NOTIFY speedChanged)
    Q_PROPERTY(bool buffering READ buffering NOTIFY bufferingChanged)
    Q_PROPERTY(bool loaded READ loaded NOTIFY loadedChanged)
    Q_PROPERTY(bool seekable READ seekable NOTIFY seekableChanged)
    Q_PROPERTY(bool ended READ ended NOTIFY endedChanged)
    Q_PROPERTY(QVariantList audioTracks READ audioTracks NOTIFY tracksChanged)
    Q_PROPERTY(QVariantList subtitleTracks READ subtitleTracks NOTIFY tracksChanged)
    Q_PROPERTY(int audioTrack READ audioTrack WRITE setAudioTrack NOTIFY audioTrackChanged)
    Q_PROPERTY(int subtitleTrack READ subtitleTrack WRITE setSubtitleTrack NOTIFY subtitleTrackChanged)
    Q_PROPERTY(QString videoInfo READ videoInfo NOTIFY videoInfoChanged)
    Q_PROPERTY(QString errorString READ errorString NOTIFY errorStringChanged)
    Q_PROPERTY(double subtitleLift READ subtitleLift WRITE setSubtitleLift NOTIFY subtitleLiftChanged)

public:
    explicit MpvPlayer(QQuickItem *parent = nullptr);
    ~MpvPlayer() override;

    Renderer *createRenderer() const override;

    QString source() const { return m_source; }
    double position() const { return m_position; }
    double duration() const { return m_duration; }
    bool paused() const { return m_paused; }
    void setPaused(bool paused);
    double volume() const { return m_volume; }
    void setVolume(double volume);
    bool muted() const { return m_muted; }
    void setMuted(bool muted);
    double speed() const { return m_speed; }
    void setSpeed(double speed);
    bool buffering() const { return m_buffering; }
    bool loaded() const { return m_loaded; }
    bool seekable() const { return m_seekable; }
    bool ended() const { return m_ended; }
    QVariantList audioTracks() const { return m_audioTracks; }
    QVariantList subtitleTracks() const { return m_subtitleTracks; }
    int audioTrack() const { return m_audioTrack; }
    void setAudioTrack(int id);
    int subtitleTrack() const { return m_subtitleTrack; }
    void setSubtitleTrack(int id);
    QString videoInfo() const;
    QString errorString() const { return m_error; }
    /// Fraction of the video height to keep clear below subtitles (e.g. while controls show).
    double subtitleLift() const { return m_subtitleLift; }
    void setSubtitleLift(double lift);

    /// Opens a file, optionally starting at `start` seconds (resume or live tune-in).
    Q_INVOKABLE void load(const QString &path, double start = 0);
    Q_INVOKABLE void stop();
    Q_INVOKABLE void togglePause();
    Q_INVOKABLE void seek(double seconds);
    Q_INVOKABLE void seekRelative(double seconds);
    /// Adds an external subtitle file (SRT, ASS, SSA, VTT, SUB) and selects it.
    Q_INVOKABLE void addSubtitleFile(const QString &path);
    /// Preferred track languages (comma-separated ISO codes) and whether subtitles start enabled.
    Q_INVOKABLE void setPreferences(const QString &audioLanguages, const QString &subtitleLanguages, bool subtitlesOn);
    Q_INVOKABLE void cycleAudio();
    Q_INVOKABLE void cycleSubtitles();

    static QString mpvVersion();

signals:
    void sourceChanged();
    void positionChanged();
    void durationChanged();
    void pausedChanged();
    void volumeChanged();
    void mutedChanged();
    void speedChanged();
    void bufferingChanged();
    void loadedChanged();
    void seekableChanged();
    void endedChanged();
    void tracksChanged();
    void audioTrackChanged();
    void subtitleTrackChanged();
    void videoInfoChanged();
    void errorStringChanged();
    void subtitleLiftChanged();
    void fileLoaded();
    void endReached();
    void playbackFailed(const QString &message);

private slots:
    void processEvents();
    void renderContextReady();

private:
    void command(const QList<QByteArray> &args);
    void setPropertyString(const char *name, const QByteArray &value);
    void setError(const QString &error);
    void setLoaded(bool loaded);
    void updateBuffering();
    void startLoad(const QString &path, double start);

    std::shared_ptr<MpvShared> m_shared;
    QString m_source;
    double m_position = 0;
    double m_duration = 0;
    bool m_paused = false;
    double m_volume = 100;
    bool m_muted = false;
    double m_speed = 1;
    bool m_buffering = false;
    bool m_cacheWait = false;
    bool m_seeking = false;
    bool m_loaded = false;
    bool m_seekable = false;
    bool m_ended = false;
    QVariantList m_audioTracks;
    QVariantList m_subtitleTracks;
    int m_audioTrack = 0;
    int m_subtitleTrack = 0;
    QString m_videoCodec;
    QString m_hwdec;
    qint64 m_width = 0;
    qint64 m_height = 0;
    QString m_error;
    bool m_renderReady = false;
    double m_subtitleLift = 0;
    QString m_pendingPath;
    double m_pendingStart = 0;
};
