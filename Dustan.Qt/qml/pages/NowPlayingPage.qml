import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Effects
import QtQuick.Layouts
import Dustan

// Full-screen music view: large artwork over an ambient wash, transport, and the queue.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    readonly property var music: app.music
    readonly property var track: music.track
    property url cover
    property int queueIndex: -1

    function loadQueue() {
        if (!music.active) { queue.clear(); return }
        Bridge.load(queue, "player.queue", {}, "tracks", result => {
            page.queueIndex = result.index
            if (page.queueIndex >= 0) list.positionViewAtIndex(page.queueIndex, ListView.Beginning)
        })
    }
    function loadCover() {
        cover = track && track.art ? Platform.fileUrl(track.art) : ""
        if (track && track.albumId) {
            const albumId = track.albumId
            Bridge.call("music.cover", { id: albumId }, path => {
                if (path && page.track && page.track.albumId === albumId) page.cover = Platform.fileUrl(path)
            })
        }
    }
    onTrackChanged: loadCover()
    onVisibleChanged: if (visible) loadQueue()
    Connections { target: page.music; function onQueueRevisionChanged() { if (page.visible) page.loadQueue() } }
    Component.onCompleted: loadCover()

    JsonListModel { id: queue; roles: ["id", "title", "artist", "album", "albumArtist", "albumId", "artistId", "trackNumber", "discNumber", "year", "genre", "duration", "file", "art"] }

    // ---- ambient background -------------------------------------------------------------------------
    Image {
        id: wash
        anchors.fill: parent
        source: page.cover
        sourceSize: Qt.size(128, 128)
        fillMode: Image.PreserveAspectCrop
        visible: false
    }
    MultiEffect {
        anchors.fill: parent
        source: wash
        visible: wash.status === Image.Ready && page.music.active
        blurEnabled: true
        blur: 1.0
        blurMax: 64
        brightness: -0.42
        saturation: 0.05
        opacity: visible ? 1 : 0
        Behavior on opacity { NumberAnimation { duration: Theme.slow } }
    }
    Rectangle {
        anchors.fill: parent
        gradient: Gradient {
            orientation: Gradient.Horizontal
            GradientStop { position: 0.0; color: Qt.rgba(0.043, 0.047, 0.059, 0.2) }
            GradientStop { position: 0.62; color: Qt.rgba(0.043, 0.047, 0.059, 0.45) }
            GradientStop { position: 1.0; color: Qt.rgba(0.043, 0.047, 0.059, 0.9) }
        }
    }

    EmptyState {
        anchors.centerIn: parent
        visible: !page.music.active
        icon: "nowPlaying"
        title: qsTr("Nothing is playing")
        message: page.music.engineError || qsTr("Pick an album, artist or song to start listening. Music keeps playing while you browse videos and the TV guide.")
        actionText: qsTr("Browse music")
        onAction: page.app.navigate("music")
    }

    RowLayout {
        anchors { fill: parent; margins: (app ? app.pagePadding : 0); topMargin: Theme.s(36); bottomMargin: Theme.s(36) }
        visible: page.music.active
        spacing: Theme.s(48)

        // ---- artwork, metadata and transport ------------------------------------------------------------
        ColumnLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            spacing: Theme.s(18)

            Item { Layout.fillHeight: true }
            Artwork {
                id: art
                readonly property real edge: Math.min(Theme.s(520), page.height * 0.48, (page.width - (queuePanel.visible ? queuePanel.width : 0)) * 0.62)
                Layout.preferredWidth: edge
                Layout.preferredHeight: edge
                Layout.alignment: Qt.AlignHCenter
                source: page.cover
                name: page.track ? page.track.album : ""
                radius: Theme.radiusL
                layer.enabled: true
                layer.effect: MultiEffect { shadowEnabled: true; shadowColor: "#000000"; shadowOpacity: 0.7; shadowBlur: 1.0; shadowVerticalOffset: 24 }
            }
            ColumnLayout {
                Layout.alignment: Qt.AlignHCenter
                Layout.preferredWidth: Math.max(art.edge, Theme.s(420))
                Layout.topMargin: Theme.s(14)
                spacing: Theme.s(6)
                Text {
                    Layout.fillWidth: true
                    horizontalAlignment: Text.AlignHCenter
                    text: page.track ? page.track.title : ""
                    color: Theme.text
                    font.family: Theme.family
                    font.pixelSize: Theme.s(30)
                    font.weight: Font.Bold
                    font.letterSpacing: -0.6
                    elide: Text.ElideRight
                }
                Text {
                    Layout.fillWidth: true
                    horizontalAlignment: Text.AlignHCenter
                    text: page.track ? page.track.artist + "  ·  " + page.track.album : ""
                    color: albumArea.containsMouse ? Theme.text : Theme.text2
                    font.family: Theme.family
                    font.pixelSize: Theme.subheading
                    elide: Text.ElideRight
                    MouseArea {
                        id: albumArea
                        anchors.fill: parent
                        hoverEnabled: true
                        cursorShape: Qt.PointingHandCursor
                        onClicked: if (page.track && page.track.albumId) page.app.openAlbum(page.track.albumId)
                    }
                }
                SeekBar {
                    id: seek
                    Layout.fillWidth: true
                    Layout.topMargin: Theme.s(18)
                    value: page.music.duration > 0 ? page.music.position / page.music.duration : 0
                    interactive: page.music.duration > 0
                    trackHeight: Theme.s(5)
                    onSeek: fraction => page.music.seek(fraction * page.music.duration)
                    onStep: direction => page.music.seek(page.music.position + direction * 10)
                }
                RowLayout {
                    Layout.fillWidth: true
                    Text {
                        text: Theme.clock(seek.dragging ? seek.dragValue * page.music.duration : page.music.position)
                        color: Theme.text2
                        font.family: Theme.family
                        font.pixelSize: Theme.caption
                        font.features: { "tnum": 1 }
                    }
                    Item { Layout.fillWidth: true }
                    Text {
                        text: "−" + Theme.clock(Math.max(0, page.music.duration - page.music.position))
                        color: Theme.text2
                        font.family: Theme.family
                        font.pixelSize: Theme.caption
                        font.features: { "tnum": 1 }
                    }
                }
                Row {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.topMargin: Theme.s(8)
                    spacing: Theme.s(22)
                    IconButton {
                        anchors.verticalCenter: parent.verticalCenter
                        glyph: "shuffle"; checked: page.music.shuffle; tip: qsTr("Shuffle")
                        diameter: Theme.s(44); iconSize: Theme.s(20)
                        onClicked: page.music.command("shuffle")
                    }
                    IconButton {
                        anchors.verticalCenter: parent.verticalCenter
                        glyph: "previous"; tip: qsTr("Previous")
                        diameter: Theme.s(52); iconSize: Theme.s(26); iconColor: Theme.text
                        onClicked: page.music.command("previous")
                    }
                    IconButton {
                        anchors.verticalCenter: parent.verticalCenter
                        filled: true
                        glyph: page.music.playing ? "pause" : "play"
                        tip: page.music.playing ? qsTr("Pause") : qsTr("Play")
                        diameter: Theme.s(68); iconSize: Theme.s(30)
                        focus: true
                        onClicked: page.music.command("toggle")
                    }
                    IconButton {
                        anchors.verticalCenter: parent.verticalCenter
                        glyph: "next"; tip: qsTr("Next")
                        diameter: Theme.s(52); iconSize: Theme.s(26); iconColor: Theme.text
                        onClicked: page.music.command("next")
                    }
                    IconButton {
                        anchors.verticalCenter: parent.verticalCenter
                        glyph: page.music.repeat === "one" ? "repeatOne" : "repeat"
                        checked: page.music.repeat !== "off"
                        tip: page.music.repeat === "one" ? qsTr("Repeat one") : page.music.repeat === "all" ? qsTr("Repeat all") : qsTr("Repeat off")
                        diameter: Theme.s(44); iconSize: Theme.s(20)
                        onClicked: page.music.command("repeat")
                    }
                }
                RowLayout {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.preferredWidth: Theme.s(260)
                    Layout.topMargin: Theme.s(6)
                    spacing: Theme.s(10)
                    IconButton {
                        glyph: page.music.volume <= 0.001 ? "mute" : "volumeLow"
                        tip: qsTr("Mute")
                        diameter: Theme.s(32); iconSize: Theme.s(16)
                        onClicked: page.music.toggleMute()
                    }
                    SeekBar {
                        Layout.fillWidth: true
                        value: page.music.volume
                        fill: Theme.text2
                        onMoved: fraction => page.music.setVolume(fraction)
                        onSeek: fraction => page.music.setVolume(fraction)
                        onStep: direction => page.music.setVolume(Math.max(0, Math.min(1, page.music.volume + direction * 0.05)))
                    }
                    Icon { name: "volume"; size: Theme.s(16); color: Theme.text3 }
                }
            }
            Item { Layout.fillHeight: true }
        }

        // ---- queue ---------------------------------------------------------------------------------------
        Rectangle {
            id: queuePanel
            visible: page.width >= Theme.s(1040)
            Layout.preferredWidth: visible ? Math.max(Theme.s(340), page.width * 0.32) : 0
            Layout.fillHeight: true
            radius: Theme.radiusL
            color: Qt.rgba(0.08, 0.086, 0.105, 0.72)
            border.width: 1
            border.color: Theme.line

            ColumnLayout {
                anchors { fill: parent; margins: Theme.s(18) }
                spacing: Theme.s(10)
                RowLayout {
                    Layout.fillWidth: true
                    Layout.leftMargin: Theme.s(6)
                    Text {
                        text: qsTr("Up next")
                        color: Theme.text
                        font.family: Theme.family
                        font.pixelSize: Theme.heading
                        font.weight: Font.DemiBold
                    }
                    Item { Layout.fillWidth: true }
                    Text {
                        text: page.queueIndex >= 0 ? qsTr("%1 of %2").arg(page.queueIndex + 1).arg(queue.count) : ""
                        color: Theme.text3
                        font.family: Theme.family
                        font.pixelSize: Theme.label
                    }
                }
                ListView {
                    id: list
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    clip: true
                    model: queue
                    keyNavigationEnabled: true
                    currentIndex: -1
                    boundsBehavior: Flickable.StopAtBounds
                    T.ScrollBar.vertical: ThinScrollBar {}
                    delegate: TrackRow {
                        required property var model
                        required property int index
                        width: ListView.view.width
                        focus: ListView.isCurrentItem
                        showArt: true
                        editable: false
                        opacity: index < page.queueIndex ? 0.45 : 1
                        title: model.title
                        subtitle: model.artist
                        album: model.album
                        duration: model.duration
                        art: model.art ? Platform.fileUrl(model.art) : ""
                        current: index === page.queueIndex
                        playing: page.music.playing
                        onActivated: page.music.jump(index)
                    }
                }
            }
        }
    }
}
