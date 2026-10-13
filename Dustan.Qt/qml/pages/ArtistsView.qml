import QtQuick
import QtQuick.Controls.Basic as T
import Dustan

// Artist grid with round portraits taken from album art.
FocusScope {
    id: view
    readonly property var app: T.ApplicationWindow.window
    property string search
    property bool active: true
    property bool loaded: false

    function reload() { if (Bridge.ready) { Bridge.load(artists, "music.artists", { search: search }); loaded = true } }
    onSearchChanged: if (active || loaded) reload()
    onActiveChanged: if (active && !loaded) reload()
    Component.onCompleted: if (active) reload()
    Connections { target: Bridge; function onReadyChanged() { if (view.active) view.reload() } }
    Connections { target: view.app; function onLibraryChanged(kind) { if (kind === "music" && view.loaded) view.reload() } }

    JsonListModel { id: artists; roles: ["id", "name", "albumCount", "trackCount", "art"] }

    readonly property int gap: Theme.s(28)
    readonly property int minCell: Theme.s(156)

    GridView {
        id: grid
        anchors { fill: parent; rightMargin: -view.gap }
        clip: true
        model: artists
        focus: true
        keyNavigationEnabled: true
        cacheBuffer: Theme.s(900)
        boundsBehavior: Flickable.StopAtBounds
        readonly property int columns: Math.max(2, Math.floor(width / (view.minCell + view.gap)))
        cellWidth: Math.floor(width / columns)
        cellHeight: cellWidth - view.gap + Theme.s(60) + view.gap
        bottomMargin: Theme.s(24)
        currentIndex: -1
        T.ScrollBar.vertical: ThinScrollBar {}
        Keys.onPressed: event => { if (currentIndex < 0 && count > 0 && [Qt.Key_Down, Qt.Key_Right].includes(event.key)) { currentIndex = 0; event.accepted = true } }

        delegate: FocusScope {
            id: cell
            required property var model
            required property int index
            width: grid.cellWidth
            height: grid.cellHeight
            AlbumCard {
                width: parent.width - view.gap
                height: parent.height - view.gap
                focus: cell.GridView.isCurrentItem
                circle: true
                art: cell.model.art ? Platform.fileUrl(cell.model.art) : ""
                title: cell.model.name
                subtitle: qsTr("%1  ·  %2").arg(Theme.plural(cell.model.albumCount, qsTr("album"))).arg(Theme.plural(cell.model.trackCount, qsTr("song")))
                onOpen: view.app.openArtist(cell.model.id)
                onPlay: view.app.playMusic({ source: "artist", id: cell.model.id })
            }
        }
    }

    Spinner { anchors.centerIn: parent; visible: artists.loading && artists.count === 0 }
    EmptyState {
        anchors.centerIn: parent
        visible: !artists.loading && view.loaded && artists.count === 0
        icon: view.search ? "search" : "artist"
        title: view.search ? qsTr("No artists match “%1”").arg(view.search) : qsTr("No artists yet")
        message: view.search ? "" : qsTr("Artists appear after your music folder is scanned.")
    }
}
