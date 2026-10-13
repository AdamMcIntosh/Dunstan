#pragma once

#include <QObject>
#include <QUrl>
#include <QtQml/qqmlregistration.h>

/// Small platform helpers for QML: file URLs, version strings and the thumbnail cache location.
class Platform : public QObject
{
    Q_OBJECT
    QML_ELEMENT
    QML_SINGLETON
    Q_PROPERTY(QString appVersion READ appVersion CONSTANT)
    Q_PROPERTY(QString qtVersion READ qtVersion CONSTANT)
    Q_PROPERTY(QString mpvVersion READ mpvVersion CONSTANT)
    Q_PROPERTY(QString os READ os CONSTANT)
    Q_PROPERTY(QString startPage READ startPage CONSTANT)
    Q_PROPERTY(QString tourDirectory READ tourDirectory CONSTANT)
    Q_PROPERTY(bool startFullScreen READ startFullScreen CONSTANT)
    Q_PROPERTY(QString interfaceSizeOverride READ interfaceSizeOverride CONSTANT)
    Q_PROPERTY(QStringList verifyFiles READ verifyFiles CONSTANT)

public:
    using QObject::QObject;

    static void setStartOptions(const QString &page, const QString &tourDirectory, bool fullScreen, const QString &sizeOverride);
    static void setVerifyFiles(const QStringList &files);

    QString appVersion() const;
    QString qtVersion() const;
    QString mpvVersion() const;
    QString os() const;
    QString startPage() const;
    QString tourDirectory() const;
    bool startFullScreen() const;
    QString interfaceSizeOverride() const;
    QStringList verifyFiles() const;

    Q_INVOKABLE QUrl fileUrl(const QString &path) const;
    Q_INVOKABLE QString localPath(const QUrl &url) const;
    Q_INVOKABLE QUrl thumbnailUrl(const QString &videoPath, double durationSeconds) const;
    Q_INVOKABLE void setDataDirectory(const QString &directory);
    Q_INVOKABLE bool exists(const QString &path) const;
    Q_INVOKABLE QString fileName(const QString &path) const;
    Q_INVOKABLE QString homeDirectory() const;
    /// Saves the rendered window (including video frames) as an image; used by the --tour screenshots.
    Q_INVOKABLE bool saveWindow(QObject *window, const QString &path) const;
};
