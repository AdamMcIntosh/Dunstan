import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import Dustan

// Series or film detail: hero with the next episode, season picker and episode list.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    readonly property var stack: T.StackView.view
    property string showKey
    property var show: null
    property var seasons: []
    property int seasonIndex: -1
    readonly property var next: show ? show.nextUp : null
    readonly property bool isMovie: show ? show.isMovie : false
    readonly property bool resumable: next && next.position > 0 && !next.watched

    function load() {
        Bridge.call("video.show", { key: showKey }, (result, error) => {
            if (error) { app.toast(error, "error"); if (page.stack) page.stack.pop(); return }
            show = result.show
            seasons = result.seasons
            let index = seasonIndex
            if (index < 0 || index >= seasons.length) {
                index = 0
                for (let i = 0; i < seasons.length; ++i)
                    if (seasons[i].episodes.some(e => e.path === show.nextUp.path)) index = i
            }
            const first = seasonIndex < 0
            seasonIndex = index
            episodes.setItems(seasons.length ? seasons[seasonIndex].episodes : [])
            if (first) Qt.callLater(list.positionViewAtBeginning)
        })
    }
    function selectSeason(index) {
        seasonIndex = index
        episodes.setItems(seasons[index].episodes)
    }
    function setWatched(paths, watched) {
        Bridge.call("video.setWatched", { paths: paths, watched: watched }, (r, error) => { if (error) app.toast(error, "error") })
    }
    function episodeMenu(item, episode) {
        menu.episode = episode
        menu.items = [
            { text: episode.position > 0 && !episode.watched ? qsTr("Resume from %1").arg(Theme.clock(episode.position)) : qsTr("Play"), icon: "play", action: "play" },
            { text: qsTr("Play from the beginning"), icon: "refresh", action: "restart", enabled: episode.position > 0 || episode.watched },
            { separator: true },
            { text: episode.watched ? qsTr("Mark as unwatched") : qsTr("Mark as watched"), icon: "check", action: "watched" },
        ]
        menu.openAt(item, true)
    }

    Component.onCompleted: load()
    Connections { target: page.app; function onLibraryChanged(kind) { if (kind === "video") page.load() } }

    JsonListModel { id: episodes; roles: ["path", "name", "code", "season", "episode", "fileTitle", "duration", "position", "watched", "poster", "folder", "lastPlayed", "showKey", "showTitle", "isMovie", "collection"] }

    PopupMenu {
        id: menu
        property var episode
        onTriggered: (index, item) => {
            if (item.action === "play") page.app.playVideo(menu.episode, menu.episode.position > 0 && !menu.episode.watched)
            else if (item.action === "restart") page.app.playVideo(menu.episode, false)
            else if (item.action === "watched") page.setWatched([menu.episode.path], !menu.episode.watched)
        }
    }

    // Hero frame, full-bleed behind the header.
    Item {
        id: heroArt
        anchors { right: parent.right; top: parent.top }
        width: Math.min(parent.width * 0.68, Theme.s(1100))
        height: Math.round(width * 9 / 16)
        opacity: 1 - Math.min(1, Math.max(0, list.contentY - list.originY) / (height * 0.8))
        // Fade as one picture; per-child opacity would let the frame show through its own vignette.
        layer.enabled: opacity < 1
        VideoThumb {
            anchors.fill: parent
            radius: 0
            path: page.next ? page.next.path : ""
            poster: page.next && page.next.poster ? page.next.poster : ""
            duration: page.next ? page.next.duration : 0
            showProgress: false
            decodeWidth: 640
            decodeHeight: 360
        }
        Rectangle {
            anchors.fill: parent
            gradient: Gradient {
                orientation: Gradient.Horizontal
                GradientStop { position: 0.0; color: Theme.bg }
                GradientStop { position: 0.45; color: Qt.rgba(0.043, 0.047, 0.059, 0.55) }
                GradientStop { position: 1.0; color: Qt.rgba(0.043, 0.047, 0.059, 0.15) }
            }
        }
        Rectangle {
            anchors.fill: parent
            gradient: Gradient {
                GradientStop { position: 0.5; color: "transparent" }
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
        model: page.isMovie ? null : episodes
        clip: true
        focus: true
        keyNavigationEnabled: true
        currentIndex: -1
        cacheBuffer: Theme.s(1200)
        boundsBehavior: Flickable.StopAtBounds
        leftMargin: (app ? app.pagePadding : 0)
        rightMargin: (app ? app.pagePadding : 0)
        bottomMargin: Theme.s(40)
        spacing: Theme.s(4)
        T.ScrollBar.vertical: ThinScrollBar {}

        header: Column {
            width: ListView.view.width - (app ? app.pagePadding : 0) * 2
            spacing: Theme.s(14)
            Item { width: 1; height: Theme.s(4) }
            IconButton { glyph: "back"; tip: qsTr("Back"); onClicked: page.stack.pop() }
            Item { width: 1; height: Math.max(Theme.s(40), heroArt.height * 0.22) }
            Text {
                text: page.show ? page.show.collection.toUpperCase() : ""
                color: Theme.accent
                font.family: Theme.family
                font.pixelSize: Theme.overline
                font.weight: Font.DemiBold
                font.letterSpacing: 1.4
            }
            Text {
                width: Math.min(parent.width, Theme.s(760))
                text: page.show ? page.show.title : ""
                color: Theme.text
                font.family: Theme.family
                font.pixelSize: Theme.display + Theme.s(6)
                font.weight: Font.Bold
                font.letterSpacing: -1.2
                wrapMode: Text.Wrap
                maximumLineCount: 2
                elide: Text.ElideRight
            }
            Text {
                text: !page.show ? "" : page.isMovie
                      ? [Theme.duration(page.show.duration), page.show.watchedCount > 0 ? qsTr("Watched") : page.resumable ? qsTr("%1 left").arg(Theme.duration(page.show.duration - page.next.position)) : ""].filter(x => x).join("  ·  ")
                      : [page.show.seasonCount > 0 ? Theme.plural(page.show.seasonCount, qsTr("season")) : "",
                         Theme.plural(page.show.episodeCount, qsTr("episode")), Theme.duration(page.show.duration),
                         qsTr("%1 of %2 watched").arg(page.show.watchedCount).arg(page.show.episodeCount)].filter(x => x).join("  ·  ")
                color: Theme.text2
                font.family: Theme.family
                font.pixelSize: Theme.body
            }
            Text {
                visible: !page.isMovie && page.next !== null
                text: page.next ? (page.resumable ? qsTr("Continue  ") : qsTr("Up next  ")) + [page.next.code, page.next.name].filter(x => x).join("  ·  ") : ""
                color: Theme.text
                font.family: Theme.family
                font.pixelSize: Theme.subheading
                font.weight: Font.Medium
                width: Math.min(parent.width, Theme.s(760))
                elide: Text.ElideRight
            }
            Row {
                spacing: Theme.s(10)
                topPadding: Theme.s(6)
                PillButton {
                    kind: "primary"
                    glyph: "play"
                    focus: true
                    text: page.resumable ? qsTr("Resume  %1").arg(Theme.clock(page.next.position))
                                         : page.isMovie ? qsTr("Play") : page.next ? qsTr("Play  %1").arg(page.next.code || "") : qsTr("Play")
                    onClicked: page.app.playVideo(page.next, page.resumable)
                }
                PillButton {
                    visible: page.resumable
                    glyph: "refresh"
                    text: qsTr("Start over")
                    onClicked: page.app.playVideo(page.next, false)
                }
                PillButton {
                    kind: "ghost"
                    glyph: "check"
                    readonly property bool allWatched: page.show && page.show.watchedCount === page.show.episodeCount
                    text: allWatched ? qsTr("Mark unwatched") : qsTr("Mark watched")
                    onClicked: {
                        const paths = []
                        for (const s of page.seasons) for (const e of s.episodes) paths.push(e.path)
                        page.setWatched(paths, !allWatched)
                    }
                }
            }
            Text {
                visible: page.isMovie && page.next !== null
                topPadding: Theme.s(10)
                width: Math.min(parent.width, Theme.s(760))
                text: page.next ? page.next.folder + "  ·  " + Platform.fileName(page.next.path) : ""
                color: Theme.text3
                font.family: Theme.family
                font.pixelSize: Theme.caption
                elide: Text.ElideMiddle
            }

            // Season picker
            Item { width: 1; height: Theme.s(18); visible: !page.isMovie }
            ListView {
                id: seasonRow
                visible: !page.isMovie && page.seasons.length > 1
                width: parent.width
                height: Theme.s(38)
                orientation: ListView.Horizontal
                spacing: Theme.s(8)
                clip: true
                boundsBehavior: Flickable.StopAtBounds
                model: page.seasons
                delegate: PillButton {
                    required property var modelData
                    required property int index
                    compact: true
                    kind: index === page.seasonIndex ? "primary" : "secondary"
                    text: modelData.number > 0 ? qsTr("Season %1").arg(modelData.number) : modelData.title
                    onClicked: page.selectSeason(index)
                }
            }
            Text {
                visible: !page.isMovie && page.seasons.length > 0
                text: page.seasonIndex >= 0 && page.seasons.length ? page.seasons[page.seasonIndex].title + "  ·  " + Theme.plural(episodes.count, qsTr("episode")) : ""
                color: Theme.text3
                font.family: Theme.family
                font.pixelSize: Theme.label
                bottomPadding: Theme.s(4)
            }
        }

        delegate: FocusScope {
            id: row
            required property var model
            required property int index
            width: ListView.view.width - (app ? app.pagePadding : 0) * 2
            height: Theme.s(124)
            focus: ListView.isCurrentItem
            readonly property bool resumable: model.position > 0 && !model.watched
            readonly property bool isNext: page.next && page.next.path === model.path
            Keys.onReturnPressed: page.app.playVideo(episodes.get(index), resumable)
            Keys.onEnterPressed: page.app.playVideo(episodes.get(index), resumable)

            Rectangle {
                anchors.fill: parent
                radius: Theme.radiusM
                color: area.pressed ? Theme.pressed : area.containsMouse || more.hovered ? Theme.hover : row.isNext ? Qt.rgba(1, 1, 1, 0.025) : "transparent"
                border.width: row.activeFocus ? Theme.s(2) : 0
                border.color: Theme.focus
                Behavior on color { ColorAnimation { duration: Theme.fast } }
            }
            MouseArea {
                id: area
                anchors.fill: parent
                hoverEnabled: true
                cursorShape: Qt.PointingHandCursor
                onClicked: page.app.playVideo(episodes.get(row.index), row.resumable)
            }
            RowLayout {
                anchors { fill: parent; margins: Theme.s(10) }
                spacing: Theme.s(20)
                VideoThumb {
                    Layout.preferredWidth: Math.round(Layout.preferredHeight * 16 / 9)
                    Layout.preferredHeight: parent.height
                    path: row.model.path
                    poster: row.model.poster || ""
                    duration: row.model.duration
                    progress: row.model.duration > 0 ? row.model.position / row.model.duration : 0
                    watched: row.model.watched
                    radius: Theme.radiusS
                    Rectangle {
                        anchors.centerIn: parent
                        width: Theme.s(40); height: width; radius: width / 2
                        color: Qt.rgba(0, 0, 0, 0.55)
                        opacity: area.containsMouse ? 1 : 0
                        Behavior on opacity { NumberAnimation { duration: Theme.fast } }
                        Icon { anchors.centerIn: parent; anchors.horizontalCenterOffset: Theme.s(1); name: "play"; size: Theme.s(18) }
                    }
                }
                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: Theme.s(5)
                    Text {
                        visible: text.length > 0
                        text: row.model.code + (row.isNext ? (row.resumable ? qsTr("  ·  CONTINUE") : qsTr("  ·  UP NEXT")) : "")
                        color: row.isNext ? Theme.accent : Theme.text3
                        font.family: Theme.family
                        font.pixelSize: Theme.overline
                        font.weight: Font.DemiBold
                        font.letterSpacing: 1.0
                    }
                    Text {
                        Layout.fillWidth: true
                        text: row.model.name
                        color: Theme.text
                        font.family: Theme.family
                        font.pixelSize: Theme.subheading
                        font.weight: Font.DemiBold
                        elide: Text.ElideRight
                    }
                    Text {
                        Layout.fillWidth: true
                        text: [row.model.duration > 0 ? Theme.duration(row.model.duration) : qsTr("Runtime unknown"),
                               row.model.watched ? qsTr("Watched") : row.resumable ? qsTr("%1 left").arg(Theme.duration(row.model.duration - row.model.position)) : ""].filter(x => x).join("  ·  ")
                        color: Theme.text3
                        font.family: Theme.family
                        font.pixelSize: Theme.label
                    }
                }
                IconButton {
                    id: more
                    glyph: "more"
                    tip: qsTr("More")
                    focusPolicy: Qt.NoFocus
                    onClicked: page.episodeMenu(this, episodes.get(row.index))
                }
            }
        }
    }
}
