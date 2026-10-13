import QtQuick
import QtQuick.Controls.Basic as T
import Dustan

// Responsive album artwork grid.
FocusScope {
    id: view
    readonly property var app: T.ApplicationWindow.window
    property string search
    property string sort: "artist"
    property bool active: true
    property bool loaded: false

    function reload() { if (Bridge.ready) { Bridge.load(albums, "music.albums", { search: search, sort: sort }); loaded = true } }
    onSearchChanged: reload()
    onSortChanged: reload()
    onActiveChanged: if (active && !loaded) reload()
    Component.onCompleted: if (active) reload()
    Connections { target: Bridge; function onReadyChanged() { if (view.active) view.reload() } }
    Connections { target: view.app; function onLibraryChanged(kind) { if (kind === "music") view.reload() } }

    JsonListModel { id: albums; roles: ["id", "name", "artist", "year", "trackCount", "art"] }

    readonly property int gap: Theme.s(24)
    readonly property int minCell: Theme.s(172)

    GridView {
        id: grid
        anchors { fill: parent; rightMargin: -view.gap }
        clip: true
        model: albums
        focus: true
        keyNavigationEnabled: true
        cacheBuffer: Theme.s(900)
        boundsBehavior: Flickable.StopAtBounds
        readonly property int columns: Math.max(2, Math.floor(width / (view.minCell + view.gap)))
        cellWidth: Math.floor(width / columns)
        cellHeight: cellWidth - view.gap + Theme.s(64) + view.gap
        bottomMargin: Theme.s(24)
        T.ScrollBar.vertical: ThinScrollBar {}
        currentIndex: -1
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
                art: cell.model.art ? Platform.fileUrl(cell.model.art) : ""
                title: cell.model.name
                subtitle: cell.model.artist + (cell.model.year > 0 ? "  ·  " + cell.model.year : "")
                onOpen: view.app.openAlbum(cell.model.id)
                onPlay: view.app.playMusic({ source: "album", id: cell.model.id })
            }
        }
    }

    Spinner { anchors.centerIn: parent; visible: albums.loading && albums.count === 0 }
    EmptyState {
        anchors.centerIn: parent
        visible: !albums.loading && view.loaded && albums.count === 0
        icon: view.search ? "search" : "album"
        title: view.search ? qsTr("No albums match “%1”").arg(view.search) : qsTr("Your music library is empty")
        message: view.search ? qsTr("Try an artist name or part of the album title.") : qsTr("Choose your NAS music folder in Settings and scan it. Files stay on the NAS; Dustan keeps a local index.")
        actionText: view.search ? "" : qsTr("Open Settings")
        onAction: view.app.navigate("settings")
    }
}
