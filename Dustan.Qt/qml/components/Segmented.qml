import QtQuick
import Dustan

// Segmented control with a sliding selection pill.
FocusScope {
    id: root
    property var options: []
    property int currentIndex: 0
    property bool compact: false
    signal activated(int index)

    implicitHeight: Theme.s(compact ? 32 : 38)
    implicitWidth: row.implicitWidth + Theme.s(8)
    activeFocusOnTab: true

    Keys.onLeftPressed: if (currentIndex > 0) { currentIndex--; activated(currentIndex) }
    Keys.onRightPressed: if (currentIndex < options.length - 1) { currentIndex++; activated(currentIndex) }

    Rectangle {
        anchors.fill: parent
        radius: height / 2
        color: Theme.surface2
        border.width: root.activeFocus ? Theme.s(2) : 0
        border.color: Theme.focus
    }
    Rectangle {
        id: pill
        readonly property Item target: repeater.count > root.currentIndex ? repeater.itemAt(root.currentIndex) : null
        x: target ? row.x + target.x : 0
        width: target ? target.width : 0
        y: Theme.s(4)
        height: parent.height - Theme.s(8)
        radius: height / 2
        color: Theme.surface3
        border.width: 1
        border.color: Theme.line
        Behavior on x { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
        Behavior on width { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
    }
    Row {
        id: row
        anchors.centerIn: parent
        Repeater {
            id: repeater
            model: root.options
            delegate: Item {
                required property int index
                required property var modelData
                width: label.implicitWidth + Theme.s(root.compact ? 24 : 32)
                height: root.height
                Text {
                    id: label
                    anchors.centerIn: parent
                    text: modelData
                    color: index === root.currentIndex ? Theme.text : area.containsMouse ? Theme.text : Theme.text2
                    font.family: Theme.family
                    font.pixelSize: root.compact ? Theme.label : Theme.body
                    font.weight: index === root.currentIndex ? Font.DemiBold : Font.Medium
                    Behavior on color { ColorAnimation { duration: Theme.fast } }
                }
                MouseArea {
                    id: area
                    anchors.fill: parent
                    hoverEnabled: true
                    cursorShape: Qt.PointingHandCursor
                    onClicked: { root.currentIndex = index; root.activated(index) }
                }
            }
        }
    }
}
