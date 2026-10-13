import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import Dustan

// Video library home: continue watching, then series and films.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    property string search: ""
    property int filterIndex: 0
    readonly property var filters: ["all", "shows", "movies", "progress", "unwatched"]
    property var counts: ({})
    property string root: ""
    property string loadError: ""
    property bool loaded: false

    function reload() {
        if (!Bridge.ready) return
        Bridge.load(titles, "video.overview", { search: search, filter: filters[filterIndex] }, "titles", (result, error) => {
            if (error) { app.toast(error, "error"); return }
            continueModel.setItems(result.continueWatching)
            counts = result.counts
            root = result.root
            loadError = result.loadError || ""
            series.setItems(titlesOf(false))
            films.setItems(titlesOf(true))
            loaded = true
        })
    }
    function titlesOf(movies) {
        const out = []
        for (let i = 0; i < titles.count; ++i) {
            const t = titles.get(i)
            if (t.isMovie === movies) out.push(t)
        }
        return out
    }
    function play(item) { app.playVideo(item, item.position > 0 && !item.watched) }

    onSearchChanged: reload()
    onFilterIndexChanged: reload()
    onVisibleChanged: if (visible && loaded) reload()
    Component.onCompleted: reload()
    Connections { target: Bridge; function onReadyChanged() { page.reload() } }
    Connections { target: page.app; function onLibraryChanged(kind) { if (kind === "video") page.reload() } }

    readonly property var titleRoles: ["key", "title", "collection", "isMovie", "episodeCount", "seasonCount", "watchedCount", "duration", "poster", "thumbPath", "lastPlayed", "nextUp"]
    JsonListModel { id: titles; roles: page.titleRoles }
    JsonListModel { id: series; roles: page.titleRoles }
    JsonListModel { id: films; roles: page.titleRoles }
    JsonListModel { id: continueModel; roles: ["path", "name", "code", "season", "episode", "fileTitle", "duration", "position", "watched", "poster", "folder", "lastPlayed", "showKey", "showTitle", "isMovie", "collection"] }

    readonly property int gap: Theme.s(22)
    readonly property int minCard: Theme.s(244)

    function ensureVisible(item) {
        if (!item) return
        const p = item.mapToItem(flick.contentItem, 0, 0)
        if (p.y < flick.contentY + Theme.s(20)) flick.contentY = Math.max(0, p.y - Theme.s(80))
        else if (p.y + item.height > flick.contentY + flick.height - Theme.s(20)) flick.contentY = Math.min(flick.contentHeight - flick.height, p.y + item.height - flick.height + Theme.s(80))
    }

    component TitleGrid: GridView {
        id: grid
        property string heading
        width: parent.width + page.gap
        height: contentHeight
        interactive: false
        keyNavigationEnabled: true
        currentIndex: -1
        readonly property int columns: Math.max(2, Math.floor(width / (page.minCard + page.gap)))
        cellWidth: Math.floor(width / columns)
        cellHeight: Math.round((cellWidth - page.gap) * 9 / 16) + Theme.s(62) + page.gap
        onCurrentItemChanged: page.ensureVisible(currentItem)
        Keys.onPressed: event => { if (currentIndex < 0 && count > 0 && [Qt.Key_Down, Qt.Key_Right].includes(event.key)) { currentIndex = 0; event.accepted = true } }
        delegate: FocusScope {
            id: cell
            required property var model
            required property int index
            width: grid.cellWidth
            height: grid.cellHeight
            VideoCard {
                width: parent.width - page.gap
                height: parent.height - page.gap
                focus: cell.GridView.isCurrentItem
                path: cell.model.thumbPath
                poster: cell.model.poster || ""
                duration: cell.model.isMovie ? cell.model.duration : (cell.model.nextUp ? cell.model.nextUp.duration : 0)
                progress: cell.model.isMovie && cell.model.nextUp && cell.model.duration > 0 ? cell.model.nextUp.position / cell.model.duration : 0
                watched: cell.model.watchedCount === cell.model.episodeCount && cell.model.episodeCount > 0
                title: cell.model.title
                subtitle: cell.model.isMovie
                          ? [Theme.duration(cell.model.duration), cell.model.collection].filter(x => x).join("  ·  ")
                          : [cell.model.seasonCount > 0 ? Theme.plural(cell.model.seasonCount, qsTr("season")) : "",
                             Theme.plural(cell.model.episodeCount, qsTr("episode")),
                             cell.model.watchedCount > 0 && cell.model.watchedCount < cell.model.episodeCount ? qsTr("%1 watched").arg(cell.model.watchedCount) : ""].filter(x => x).join("  ·  ")
                badge: cell.model.collection.indexOf("·") > 0 ? cell.model.collection.split("·").pop().trim().toUpperCase() : ""
                onOpen: page.app.openShow(cell.model.key)
                onPlay: page.play(cell.model.nextUp)
            }
        }
    }

    Flickable {
        id: flick
        anchors.fill: parent
        contentHeight: column.implicitHeight + Theme.s(40)
        clip: true
        boundsBehavior: Flickable.StopAtBounds
        T.ScrollBar.vertical: ThinScrollBar {}

        Column {
            id: column
            x: (app ? app.pagePadding : 0)
            width: flick.width - (app ? app.pagePadding : 0) * 2
            spacing: Theme.s(18)
            topPadding: Theme.s(12)

            PageHeader {
                width: parent.width
                title: qsTr("Videos")
                subtitle: page.counts.videos !== undefined
                          ? qsTr("%1 series  ·  %2 films  ·  %3 videos").arg(page.counts.shows).arg(page.counts.movies).arg(Number(page.counts.videos).toLocaleString(Qt.locale(), "f", 0))
                          : ""
                SearchField {
                    width: Math.min(Theme.s(320), page.width * 0.3)
                    placeholder: qsTr("Search titles and episodes")
                    onSearch: text => page.search = text
                }
            }
            Segmented {
                options: [qsTr("All"), qsTr("Series"), qsTr("Films"), qsTr("In progress"), qsTr("Unwatched")]
                currentIndex: page.filterIndex
                onActivated: index => page.filterIndex = index
            }

            // Continue watching
            Column {
                width: parent.width
                spacing: Theme.s(14)
                visible: continueModel.count > 0 && page.search.length === 0 && page.filterIndex === 0
                Item { width: 1; height: Theme.s(6) }
                SectionTitle { width: parent.width; text: qsTr("Continue watching") }
                ListView {
                    id: continueRow
                    width: parent.width
                    height: Math.round(Theme.s(300) * 9 / 16) + Theme.s(66)
                    orientation: ListView.Horizontal
                    spacing: page.gap
                    clip: true
                    model: continueModel
                    keyNavigationEnabled: true
                    currentIndex: -1
                    boundsBehavior: Flickable.StopAtBounds
                    T.ScrollBar.horizontal: ThinScrollBar { orientation: Qt.Horizontal }
                    delegate: VideoCard {
                        required property var model
                        required property int index
                        width: Theme.s(300)
                        height: continueRow.height
                        focus: ListView.isCurrentItem
                        path: model.path
                        poster: model.poster || ""
                        duration: model.duration
                        progress: model.duration > 0 ? model.position / model.duration : 0
                        title: model.isMovie ? model.showTitle : model.showTitle
                        subtitle: model.isMovie
                                  ? qsTr("%1 left").arg(Theme.duration(model.duration - model.position))
                                  : [model.code, model.name].filter(x => x).join("  ·  ")
                        onOpen: page.play(continueModel.get(index))
                        onPlay: page.play(continueModel.get(index))
                    }
                }
            }

            Column {
                width: parent.width
                spacing: Theme.s(14)
                visible: series.count > 0
                Item { width: 1; height: Theme.s(6) }
                SectionTitle { width: parent.width; text: qsTr("Series"); detail: series.count }
                TitleGrid { model: series }
            }
            Column {
                width: parent.width
                spacing: Theme.s(14)
                visible: films.count > 0
                Item { width: 1; height: Theme.s(6) }
                SectionTitle { width: parent.width; text: qsTr("Films"); detail: films.count }
                TitleGrid { model: films }
            }

            EmptyState {
                anchors.horizontalCenter: parent.horizontalCenter
                visible: page.loaded && titles.count === 0
                icon: page.loadError ? "warning" : page.search || page.filterIndex > 0 ? "search" : "videos"
                title: page.loadError ? qsTr("The video catalog couldn't be loaded")
                     : page.search ? qsTr("Nothing matches “%1”").arg(page.search)
                     : page.filterIndex > 0 ? qsTr("Nothing here yet")
                     : qsTr("Your video library is empty")
                message: page.loadError ? page.loadError
                       : page.search || page.filterIndex > 0 ? qsTr("Try another filter or search.")
                       : qsTr("Choose your NAS video folder in Settings. Dustan reads runtimes with ffprobe in a separate process, so a damaged file can't take the app down.")
                actionText: !page.search && page.filterIndex === 0 && !page.loadError ? qsTr("Open Settings") : ""
                onAction: page.app.navigate("settings")
            }
        }
    }
    Spinner { anchors.centerIn: parent; visible: !page.loaded && titles.loading }
}
