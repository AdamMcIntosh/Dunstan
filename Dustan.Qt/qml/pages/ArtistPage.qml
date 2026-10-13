import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Effects
import QtQuick.Layouts
import Dustan

// Artist detail: discography row and every song by the artist.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    readonly property var stack: T.StackView.view
    property var artistId
    property var artist: null
    property real total: 0

    function load() {
        Bridge.load(tracks, "music.artist", { id: artistId }, "tracks", (result, error) => {
            if (error) { app.toast(error, "error"); if (page.stack) page.stack.pop(); return }
            artist = result.artist
            total = result.duration
            albums.setItems(result.albums)
            Qt.callLater(list.positionViewAtBeginning)
        })
    }
    Component.onCompleted: load()
    Connections { target: page.app; function onLibraryChanged(kind) { if (kind === "music") page.load() } }

    JsonListModel { id: tracks; roles: ["id", "title", "artist", "album", "albumArtist", "albumId", "artistId", "trackNumber", "discNumber", "year", "genre", "duration", "file", "art"] }
    JsonListModel { id: albums; roles: ["id", "name", "artist", "year", "trackCount", "art"] }

    ListView {
        id: list
        anchors.fill: parent
        // The header grows as artwork and details load; stay at the top until the user scrolls.
        property bool userScrolled: false
        onMovementStarted: userScrolled = true
        onOriginYChanged: if (!userScrolled) Qt.callLater(positionViewAtBeginning)
        model: tracks
        clip: true
        focus: true
        keyNavigationEnabled: true
        currentIndex: -1
        cacheBuffer: Theme.s(1000)
        boundsBehavior: Flickable.StopAtBounds
        leftMargin: (app ? app.pagePadding : 0)
        rightMargin: (app ? app.pagePadding : 0)
        bottomMargin: Theme.s(32)
        T.ScrollBar.vertical: ThinScrollBar {}

        header: Column {
            width: ListView.view.width - (app ? app.pagePadding : 0) * 2
            spacing: Theme.s(16)
            Item { width: 1; height: Theme.s(4) }
            IconButton { glyph: "back"; tip: qsTr("Back"); onClicked: page.stack.pop() }
            RowLayout {
                width: parent.width
                spacing: Theme.s(32)
                Artwork {
                    Layout.preferredWidth: Theme.s(176)
                    Layout.preferredHeight: Theme.s(176)
                    circle: true
                    source: page.artist && page.artist.art ? Platform.fileUrl(page.artist.art) : ""
                    name: page.artist ? page.artist.name : ""
                    layer.enabled: true
                    layer.effect: MultiEffect { shadowEnabled: true; shadowColor: "#000000"; shadowOpacity: 0.55; shadowBlur: 1.0; shadowVerticalOffset: 12 }
                }
                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: Theme.s(10)
                    Text {
                        text: qsTr("ARTIST")
                        color: Theme.text2
                        font.family: Theme.family
                        font.pixelSize: Theme.overline
                        font.weight: Font.DemiBold
                        font.letterSpacing: 1.4
                    }
                    Text {
                        Layout.fillWidth: true
                        text: page.artist ? page.artist.name : ""
                        color: Theme.text
                        font.family: Theme.family
                        font.pixelSize: Theme.display
                        font.weight: Font.Bold
                        font.letterSpacing: -1
                        elide: Text.ElideRight
                    }
                    Text {
                        text: page.artist ? [Theme.plural(albums.count, qsTr("album")), Theme.plural(tracks.count, qsTr("song")), Theme.duration(page.total)].join("  ·  ") : ""
                        color: Theme.text2
                        font.family: Theme.family
                        font.pixelSize: Theme.label
                    }
                    Row {
                        Layout.topMargin: Theme.s(8)
                        spacing: Theme.s(10)
                        PillButton { kind: "primary"; glyph: "play"; text: qsTr("Play"); onClicked: page.app.playMusic({ source: "artist", id: page.artistId, shuffle: false }) }
                        PillButton { glyph: "shuffle"; text: qsTr("Shuffle"); onClicked: page.app.playMusic({ source: "artist", id: page.artistId, shuffle: true }) }
                    }
                }
            }
            SectionTitle {
                width: parent.width
                visible: albums.count > 0
                text: qsTr("Albums")
                detail: albums.count
            }
            ListView {
                id: albumRow
                width: parent.width
                height: visible ? Theme.s(176) + Theme.s(64) : 0
                visible: albums.count > 0
                orientation: ListView.Horizontal
                spacing: Theme.s(22)
                clip: true
                model: albums
                boundsBehavior: Flickable.StopAtBounds
                T.ScrollBar.horizontal: ThinScrollBar { orientation: Qt.Horizontal }
                delegate: AlbumCard {
                    required property var model
                    width: Theme.s(176)
                    height: albumRow.height
                    art: model.art ? Platform.fileUrl(model.art) : ""
                    title: model.name
                    subtitle: model.year > 0 ? String(model.year) : Theme.plural(model.trackCount, qsTr("song"))
                    onOpen: page.app.openAlbum(model.id)
                    onPlay: page.app.playMusic({ source: "album", id: model.id })
                }
            }
            SectionTitle {
                width: parent.width
                text: qsTr("Songs")
                detail: tracks.count
            }
        }

        delegate: TrackRow {
            required property var model
            required property int index
            width: ListView.view.width - (app ? app.pagePadding : 0) * 2
            focus: ListView.isCurrentItem
            showArt: true
            showAlbum: true
            title: model.title
            subtitle: model.artist
            album: model.album
            duration: model.duration
            art: model.art ? Platform.fileUrl(model.art) : ""
            current: page.app.music.track !== null && page.app.music.track.id === model.id
            playing: page.app.music.playing
            onActivated: page.app.playMusic({ source: "artist", id: page.artistId, trackId: model.id, shuffle: false })
            onEdit: page.app.editTrack(tracks.get(index))
            onAlbumClicked: if (model.albumId) page.app.openAlbum(model.albumId)
        }
    }
}
