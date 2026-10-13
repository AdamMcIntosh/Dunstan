import QtQuick
import QtQuick.Layouts
import Dustan

// Primary navigation. Collapses to icons on narrow windows.
Rectangle {
    id: rail
    property string current: "music"
    property bool compact: false
    property bool musicActive: false
    property bool musicPlaying: false
    signal navigate(string section)

    color: Theme.rail
    implicitWidth: compact ? Theme.s(76) : Theme.s(232)
    Behavior on implicitWidth { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }

    readonly property var primary: [
        { key: "music", label: qsTr("Music"), icon: "music" },
        { key: "videos", label: qsTr("Videos"), icon: "videos" },
        { key: "tv", label: qsTr("TV Guide"), icon: "tv" },
    ]

    Rectangle { anchors { right: parent.right; top: parent.top; bottom: parent.bottom } width: 1; color: Theme.line }

    component NavItem: FocusScope {
        id: item
        property string key
        property string label
        property string icon
        property bool showBars: false
        readonly property bool active: rail.current === key
        width: parent ? parent.width : 0
        height: Theme.s(44)
        activeFocusOnTab: true
        Keys.onReturnPressed: rail.navigate(key)
        Keys.onEnterPressed: rail.navigate(key)
        Keys.onSpacePressed: rail.navigate(key)

        Rectangle {
            anchors { fill: parent; leftMargin: Theme.s(12); rightMargin: Theme.s(12) }
            radius: Theme.radiusS
            color: item.active ? Theme.surface2 : mouse.pressed ? Theme.pressed : mouse.containsMouse ? Theme.hover : "transparent"
            border.width: item.activeFocus ? Theme.s(2) : 0
            border.color: Theme.focus
            Behavior on color { ColorAnimation { duration: Theme.fast } }
            Rectangle {
                anchors { left: parent.left; verticalCenter: parent.verticalCenter }
                width: Theme.s(3); height: item.active ? parent.height * 0.5 : 0
                radius: width / 2
                color: Theme.accent
                Behavior on height { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
            }
            Icon {
                id: glyph
                anchors { left: parent.left; leftMargin: rail.compact ? (parent.width - width) / 2 : Theme.s(14); verticalCenter: parent.verticalCenter }
                name: item.icon
                size: Theme.s(20)
                color: item.active ? Theme.accent : mouse.containsMouse ? Theme.text : Theme.text2
                visible: !(item.showBars && rail.compact)
            }
            Text {
                visible: !rail.compact
                anchors { left: glyph.right; leftMargin: Theme.s(14); right: bars.left; rightMargin: Theme.s(8); verticalCenter: parent.verticalCenter }
                text: item.label
                color: item.active ? Theme.text : mouse.containsMouse ? Theme.text : Theme.text2
                font.family: Theme.family
                font.pixelSize: Theme.body
                font.weight: item.active ? Font.DemiBold : Font.Medium
                elide: Text.ElideRight
            }
            EqualizerBars {
                id: bars
                visible: item.showBars
                anchors { right: parent.right; rightMargin: rail.compact ? (parent.width - width) / 2 : Theme.s(14); verticalCenter: parent.verticalCenter }
                playing: rail.musicPlaying
                maxHeight: Theme.s(12)
            }
        }
        MouseArea {
            id: mouse
            anchors.fill: parent
            hoverEnabled: true
            cursorShape: Qt.PointingHandCursor
            onClicked: rail.navigate(item.key)
        }
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.topMargin: Theme.s(22)
        anchors.bottomMargin: Theme.s(18)
        spacing: Theme.s(4)

        Row {
            Layout.leftMargin: rail.compact ? (rail.width - logo.width) / 2 : Theme.s(24)
            Layout.bottomMargin: Theme.s(26)
            spacing: Theme.s(12)
            Image {
                id: logo
                source: "qrc:/qt/qml/Dustan/packaging/dustan.svg"
                width: Theme.s(30); height: width
                sourceSize: Qt.size(width * 2, height * 2)
                anchors.verticalCenter: parent.verticalCenter
            }
            Text {
                visible: !rail.compact
                anchors.verticalCenter: parent.verticalCenter
                text: "Dustan"
                color: Theme.text
                font.family: Theme.family
                font.pixelSize: Theme.s(19)
                font.weight: Font.Bold
                font.letterSpacing: -0.4
            }
        }

        Text {
            visible: !rail.compact
            Layout.leftMargin: Theme.s(26)
            Layout.bottomMargin: Theme.s(6)
            text: qsTr("LIBRARY")
            color: Theme.text3
            font.family: Theme.family
            font.pixelSize: Theme.overline
            font.weight: Font.DemiBold
            font.letterSpacing: 1.2
        }
        Repeater {
            model: rail.primary
            NavItem {
                required property var modelData
                Layout.fillWidth: true
                key: modelData.key
                label: modelData.label
                icon: modelData.icon
            }
        }
        Item { Layout.preferredHeight: Theme.s(18) }
        Text {
            visible: !rail.compact
            Layout.leftMargin: Theme.s(26)
            Layout.bottomMargin: Theme.s(6)
            text: qsTr("PLAYING")
            color: Theme.text3
            font.family: Theme.family
            font.pixelSize: Theme.overline
            font.weight: Font.DemiBold
            font.letterSpacing: 1.2
        }
        NavItem {
            Layout.fillWidth: true
            key: "nowplaying"
            label: qsTr("Now Playing")
            icon: "nowPlaying"
            showBars: rail.musicActive
        }
        Item { Layout.fillHeight: true }
        NavItem {
            Layout.fillWidth: true
            key: "settings"
            label: qsTr("Settings")
            icon: "settings"
        }
    }
}
