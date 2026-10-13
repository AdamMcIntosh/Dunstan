import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import Dustan

// Programme guide for scheduled channels: what's on now, then a scrollable timeline.
FocusScope {
    id: page
    readonly property var app: T.ApplicationWindow.window
    property var channels: []
    property var airingNow: []
    property real windowStart: 0
    readonly property int hours: 6
    readonly property real windowEnd: windowStart + hours * 3600000
    property real now: Date.now()
    readonly property real ppm: Theme.s(7)              // pixels per minute
    readonly property real rowHeight: Theme.s(80)
    readonly property real labelWidth: Theme.s(220)
    readonly property real rulerHeight: Theme.s(40)
    property bool loaded: false

    function floorHalfHour(ms) { return Math.floor(ms / 1800000) * 1800000 }
    function xFor(ms) { return (ms - windowStart) / 60000 * ppm }
    function reload() {
        if (!Bridge.ready) return
        now = Date.now()
        Bridge.call("tv.guide", { from: windowStart, hours: hours }, (result, error) => {
            if (error) { app.toast(error, "error"); return }
            channels = result.channels || []
            loaded = true
        })
        Bridge.call("tv.channels", {}, (result, error) => { if (!error) airingNow = result || [] })
    }
    function shift(hoursDelta) {
        const earliest = floorHalfHour(Date.now()) - 1800000
        windowStart = Math.max(earliest, Math.min(earliest + 7 * 24 * 3600000, windowStart + hoursDelta * 3600000))
        grid.contentX = 0
        reload()
    }
    function goNow() {
        windowStart = floorHalfHour(Date.now()) - 1800000
        grid.contentX = 0
        reload()
    }
    function showDetails(channel, programme, item) {
        details.channel = channel
        details.programme = programme
        const p = item.mapToItem(page, 0, item.height)
        details.x = Math.max(Theme.s(16), Math.min(page.width - details.width - Theme.s(16), p.x))
        details.y = Math.min(page.height - details.implicitHeight - Theme.s(16), p.y + Theme.s(6))
        details.open()
    }

    Component.onCompleted: { windowStart = floorHalfHour(Date.now()) - 1800000; reload(); app.channelSheet = creator }
    onVisibleChanged: if (visible) reload()
    Connections { target: Bridge; function onReadyChanged() { page.reload() } }
    Connections { target: page.app; function onLibraryChanged(kind) { if (kind === "tv" || kind === "video") page.reload() } }
    Timer {
        interval: 15000; repeat: true; running: page.visible
        onTriggered: {
            page.now = Date.now()
            // Refresh once the airing programme changes on any channel.
            if (page.airingNow.some(c => c.now && c.now.end <= page.now)) page.reload()
        }
    }

    ColumnLayout {
        anchors { fill: parent; leftMargin: (app ? app.pagePadding : 0); rightMargin: (app ? app.pagePadding : 0); topMargin: Theme.s(12); bottomMargin: Theme.s(24) }
        spacing: Theme.s(16)

        PageHeader {
            Layout.fillWidth: true
            title: qsTr("TV Guide")
            subtitle: Qt.formatDate(new Date(page.windowStart + 1800000), "dddd, MMMM d") + (page.channels.length ? "  ·  " + Theme.plural(page.channels.length, qsTr("channel")) : "")
            IconButton { glyph: "chevronLeft"; tip: qsTr("Earlier"); enabled: page.windowStart > page.floorHalfHour(Date.now()) - 1800000; opacity: enabled ? 1 : 0.35; onClicked: page.shift(-3) }
            PillButton { text: qsTr("Now"); compact: true; anchors.verticalCenter: parent.verticalCenter; onClicked: page.goNow() }
            IconButton { glyph: "chevronRight"; tip: qsTr("Later"); onClicked: page.shift(3) }
            Item { width: Theme.s(8); height: 1 }
            PillButton { kind: "primary"; glyph: "plus"; text: qsTr("New channel"); anchors.verticalCenter: parent.verticalCenter; onClicked: creator.open() }
        }

        // ---- on now -----------------------------------------------------------------------------------
        ListView {
            id: nowRow
            Layout.fillWidth: true
            Layout.preferredHeight: visible ? Theme.s(126) : 0
            visible: page.airingNow.length > 0
            orientation: ListView.Horizontal
            spacing: Theme.s(16)
            clip: true
            boundsBehavior: Flickable.StopAtBounds
            model: page.airingNow
            keyNavigationEnabled: true
            T.ScrollBar.horizontal: ThinScrollBar { orientation: Qt.Horizontal }
            delegate: FocusScope {
                id: tile
                required property var modelData
                required property int index
                width: Theme.s(380)
                height: nowRow.height - Theme.s(8)
                focus: ListView.isCurrentItem
                readonly property var programme: modelData.now
                Keys.onReturnPressed: page.app.watchChannel(modelData.id)
                Keys.onEnterPressed: page.app.watchChannel(modelData.id)
                Rectangle {
                    anchors.fill: parent
                    radius: Theme.radiusM
                    color: tileArea.containsMouse ? Theme.surface3 : Theme.surface2
                    border.width: tile.activeFocus ? Theme.s(2) : 1
                    border.color: tile.activeFocus ? Theme.focus : Theme.line
                    Behavior on color { ColorAnimation { duration: Theme.fast } }
                }
                MouseArea { id: tileArea; anchors.fill: parent; hoverEnabled: true; cursorShape: Qt.PointingHandCursor; onClicked: page.app.watchChannel(tile.modelData.id) }
                RowLayout {
                    anchors { fill: parent; margins: Theme.s(10) }
                    spacing: Theme.s(14)
                    VideoThumb {
                        Layout.preferredHeight: parent.height
                        Layout.preferredWidth: Math.round(parent.height * 16 / 9)
                        path: tile.programme ? tile.programme.path : ""
                        duration: tile.programme ? (tile.programme.end - tile.programme.start) / 1000 : 0
                        radius: Theme.radiusS
                        showProgress: false
                        Rectangle {
                            anchors { left: parent.left; top: parent.top; margins: Theme.s(8) }
                            width: liveText.implicitWidth + Theme.s(14); height: Theme.s(20); radius: Theme.s(4)
                            color: Theme.live
                            Text { id: liveText; anchors.centerIn: parent; text: qsTr("LIVE"); color: "white"; font.family: Theme.family; font.pixelSize: Theme.overline; font.weight: Font.Bold; font.letterSpacing: 0.8 }
                        }
                        Rectangle {
                            anchors.centerIn: parent
                            width: Theme.s(36); height: width; radius: width / 2
                            color: Qt.rgba(0, 0, 0, 0.55)
                            opacity: tileArea.containsMouse ? 1 : 0
                            Behavior on opacity { NumberAnimation { duration: Theme.fast } }
                            Icon { anchors.centerIn: parent; anchors.horizontalCenterOffset: Theme.s(1); name: "play"; size: Theme.s(16) }
                        }
                    }
                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: Theme.s(4)
                        Text {
                            Layout.fillWidth: true
                            text: tile.modelData.name.toUpperCase()
                            color: Theme.accent
                            font.family: Theme.family
                            font.pixelSize: Theme.overline
                            font.weight: Font.DemiBold
                            font.letterSpacing: 1.1
                            elide: Text.ElideRight
                        }
                        Text {
                            Layout.fillWidth: true
                            text: tile.programme ? tile.programme.title : qsTr("No schedule")
                            color: Theme.text
                            font.family: Theme.family
                            font.pixelSize: Theme.body
                            font.weight: Font.DemiBold
                            elide: Text.ElideRight
                        }
                        Text {
                            Layout.fillWidth: true
                            text: tile.programme ? tile.programme.subtitle : ""
                            color: Theme.text2
                            font.family: Theme.family
                            font.pixelSize: Theme.caption
                            elide: Text.ElideRight
                        }
                        ProgressLine {
                            Layout.fillWidth: true
                            Layout.topMargin: Theme.s(4)
                            value: tile.programme ? (page.now - tile.programme.start) / (tile.programme.end - tile.programme.start) : 0
                        }
                        Text {
                            Layout.fillWidth: true
                            readonly property var upcoming: tile.modelData.next
                            text: tile.programme ? qsTr("%1 left").arg(Theme.duration(Math.max(60, (tile.programme.end - page.now) / 1000)))
                                                   + (upcoming ? qsTr("  ·  Next: %1").arg(upcoming.title === tile.programme.title ? upcoming.subtitle : upcoming.title) : "") : ""
                            color: Theme.text3
                            font.family: Theme.family
                            font.pixelSize: Theme.caption
                            elide: Text.ElideRight
                        }
                    }
                }
            }
        }

        // ---- timeline -----------------------------------------------------------------------------------
        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            visible: page.channels.length > 0
            radius: Theme.radiusL
            color: Theme.surface
            border.width: 1
            border.color: Theme.line
            clip: true

            // Corner
            Item {
                id: corner
                width: page.labelWidth
                height: page.rulerHeight
                Text {
                    anchors { left: parent.left; leftMargin: Theme.s(18); verticalCenter: parent.verticalCenter }
                    text: qsTr("CHANNELS")
                    color: Theme.text3
                    font.family: Theme.family
                    font.pixelSize: Theme.overline
                    font.weight: Font.DemiBold
                    font.letterSpacing: 1.2
                }
            }
            Rectangle { anchors { left: parent.left; right: parent.right; top: corner.bottom } height: 1; color: Theme.line }
            Rectangle { x: page.labelWidth; anchors { top: parent.top; bottom: parent.bottom } width: 1; color: Theme.line }

            // Time ruler, following the grid horizontally.
            Flickable {
                id: ruler
                x: page.labelWidth + 1
                width: parent.width - x
                height: page.rulerHeight
                interactive: false
                clip: true
                contentWidth: grid.contentWidth
                contentX: grid.contentX
                Repeater {
                    model: page.hours * 2
                    Item {
                        required property int index
                        x: index * 30 * page.ppm
                        width: 30 * page.ppm
                        height: ruler.height
                        Rectangle { width: 1; height: Theme.s(10); anchors.bottom: parent.bottom; color: Theme.lineStrong }
                        Text {
                            anchors { left: parent.left; leftMargin: Theme.s(8); verticalCenter: parent.verticalCenter }
                            text: Theme.timeOfDay(page.windowStart + index * 1800000)
                            color: Theme.text2
                            // Make room for the "now" marker's time pill.
                            readonly property real gap: parent.x + x - page.xFor(page.now)
                            opacity: gap > -implicitWidth - Theme.s(34) && gap < Theme.s(34) ? 0 : 1
                            font.family: Theme.family
                            font.pixelSize: Theme.caption
                            font.weight: Font.Medium
                            font.features: { "tnum": 1 }
                        }
                    }
                }
            }

            // Channel names, following the grid vertically.
            Flickable {
                id: names
                y: page.rulerHeight + 1
                width: page.labelWidth
                height: parent.height - y
                interactive: false
                clip: true
                contentHeight: grid.contentHeight
                contentY: grid.contentY
                Column {
                    Repeater {
                        model: page.channels
                        Item {
                            id: label
                            required property var modelData
                            width: page.labelWidth
                            height: page.rowHeight
                            Rectangle { anchors.bottom: parent.bottom; width: parent.width; height: 1; color: Theme.line }
                            ColumnLayout {
                                anchors { left: parent.left; leftMargin: Theme.s(18); right: actions.left; rightMargin: Theme.s(6); verticalCenter: parent.verticalCenter }
                                spacing: Theme.s(3)
                                Text {
                                    Layout.fillWidth: true
                                    text: label.modelData.name
                                    color: Theme.text
                                    font.family: Theme.family
                                    font.pixelSize: Theme.body
                                    font.weight: Font.DemiBold
                                    elide: Text.ElideRight
                                }
                                Text {
                                    Layout.fillWidth: true
                                    text: Theme.plural(label.modelData.videoCount, qsTr("video"))
                                    color: Theme.text3
                                    font.family: Theme.family
                                    font.pixelSize: Theme.caption
                                }
                            }
                            Row {
                                id: actions
                                anchors { right: parent.right; rightMargin: Theme.s(10); verticalCenter: parent.verticalCenter }
                                spacing: Theme.s(2)
                                IconButton {
                                    glyph: "play"; tip: qsTr("Watch live"); diameter: Theme.s(32); iconSize: Theme.s(15)
                                    onClicked: page.app.watchChannel(label.modelData.id)
                                }
                                IconButton {
                                    glyph: "more"; tip: qsTr("Channel options"); diameter: Theme.s(32); iconSize: Theme.s(16)
                                    onClicked: { channelMenu.channel = label.modelData; channelMenu.openAt(this, false) }
                                }
                            }
                        }
                    }
                }
            }

            Flickable {
                id: grid
                x: page.labelWidth + 1
                y: page.rulerHeight + 1
                width: parent.width - x
                height: parent.height - y
                clip: true
                contentWidth: page.hours * 60 * page.ppm
                contentHeight: page.channels.length * page.rowHeight
                boundsBehavior: Flickable.StopAtBounds
                flickableDirection: Flickable.HorizontalAndVerticalFlick
                T.ScrollBar.horizontal: ThinScrollBar { orientation: Qt.Horizontal }
                T.ScrollBar.vertical: ThinScrollBar {}

                // Half-hour guides
                Repeater {
                    model: page.hours * 2
                    Rectangle {
                        required property int index
                        x: index * 30 * page.ppm
                        width: 1
                        height: grid.contentHeight
                        color: Qt.rgba(1, 1, 1, index % 2 === 0 ? 0.05 : 0.025)
                    }
                }
                Column {
                    Repeater {
                        model: page.channels
                        Item {
                            id: channelRow
                            required property var modelData
                            width: grid.contentWidth
                            height: page.rowHeight
                            Rectangle { anchors.bottom: parent.bottom; width: parent.width; height: 1; color: Theme.line }
                            Repeater {
                                model: channelRow.modelData.programmes
                                Rectangle {
                                    id: block
                                    required property var modelData
                                    readonly property var programme: modelData
                                    readonly property bool airing: programme.start <= page.now && programme.end > page.now
                                    readonly property bool past: programme.end <= page.now
                                    readonly property real startX: page.xFor(Math.max(programme.start, page.windowStart))
                                    x: startX + Theme.s(3)
                                    y: Theme.s(6)
                                    width: Math.max(Theme.s(4), page.xFor(Math.min(programme.end, page.windowEnd)) - startX - Theme.s(6))
                                    height: page.rowHeight - Theme.s(12)
                                    radius: Theme.s(8)
                                    color: blockArea.containsMouse ? Theme.surface3 : airing ? Qt.rgba(0.89, 0.725, 0.42, 0.12) : past ? "transparent" : Theme.surface2
                                    border.width: 1
                                    border.color: airing ? Qt.rgba(0.89, 0.725, 0.42, 0.35) : Theme.line
                                    clip: true
                                    Behavior on color { ColorAnimation { duration: Theme.fast } }
                                    Rectangle {
                                        visible: block.airing
                                        width: Math.max(0, Math.min(1, (page.now - block.programme.start) / (block.programme.end - block.programme.start))) * parent.width
                                        height: Theme.s(3)
                                        anchors.bottom: parent.bottom
                                        color: Theme.accent
                                        opacity: 0.85
                                    }
                                    Column {
                                        anchors { left: parent.left; right: parent.right; verticalCenter: parent.verticalCenter; leftMargin: Theme.s(12); rightMargin: Theme.s(8) }
                                        spacing: Theme.s(3)
                                        visible: block.width > Theme.s(44)
                                        opacity: block.past ? 0.5 : 1
                                        Text {
                                            width: parent.width
                                            text: block.programme.title
                                            color: Theme.text
                                            font.family: Theme.family
                                            font.pixelSize: Theme.label
                                            font.weight: Font.DemiBold
                                            elide: Text.ElideRight
                                        }
                                        Text {
                                            width: parent.width
                                            text: block.programme.subtitle
                                            color: Theme.text2
                                            font.family: Theme.family
                                            font.pixelSize: Theme.caption
                                            elide: Text.ElideRight
                                        }
                                        Text {
                                            width: parent.width
                                            text: Theme.timeOfDay(block.programme.start) + " – " + Theme.timeOfDay(block.programme.end)
                                            color: Theme.text3
                                            font.family: Theme.family
                                            font.pixelSize: Theme.caption
                                            font.features: { "tnum": 1 }
                                            elide: Text.ElideRight
                                        }
                                    }
                                    MouseArea {
                                        id: blockArea
                                        anchors.fill: parent
                                        hoverEnabled: true
                                        cursorShape: Qt.PointingHandCursor
                                        onClicked: page.showDetails(channelRow.modelData, block.programme, block)
                                        onDoubleClicked: if (block.airing) page.app.watchChannel(channelRow.modelData.id)
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Now marker across the ruler and grid.
            Item {
                readonly property real at: page.labelWidth + 1 + page.xFor(page.now) - grid.contentX
                visible: at > page.labelWidth && at < parent.width
                x: at
                y: Theme.s(6)
                height: parent.height - y
                width: 1
                Rectangle { width: Theme.s(2); x: -width / 2; height: parent.height; color: Theme.accent; opacity: 0.9 }
                Rectangle {
                    x: -width / 2; y: 0
                    width: Theme.s(54); height: Theme.s(20); radius: height / 2
                    color: Theme.accent
                    Text {
                        anchors.centerIn: parent
                        text: Theme.timeOfDay(page.now)
                        color: Theme.accentText
                        font.family: Theme.family
                        font.pixelSize: Theme.overline
                        font.weight: Font.Bold
                        font.features: { "tnum": 1 }
                    }
                }
            }
        }

        EmptyState {
            Layout.alignment: Qt.AlignCenter
            Layout.topMargin: Theme.s(40)
            visible: page.loaded && page.channels.length === 0
            icon: "tv"
            title: qsTr("No channels yet")
            message: qsTr("Channels turn your NAS videos into a continuous broadcast. Pick shows or seasons, and Dustan schedules them back to back, around the clock — tune in any time and join whatever is on.")
            actionText: qsTr("Create a channel")
            onAction: creator.open()
        }
        Item { Layout.fillHeight: true; visible: page.channels.length === 0 }
    }

    // ---- programme details ------------------------------------------------------------------------------
    T.Popup {
        id: details
        property var channel: null
        property var programme: null
        readonly property bool airing: programme && programme.start <= page.now && programme.end > page.now
        width: Theme.s(360)
        padding: Theme.s(18)
        focus: true
        closePolicy: T.Popup.CloseOnEscape | T.Popup.CloseOnPressOutside
        enter: Transition { NumberAnimation { property: "opacity"; from: 0; to: 1; duration: Theme.fast } }
        background: Rectangle { radius: Theme.radiusM; color: Theme.surface2; border.width: 1; border.color: Theme.lineStrong }
        contentItem: ColumnLayout {
            spacing: Theme.s(6)
            Text {
                text: details.channel ? details.channel.name.toUpperCase() : ""
                color: Theme.accent
                font.family: Theme.family; font.pixelSize: Theme.overline; font.weight: Font.DemiBold; font.letterSpacing: 1.1
            }
            Text {
                Layout.fillWidth: true
                text: details.programme ? details.programme.title : ""
                color: Theme.text
                font.family: Theme.family; font.pixelSize: Theme.subheading; font.weight: Font.DemiBold
                wrapMode: Text.Wrap
            }
            Text {
                Layout.fillWidth: true
                text: details.programme ? details.programme.subtitle : ""
                color: Theme.text2
                font.family: Theme.family; font.pixelSize: Theme.label
                wrapMode: Text.Wrap
            }
            Text {
                Layout.topMargin: Theme.s(4)
                text: !details.programme ? "" : Theme.timeOfDay(details.programme.start) + " – " + Theme.timeOfDay(details.programme.end) + "  ·  "
                      + (details.airing ? qsTr("On now, %1 left").arg(Theme.duration(Math.max(60, (details.programme.end - page.now) / 1000)))
                         : details.programme.start > page.now ? qsTr("Starts in %1").arg(Theme.duration(Math.max(60, (details.programme.start - page.now) / 1000)))
                         : qsTr("Ended"))
                color: Theme.text3
                font.family: Theme.family; font.pixelSize: Theme.caption
            }
            Text {
                visible: details.programme && !details.programme.available
                Layout.fillWidth: true
                text: qsTr("This file is currently unavailable. Check that the NAS share is mounted.")
                color: Theme.live
                font.family: Theme.family; font.pixelSize: Theme.caption
                wrapMode: Text.Wrap
            }
            PillButton {
                visible: details.airing
                Layout.topMargin: Theme.s(8)
                kind: "primary"; glyph: "live"; text: qsTr("Watch live")
                onClicked: { details.close(); page.app.watchChannel(details.channel.id) }
            }
        }
    }

    PopupMenu {
        id: channelMenu
        property var channel: null
        items: [
            { text: qsTr("Watch live"), icon: "play", action: "watch" },
            { separator: true },
            { text: qsTr("Remove channel…"), icon: "trash", action: "remove" },
        ]
        onTriggered: (index, item) => {
            if (item.action === "watch") page.app.watchChannel(channel.id)
            else if (item.action === "remove") { removeSheet.channel = channel; removeSheet.open() }
        }
    }

    Sheet {
        id: removeSheet
        property var channel: null
        title: qsTr("Remove “%1”?").arg(channel ? channel.name : "")
        subtitle: qsTr("The schedule is deleted. Your video files stay on the NAS.")
        preferredWidth: Theme.s(460)
        footer: [
            Item { Layout.fillWidth: true },
            PillButton { text: qsTr("Cancel"); kind: "ghost"; onClicked: removeSheet.close() },
            PillButton {
                text: qsTr("Remove channel"); kind: "primary"
                onClicked: Bridge.call("tv.remove", { id: removeSheet.channel.id }, (r, error) => {
                    removeSheet.close()
                    if (error) page.app.toast(error, "error")
                    else page.app.toast(qsTr("Channel removed."), "success")
                })
            }
        ]
    }

    CreateChannelSheet {
        id: creator
        onCreated: name => { page.app.toast(qsTr("“%1” is on the air.").arg(name), "success"); page.reload() }
    }
}
