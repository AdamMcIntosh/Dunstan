import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Effects
import QtQuick.Layouts
import Dustan

// Album detail: hero with large artwork, then the track list grouped by disc.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    readonly property var stack: T.StackView.view
    property var albumId
    property var album: null
    property real total: 0
    property string genre
    property int discs: 1
    property url cover

    function load() {
        Bridge.load(tracks, "music.album", { id: albumId }, "tracks", (result, error) => {
            if (error) { app.toast(error, "error"); if (page.stack) page.stack.pop(); return }
            album = result.album
            total = result.duration
            genre = result.genre
            discs = result.discs
            Qt.callLater(list.positionViewAtBeginning)
            if (!cover.toString() && album.art) cover = Platform.fileUrl(album.art)
            Bridge.call("music.cover", { id: albumId }, path => { if (path) page.cover = Platform.fileUrl(path) })
        })
    }
    Component.onCompleted: load()
    Connections { target: page.app; function onLibraryChanged(kind) { if (kind === "music") page.load() } }

    JsonListModel { id: tracks; roles: ["id", "title", "artist", "album", "albumArtist", "albumId", "artistId", "trackNumber", "discNumber", "year", "genre", "duration", "file", "art"] }

    // Ambient wash from the cover.
    Item {
        anchors { left: parent.left; right: parent.right; top: parent.top }
        height: Theme.s(520)
        opacity: 0.55
        Image {
            id: wash
            anchors.fill: parent
            source: page.cover
            sourceSize: Qt.size(96, 96)
            fillMode: Image.PreserveAspectCrop
            visible: false
        }
        MultiEffect {
            anchors.fill: parent
            source: wash
            blurEnabled: true
            blur: 1.0
            blurMax: 64
            saturation: -0.1
            brightness: -0.25
            visible: wash.status === Image.Ready
        }
        Rectangle {
            anchors.fill: parent
            gradient: Gradient {
                GradientStop { position: 0.0; color: Qt.rgba(0.043, 0.047, 0.059, 0.35) }
                GradientStop { position: 1.0; color: Theme.bg }
            }
        }
    }

    ListView {
        id: list
        anchors.fill: parent
        // The header grows as artwork and details load; stay at the top until the user scrolls.
        property bool userScrolled: false
        onMovementStarted: userScrolled = true
        onOriginYChanged: if (!userScrolled) Qt.callLater(positionViewAtBeginning)
        model: tracks
        focus: true
        keyNavigationEnabled: true
        currentIndex: -1
        clip: true
        boundsBehavior: Flickable.StopAtBounds
        bottomMargin: Theme.s(32)
        leftMargin: (app ? app.pagePadding : 0)
        rightMargin: (app ? app.pagePadding : 0)
        T.ScrollBar.vertical: ThinScrollBar {}
        section.property: page.discs > 1 ? "discNumber" : ""
        section.delegate: Text {
            required property string section
            width: ListView.view.width - (app ? app.pagePadding : 0) * 2
            topPadding: Theme.s(20); bottomPadding: Theme.s(8); leftPadding: Theme.s(10)
            text: qsTr("DISC %1").arg(section)
            color: Theme.text3
            font.family: Theme.family
            font.pixelSize: Theme.overline
            font.weight: Font.DemiBold
            font.letterSpacing: 1.2
        }

        header: Item {
            width: ListView.view.width - (app ? app.pagePadding : 0) * 2
            height: hero.y + hero.implicitHeight + Theme.s(28)
            IconButton {
                id: back
                y: Theme.s(20)
                glyph: "back"
                tip: qsTr("Back")
                onClicked: page.stack.pop()
            }
            RowLayout {
                id: hero
                anchors { left: parent.left; right: parent.right; top: back.bottom; topMargin: Theme.s(16) }
                spacing: Theme.s(36)
                Artwork {
                    Layout.preferredWidth: Math.min(Theme.s(248), page.width * 0.24)
                    Layout.preferredHeight: Layout.preferredWidth
                    source: page.cover
                    name: page.album ? page.album.name : ""
                    radius: Theme.radiusM
                    layer.enabled: true
                    layer.effect: MultiEffect { shadowEnabled: true; shadowColor: "#000000"; shadowOpacity: 0.6; shadowBlur: 1.0; shadowVerticalOffset: 14 }
                }
                ColumnLayout {
                    Layout.fillWidth: true
                    Layout.alignment: Qt.AlignBottom
                    spacing: Theme.s(10)
                    Text {
                        text: qsTr("ALBUM")
                        color: Theme.text2
                        font.family: Theme.family
                        font.pixelSize: Theme.overline
                        font.weight: Font.DemiBold
                        font.letterSpacing: 1.4
                    }
                    Text {
                        Layout.fillWidth: true
                        text: page.album ? page.album.name : ""
                        color: Theme.text
                        font.family: Theme.family
                        font.pixelSize: Theme.display
                        font.weight: Font.Bold
                        font.letterSpacing: -1
                        wrapMode: Text.Wrap
                        maximumLineCount: 2
                        elide: Text.ElideRight
                    }
                    Text {
                        text: page.album ? page.album.artist : ""
                        color: artistArea.containsMouse ? Theme.accentHover : Theme.accent
                        font.family: Theme.family
                        font.pixelSize: Theme.subheading
                        font.weight: Font.DemiBold
                        MouseArea {
                            id: artistArea
                            anchors.fill: parent
                            hoverEnabled: true
                            cursorShape: Qt.PointingHandCursor
                            onClicked: {
                                const first = tracks.count > 0 ? tracks.get(0) : null
                                if (first && first.artistId) page.app.openArtist(first.artistId)
                            }
                        }
                    }
                    Text {
                        Layout.fillWidth: true
                        text: [page.album && page.album.year > 0 ? page.album.year : "", page.genre,
                               Theme.plural(tracks.count, qsTr("song")), Theme.duration(page.total)].filter(x => x !== "" && x !== undefined).join("  ·  ")
                        color: Theme.text2
                        font.family: Theme.family
                        font.pixelSize: Theme.label
                        elide: Text.ElideRight
                    }
                    Row {
                        Layout.topMargin: Theme.s(10)
                        spacing: Theme.s(10)
                        PillButton { kind: "primary"; glyph: "play"; text: qsTr("Play"); onClicked: page.app.playMusic({ source: "album", id: page.albumId, shuffle: false }) }
                        PillButton { glyph: "shuffle"; text: qsTr("Shuffle"); onClicked: page.app.playMusic({ source: "album", id: page.albumId, shuffle: true }) }
                        IconButton {
                            glyph: "edit"; tip: qsTr("Edit album tags"); diameter: Theme.s(40)
                            onClicked: if (page.album) page.app.editAlbum(page.album)
                        }
                    }
                }
            }
        }

        delegate: TrackRow {
            required property var model
            required property int index
            width: ListView.view.width - (app ? app.pagePadding : 0) * 2
            focus: ListView.isCurrentItem
            number: model.trackNumber
            title: model.title
            subtitle: model.artist !== model.albumArtist ? model.artist : ""
            album: model.album
            duration: model.duration
            current: page.app.music.track !== null && page.app.music.track.id === model.id
            playing: page.app.music.playing
            onActivated: page.app.playMusic({ source: "album", id: page.albumId, trackId: model.id, shuffle: false })
            onEdit: page.app.editTrack(tracks.get(index))
        }
    }
}
