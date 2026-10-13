import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import Dustan

// Every song in the library, virtualised for large collections.
FocusScope {
    id: view
    readonly property var app: T.ApplicationWindow.window
    property string search
    property bool active: true
    property bool loaded: false

    function reload() { if (Bridge.ready) { Bridge.load(tracks, "music.tracks", { search: search }); loaded = true } }
    onSearchChanged: if (active || loaded) reload()
    onActiveChanged: if (active && !loaded) reload()
    Component.onCompleted: if (active) reload()
    Connections { target: Bridge; function onReadyChanged() { if (view.active) view.reload() } }
    Connections { target: view.app; function onLibraryChanged(kind) { if (kind === "music" && view.loaded) view.reload() } }

    JsonListModel { id: tracks; roles: ["id", "title", "artist", "album", "albumArtist", "albumId", "artistId", "trackNumber", "discNumber", "year", "genre", "duration", "file", "art"] }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0
        RowLayout {
            Layout.fillWidth: true
            Layout.leftMargin: Theme.s(10)
            Layout.rightMargin: Theme.s(10)
            Layout.bottomMargin: Theme.s(6)
            spacing: Theme.s(14)
            component Heading: Text {
                color: Theme.text3
                font.family: Theme.family
                font.pixelSize: Theme.overline
                font.weight: Font.DemiBold
                font.letterSpacing: 1.1
            }
            Item { Layout.preferredWidth: Theme.s(28) }
            Item { Layout.preferredWidth: Theme.s(40) }
            Heading { text: qsTr("TITLE"); Layout.fillWidth: true; Layout.preferredWidth: 3 }
            Heading { text: qsTr("ALBUM"); Layout.fillWidth: true; Layout.preferredWidth: 2; visible: list.width > Theme.s(720) }
            Item { Layout.preferredWidth: Theme.s(30) }
            Heading { text: qsTr("TIME"); Layout.preferredWidth: Theme.s(52); horizontalAlignment: Text.AlignRight }
        }
        Rectangle { Layout.fillWidth: true; Layout.preferredHeight: 1; color: Theme.line }
        ListView {
            id: list
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            model: tracks
            focus: true
            keyNavigationEnabled: true
            currentIndex: -1
            cacheBuffer: Theme.s(1200)
            boundsBehavior: Flickable.StopAtBounds
            topMargin: Theme.s(6)
            bottomMargin: Theme.s(24)
            T.ScrollBar.vertical: ThinScrollBar {}
            delegate: TrackRow {
                required property var model
                required property int index
                width: ListView.view.width
                focus: ListView.isCurrentItem
                showArt: true
                showAlbum: true
                title: model.title
                subtitle: model.artist
                album: model.album
                duration: model.duration
                art: model.art ? Platform.fileUrl(model.art) : ""
                current: view.app.music.track !== null && view.app.music.track.id === model.id
                playing: view.app.music.playing
                onActivated: view.app.playMusic({ source: "tracks", search: view.search, trackId: model.id, shuffle: false })
                onEdit: view.app.editTrack(tracks.get(index))
                onAlbumClicked: if (model.albumId) view.app.openAlbum(model.albumId)
            }
        }
    }

    Spinner { anchors.centerIn: parent; visible: tracks.loading && tracks.count === 0 }
    EmptyState {
        anchors.centerIn: parent
        visible: !tracks.loading && view.loaded && tracks.count === 0
        icon: view.search ? "search" : "music"
        title: view.search ? qsTr("No songs match “%1”").arg(view.search) : qsTr("No songs yet")
        message: view.search ? qsTr("Search looks at titles, artists and albums.") : qsTr("Songs appear after your music folder is scanned.")
    }
}
