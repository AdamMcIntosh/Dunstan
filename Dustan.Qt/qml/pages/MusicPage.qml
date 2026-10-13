import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import Dustan

// Music library: albums, artists and songs.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    readonly property var counts: app.info.counts || ({})
    property string search: ""
    property int tab: app.prefs.musicTab
    readonly property var sorts: [
        { key: "artist", text: qsTr("Artist") },
        { key: "title", text: qsTr("Title") },
        { key: "year", text: qsTr("Year") },
        { key: "recent", text: qsTr("Recently added") },
    ]

    function setTab(index) { app.prefs.musicTab = index; tab = index }
    function sortLabel(key) {
        const match = sorts.find(s => s.key === key)
        return match ? match.text : sorts[0].text
    }

    ColumnLayout {
        anchors { fill: parent; leftMargin: (app ? app.pagePadding : 0); rightMargin: (app ? app.pagePadding : 0); topMargin: Theme.s(12) }
        spacing: Theme.s(8)

        PageHeader {
            Layout.fillWidth: true
            title: qsTr("Music")
            subtitle: page.counts.tracks !== undefined
                      ? qsTr("%1 songs  ·  %2 albums  ·  %3 artists").arg(Number(page.counts.tracks).toLocaleString(Qt.locale(), "f", 0))
                            .arg(Number(page.counts.albums).toLocaleString(Qt.locale(), "f", 0)).arg(page.counts.artists)
                      : ""
            SearchField {
                id: searchField
                width: Math.min(Theme.s(320), page.width * 0.3)
                placeholder: [qsTr("Search albums"), qsTr("Search artists"), qsTr("Search songs")][page.tab]
                onSearch: text => page.search = text
            }
        }

        RowLayout {
            Layout.fillWidth: true
            Layout.bottomMargin: Theme.s(12)
            spacing: Theme.s(12)
            Segmented {
                options: [qsTr("Albums"), qsTr("Artists"), qsTr("Songs")]
                currentIndex: page.tab
                onActivated: index => page.setTab(index)
            }
            Item { Layout.fillWidth: true }
            PillButton {
                visible: page.tab === 0
                kind: "ghost"
                compact: true
                glyph: "chevronDown"
                text: qsTr("Sort: %1").arg(page.sortLabel(app.prefs.albumSort))
                onClicked: sortMenu.openAt(this, true)
            }
            PillButton {
                visible: page.tab === 2
                kind: "secondary"
                compact: true
                glyph: "shuffle"
                text: qsTr("Shuffle all")
                onClicked: app.playMusic({ source: "tracks", search: page.search, shuffle: true })
            }
        }

        StackLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            currentIndex: page.tab
            AlbumsView { search: page.search; sort: app.prefs.albumSort; active: page.tab === 0 }
            ArtistsView { search: page.search; active: page.tab === 1 }
            SongsView { search: page.search; active: page.tab === 2 }
        }
    }

    PopupMenu {
        id: sortMenu
        heading: qsTr("Sort albums by")
        items: page.sorts.map(s => ({ text: s.text, checked: s.key === app.prefs.albumSort, key: s.key }))
        onTriggered: (index, item) => app.prefs.albumSort = item.key
    }
}
