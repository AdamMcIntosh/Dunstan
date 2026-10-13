import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Dialogs
import QtQuick.Layouts
import Dustan

// Library locations and scans, interface and playback preferences, and diagnostics.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    readonly property var info: app.info
    readonly property var counts: info.counts || ({})

    property bool musicScanning: false
    property var musicProgress: null
    property string musicMessage: ""
    property bool videoScanning: false
    property string videoMessage: ""

    onInfoChanged: {
        if (!musicPath.input.activeFocus) musicPath.text = info.musicRoot || ""
        if (!videoPath.input.activeFocus) videoPath.text = info.videoRoot || ""
        musicScanning = !!info.musicScanning
        videoScanning = !!info.videoScanning
    }

    Connections {
        target: Bridge
        function onEvent(name, data) {
            if (name === "musicScan") {
                page.musicProgress = data
                page.musicScanning = !data.complete
                if (data.complete) page.musicMessage = data.error ? qsTr("Scan finished with problems: %1").arg(data.error) : qsTr("Scan complete. Unchanged files were skipped.")
            } else if (name === "videoScan") {
                page.videoScanning = data.state === "running"
                page.videoMessage = data.message || ""
            }
        }
    }

    function scanMusic(saveFirst) {
        musicMessage = ""
        const start = () => Bridge.call("music.scan", {}, (r, error) => {
            if (error) { musicMessage = error; return }
            musicScanning = true
            musicProgress = null
        })
        if (saveFirst) {
            Bridge.call("app.setMusicPath", { path: musicPath.text }, (path, error) => {
                if (error) { musicMessage = error; return }
                musicPath.text = path
                app.refreshInfo()
                start()
            })
        } else start()
    }
    function scanVideos() {
        videoMessage = ""
        Bridge.call("video.scan", { path: videoPath.text }, (r, error) => {
            if (error) { videoMessage = error; return }
            videoScanning = true
            videoMessage = qsTr("Starting…")
        })
    }

    component Card: Rectangle {
        id: card
        default property alias content: inner.data
        property string title
        property string description
        Layout.fillWidth: true
        implicitHeight: inner.implicitHeight + Theme.s(48)
        radius: Theme.radiusL
        color: Theme.surface
        border.width: 1
        border.color: Theme.line
        ColumnLayout {
            id: inner
            anchors { left: parent.left; right: parent.right; top: parent.top; margins: Theme.s(24) }
            spacing: Theme.s(14)
            ColumnLayout {
                spacing: Theme.s(4)
                Layout.fillWidth: true
                Layout.bottomMargin: Theme.s(4)
                Text { text: card.title; color: Theme.text; font.family: Theme.family; font.pixelSize: Theme.subheading + Theme.s(1); font.weight: Font.DemiBold }
                Text {
                    visible: text.length > 0
                    Layout.fillWidth: true
                    text: card.description
                    color: Theme.text3
                    font.family: Theme.family; font.pixelSize: Theme.label
                    wrapMode: Text.Wrap
                }
            }
        }
    }
    component Status: RowLayout {
        id: status
        property string icon: "info"
        property color tone: Theme.text2
        property string text
        property bool busy: false
        spacing: Theme.s(10)
        Layout.fillWidth: true
        visible: text.length > 0
        Item {
            Layout.preferredWidth: Theme.s(18); Layout.preferredHeight: Theme.s(18)
            Layout.alignment: Qt.AlignTop
            Icon { anchors.centerIn: parent; visible: !status.busy; name: status.icon; size: Theme.s(17); color: status.tone }
            Spinner { anchors.centerIn: parent; visible: status.busy; size: Theme.s(16) }
        }
        Text { Layout.fillWidth: true; text: status.text; color: Theme.text2; font.family: Theme.family; font.pixelSize: Theme.label; wrapMode: Text.Wrap }
    }
    component KeyValue: RowLayout {
        id: pair
        property string key
        property string value
        Layout.fillWidth: true
        spacing: Theme.s(16)
        Text { Layout.preferredWidth: Theme.s(170); text: pair.key; color: Theme.text3; font.family: Theme.family; font.pixelSize: Theme.label }
        Text { Layout.fillWidth: true; text: pair.value; color: Theme.text; font.family: Theme.family; font.pixelSize: Theme.label; elide: Text.ElideMiddle }
    }

    Flickable {
        anchors.fill: parent
        contentHeight: column.implicitHeight + Theme.s(48)
        clip: true
        boundsBehavior: Flickable.StopAtBounds
        T.ScrollBar.vertical: ThinScrollBar {}

        ColumnLayout {
            id: column
            x: (app ? app.pagePadding : 0)
            width: Math.min(parent.width - (app ? app.pagePadding : 0) * 2, Theme.s(880))
            spacing: Theme.s(18)

            PageHeader {
                Layout.fillWidth: true
                Layout.topMargin: Theme.s(12)
                title: qsTr("Settings")
                subtitle: qsTr("Your files stay on the NAS. Dustan keeps its indexes, progress and channels in %1").arg(page.info.dataDirectory || "")
            }

            // ---- music ----
            Card {
                title: qsTr("Music library")
                description: qsTr("A mounted folder (e.g. /mnt/music) or a UNC path on Windows. Use one canonical path; the same files under two mounts create duplicates.")
                RowLayout {
                    Layout.fillWidth: true
                    spacing: Theme.s(10)
                    Field { id: musicPath; Layout.fillWidth: true; placeholder: qsTr("/mnt/music"); onAccepted: page.scanMusic(true) }
                    PillButton { Layout.alignment: Qt.AlignBottom; glyph: "folder"; text: qsTr("Browse…"); onClicked: { folderDialog.target = musicPath; folderDialog.open() } }
                }
                RowLayout {
                    spacing: Theme.s(10)
                    PillButton {
                        kind: "primary"; glyph: "refresh"
                        text: musicPath.text !== (page.info.musicRoot || "") ? qsTr("Save & scan") : qsTr("Rescan")
                        enabled: !page.musicScanning && musicPath.text.trim().length > 0
                        onClicked: page.scanMusic(musicPath.text !== (page.info.musicRoot || ""))
                    }
                    PillButton { visible: page.musicScanning; kind: "ghost"; text: qsTr("Cancel scan"); onClicked: Bridge.call("music.cancelScan", {}) }
                }
                Status {
                    busy: page.musicScanning
                    icon: page.info.musicRoot && !page.info.musicRootAvailable ? "warning" : "check"
                    tone: page.info.musicRoot && !page.info.musicRootAvailable ? Theme.live : Theme.positive
                    text: page.musicScanning
                          ? (page.musicProgress ? qsTr("Scanning  ·  %1 seen  ·  %2 read  ·  %3 unchanged  ·  %4 removed\n%5")
                                 .arg(page.musicProgress.seen).arg(page.musicProgress.tagged).arg(page.musicProgress.skipped).arg(page.musicProgress.removed).arg(page.musicProgress.current || "")
                             : qsTr("Scanning…"))
                          : page.musicMessage || (page.info.musicRoot
                             ? (page.info.musicRootAvailable ? qsTr("%1 songs indexed from %2").arg(Number(page.counts.tracks || 0).toLocaleString(Qt.locale(), "f", 0)).arg(page.info.musicRoot)
                                : qsTr("%1 is not reachable. Mount the NAS share; your index is kept and playback resumes once it's back.").arg(page.info.musicRoot))
                             : qsTr("No music folder yet."))
                }
            }

            // ---- video ----
            Card {
                title: qsTr("Video library")
                description: qsTr("Kept separate from music. Runtimes are read by ffprobe in a separate process with a 20-second limit per file; unchanged files reuse cached runtimes, and a failed or cancelled scan keeps your previous catalog.")
                RowLayout {
                    Layout.fillWidth: true
                    spacing: Theme.s(10)
                    Field { id: videoPath; Layout.fillWidth: true; placeholder: qsTr("/mnt/video"); onAccepted: page.scanVideos() }
                    PillButton { Layout.alignment: Qt.AlignBottom; glyph: "folder"; text: qsTr("Browse…"); onClicked: { folderDialog.target = videoPath; folderDialog.open() } }
                }
                RowLayout {
                    spacing: Theme.s(10)
                    PillButton {
                        kind: "primary"; glyph: "refresh"
                        text: videoPath.text !== (page.info.videoRoot || "") ? qsTr("Save & scan") : qsTr("Rescan")
                        enabled: !page.videoScanning && videoPath.text.trim().length > 0 && !!page.info.ffprobe
                        onClicked: page.scanVideos()
                    }
                    PillButton { visible: page.videoScanning; kind: "ghost"; text: qsTr("Cancel scan"); onClicked: Bridge.call("video.cancelScan", {}) }
                }
                Status {
                    busy: page.videoScanning
                    icon: !page.info.ffprobe || page.info.videoLoadError ? "warning" : "check"
                    tone: !page.info.ffprobe || page.info.videoLoadError ? Theme.live : Theme.positive
                    text: page.info.videoLoadError ? page.info.videoLoadError
                        : !page.info.ffprobe ? qsTr("ffprobe was not found. Install FFmpeg (with ffprobe) and make it available on PATH to scan videos.")
                        : page.videoMessage.length ? (page.videoScanning ? qsTr("Scanning  ·  %1").arg(page.videoMessage) : page.videoMessage)
                        : page.info.videoRoot ? qsTr("%1 videos from %2").arg(Number(page.counts.videos || 0).toLocaleString(Qt.locale(), "f", 0)).arg(page.info.videoRoot)
                            + (page.counts.unknownDurations > 0 ? qsTr("  ·  %1 with unknown runtime (not schedulable on TV)").arg(page.counts.unknownDurations) : "")
                        : qsTr("No video folder yet.")
                }
            }

            // ---- interface ----
            Card {
                title: qsTr("Interface")
                description: qsTr("Auto keeps desktop sizing and grows on very large windows. Choose TV for a living-room screen viewed from across the room.")
                Segmented {
                    readonly property var keys: ["auto", "compact", "standard", "large", "tv"]
                    options: [qsTr("Auto"), qsTr("Compact"), qsTr("Standard"), qsTr("Large"), qsTr("TV")]
                    currentIndex: Math.max(0, keys.indexOf(app.prefs.interfaceSize))
                    onActivated: index => app.prefs.interfaceSize = keys[index]
                }
                Toggle {
                    Layout.fillWidth: true
                    text: qsTr("Play videos full screen")
                    description: qsTr("Press F or double-click to switch at any time.")
                    checked: app.prefs.videoFullScreen
                    onToggled: app.prefs.videoFullScreen = checked
                }
                Toggle {
                    Layout.fillWidth: true
                    text: qsTr("Play the next episode automatically")
                    description: qsTr("Counts down for ten seconds at the end of an episode.")
                    checked: app.prefs.autoplayNext
                    onToggled: app.prefs.autoplayNext = checked
                }
            }

            // ---- playback ----
            Card {
                title: qsTr("Video playback")
                description: qsTr("Language codes in order of preference, such as en,ja or eng. Leave blank to use each file's default tracks.")
                RowLayout {
                    Layout.fillWidth: true
                    spacing: Theme.s(16)
                    Field {
                        Layout.fillWidth: true
                        label: qsTr("Audio languages")
                        placeholder: qsTr("e.g. en,ja")
                        text: app.prefs.audioLanguages
                        input.onEditingFinished: app.prefs.audioLanguages = text.trim()
                    }
                    Field {
                        Layout.fillWidth: true
                        label: qsTr("Subtitle languages")
                        placeholder: qsTr("e.g. en")
                        text: app.prefs.subtitleLanguages
                        input.onEditingFinished: app.prefs.subtitleLanguages = text.trim()
                    }
                }
                Toggle {
                    Layout.fillWidth: true
                    text: qsTr("Show subtitles when available")
                    description: qsTr("Embedded tracks and subtitle files next to the video (same name) are picked up automatically.")
                    checked: app.prefs.subtitlesOn
                    onToggled: app.prefs.subtitlesOn = checked
                }
            }

            // ---- about ----
            Card {
                title: qsTr("About Dustan")
                KeyValue { key: qsTr("Version"); value: Platform.appVersion + "  (service " + (page.info.version || "—") + ")" }
                KeyValue { key: qsTr("Data folder"); value: page.info.dataDirectory || "" }
                KeyValue { key: qsTr("Music playback"); value: page.info.musicEngineError ? page.info.musicEngineError : qsTr("LibVLC (in the library service)") }
                KeyValue { key: qsTr("Video playback"); value: Platform.mpvVersion }
                KeyValue { key: qsTr("ffprobe"); value: page.info.ffprobe || qsTr("Not found") }
                KeyValue { key: qsTr("Qt"); value: Platform.qtVersion + "  ·  " + Platform.os }
                PillButton {
                    Layout.topMargin: Theme.s(4)
                    kind: "ghost"; compact: true; glyph: "folder"
                    text: qsTr("Open data folder")
                    onClicked: Qt.openUrlExternally(Platform.fileUrl(page.info.dataDirectory))
                }
            }
        }
    }

    FolderDialog {
        id: folderDialog
        property var target: null
        title: qsTr("Choose folder")
        currentFolder: target && target.text ? Platform.fileUrl(target.text) : Platform.fileUrl(Platform.homeDirectory())
        onAccepted: if (target) target.text = Platform.localPath(selectedFolder)
    }
}
