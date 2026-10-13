import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import Dustan

// Build a channel from whole shows, individual seasons or films.
Sheet {
    id: sheet
    signal created(string name)

    title: qsTr("New channel")
    subtitle: qsTr("Pick shows, seasons or films. They air back to back and repeat around the clock, starting now.")
    preferredWidth: Theme.s(760)
    preferredHeight: Theme.s(800)

    property var sources: []
    property var selected: ({})
    property var expanded: ({})
    property string filter: ""
    property string error: ""
    property bool busy: false
    property bool loading: false

    readonly property var visibleSources: filter.length === 0 ? sources
        : sources.filter(s => (s.title + " " + s.collection).toLowerCase().indexOf(filter.toLowerCase()) >= 0)
    readonly property var summary: {
        let units = 0, count = 0, duration = 0
        for (const s of sources)
            for (const season of s.seasons)
                if (selected[season.key]) { units++; count += season.count; duration += season.duration }
        return { units: units, count: count, duration: duration }
    }

    onOpened: {
        nameField.text = ""
        selected = {}
        expanded = {}
        error = ""
        order.currentIndex = 0
        search.clear()
        loading = true
        Bridge.call("tv.sources", {}, (result, err) => {
            loading = false
            if (err) error = err
            else sources = result
        })
        nameField.input.forceActiveFocus()
    }

    function stateOf(show) {
        let on = 0
        for (const season of show.seasons) if (selected[season.key]) on++
        return on === 0 ? "none" : on === show.seasons.length ? "all" : "some"
    }
    function toggleShow(show) {
        const next = Object.assign({}, selected)
        const turnOn = stateOf(show) !== "all"
        for (const season of show.seasons) {
            if (turnOn) next[season.key] = true
            else delete next[season.key]
        }
        selected = next
    }
    function toggleSeason(key) {
        const next = Object.assign({}, selected)
        if (next[key]) delete next[key]
        else next[key] = true
        selected = next
    }
    function toggleExpanded(key) {
        const next = Object.assign({}, expanded)
        next[key] = !next[key]
        expanded = next
    }
    function create() {
        error = ""
        const units = []
        for (const s of sources)
            for (const season of s.seasons)
                if (selected[season.key]) units.push(season.key)
        busy = true
        Bridge.call("tv.create", { name: nameField.text.trim(), units: units, shuffle: order.currentIndex === 1 }, (result, err) => {
            busy = false
            if (err) { error = err; return }
            sheet.close()
            sheet.created(result.name)
        })
    }

    component Check: Rectangle {
        property string mark: "none"
        width: Theme.s(20); height: width
        radius: Theme.s(5)
        color: mark === "none" ? "transparent" : Theme.accent
        border.width: mark === "none" ? Theme.s(2) : 0
        border.color: Theme.lineStrong
        Behavior on color { ColorAnimation { duration: Theme.fast } }
        Icon {
            anchors.centerIn: parent
            visible: parent.mark === "all"
            name: "check"; size: Theme.s(15); weight: 2.6
            color: Theme.accentText
        }
        Rectangle {
            anchors.centerIn: parent
            visible: parent.mark === "some"
            width: Theme.s(10); height: Theme.s(2); radius: 1
            color: Theme.accentText
        }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: Theme.s(16)

        Field {
            id: nameField
            Layout.fillWidth: true
            label: qsTr("Channel name")
            placeholder: qsTr("e.g. Saturday Morning Cartoons")
        }
        RowLayout {
            Layout.fillWidth: true
            spacing: Theme.s(12)
            Segmented { id: order; compact: true; options: [qsTr("In order"), qsTr("Shuffled")] }
            Item { Layout.fillWidth: true }
            SearchField {
                id: search
                Layout.preferredWidth: Theme.s(260)
                implicitHeight: Theme.s(34)
                placeholder: qsTr("Filter shows")
                onSearch: text => sheet.filter = text
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            radius: Theme.radiusM
            color: Theme.surface2
            border.width: 1
            border.color: Theme.line
            clip: true

            ListView {
                id: list
                anchors { fill: parent; margins: Theme.s(6) }
                clip: true
                model: sheet.visibleSources
                boundsBehavior: Flickable.StopAtBounds
                T.ScrollBar.vertical: ThinScrollBar {}
                delegate: Column {
                    id: showItem
                    required property var modelData
                    readonly property bool open: sheet.expanded[modelData.key] === true
                    readonly property bool multi: modelData.seasons.length > 1
                    width: ListView.view.width

                    Item {
                        width: parent.width
                        height: Theme.s(54)
                        Rectangle {
                            anchors.fill: parent
                            radius: Theme.radiusS
                            color: showArea.containsMouse ? Theme.hover : "transparent"
                        }
                        MouseArea { id: showArea; anchors.fill: parent; hoverEnabled: true; cursorShape: Qt.PointingHandCursor; onClicked: sheet.toggleShow(showItem.modelData) }
                        RowLayout {
                            anchors { fill: parent; leftMargin: Theme.s(12); rightMargin: Theme.s(6) }
                            spacing: Theme.s(14)
                            Check { mark: sheet.stateOf(showItem.modelData) }
                            ColumnLayout {
                                Layout.fillWidth: true
                                spacing: Theme.s(2)
                                Text {
                                    Layout.fillWidth: true
                                    text: showItem.modelData.title
                                    color: Theme.text
                                    font.family: Theme.family; font.pixelSize: Theme.body; font.weight: Font.Medium
                                    elide: Text.ElideRight
                                }
                                Text {
                                    Layout.fillWidth: true
                                    readonly property int total: showItem.modelData.seasons.reduce((n, s) => n + s.count, 0)
                                    text: [showItem.modelData.collection, showItem.modelData.isMovie ? qsTr("Film") : Theme.plural(total, qsTr("episode")),
                                           showItem.multi ? Theme.plural(showItem.modelData.seasons.length, qsTr("season")) : ""].filter(x => x).join("  ·  ")
                                    color: Theme.text3
                                    font.family: Theme.family; font.pixelSize: Theme.caption
                                    elide: Text.ElideRight
                                }
                            }
                            IconButton {
                                visible: showItem.multi
                                glyph: showItem.open ? "chevronUp" : "chevronDown"
                                tip: showItem.open ? qsTr("Hide seasons") : qsTr("Choose seasons")
                                diameter: Theme.s(32); iconSize: Theme.s(16)
                                onClicked: sheet.toggleExpanded(showItem.modelData.key)
                            }
                        }
                    }
                    Repeater {
                        model: showItem.open ? showItem.modelData.seasons : []
                        Item {
                            id: seasonItem
                            required property var modelData
                            width: showItem.width
                            height: Theme.s(42)
                            Rectangle { anchors.fill: parent; radius: Theme.radiusS; color: seasonArea.containsMouse ? Theme.hover : "transparent" }
                            MouseArea { id: seasonArea; anchors.fill: parent; hoverEnabled: true; cursorShape: Qt.PointingHandCursor; onClicked: sheet.toggleSeason(seasonItem.modelData.key) }
                            RowLayout {
                                anchors { fill: parent; leftMargin: Theme.s(46); rightMargin: Theme.s(16) }
                                spacing: Theme.s(12)
                                Check { mark: sheet.selected[seasonItem.modelData.key] ? "all" : "none"; scale: 0.9 }
                                Text {
                                    Layout.fillWidth: true
                                    text: seasonItem.modelData.title
                                    color: Theme.text2
                                    font.family: Theme.family; font.pixelSize: Theme.label
                                }
                                Text {
                                    text: Theme.plural(seasonItem.modelData.count, qsTr("episode")) + "  ·  " + Theme.duration(seasonItem.modelData.duration)
                                    color: Theme.text3
                                    font.family: Theme.family; font.pixelSize: Theme.caption
                                }
                            }
                        }
                    }
                }
            }
            Spinner { anchors.centerIn: parent; visible: sheet.loading }
            Text {
                anchors.centerIn: parent
                visible: !sheet.loading && sheet.sources.length === 0 && !sheet.error
                width: parent.width - Theme.s(60)
                horizontalAlignment: Text.AlignHCenter
                wrapMode: Text.Wrap
                text: qsTr("No videos with known runtimes yet. Scan your video folder in Settings first.")
                color: Theme.text2
                font.family: Theme.family; font.pixelSize: Theme.body
            }
        }
        Text {
            Layout.fillWidth: true
            visible: sheet.error.length > 0
            text: sheet.error
            color: Theme.live
            font.family: Theme.family; font.pixelSize: Theme.label
            wrapMode: Text.Wrap
        }
    }

    footer: [
        Text {
            Layout.fillWidth: true
            text: sheet.summary.count > 0
                  ? qsTr("%1  ·  %2 per cycle").arg(Theme.plural(sheet.summary.count, qsTr("video"))).arg(Theme.duration(sheet.summary.duration))
                  : qsTr("Nothing selected")
            color: Theme.text2
            font.family: Theme.family; font.pixelSize: Theme.label
            elide: Text.ElideRight
        },
        PillButton { text: qsTr("Cancel"); kind: "ghost"; onClicked: sheet.close() },
        PillButton {
            text: sheet.busy ? qsTr("Creating…") : qsTr("Create channel")
            kind: "primary"
            glyph: "live"
            enabled: !sheet.busy && nameField.text.trim().length > 0 && sheet.summary.count > 0
            onClicked: sheet.create()
        }
    ]
}
