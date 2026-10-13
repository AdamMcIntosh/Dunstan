import QtQuick
import Dustan

// Centered explanation with an optional action, for empty or unavailable screens.
Column {
    id: root
    property string icon: "info"
    property string title
    property string message
    property string actionText
    property bool busy: false
    signal action()

    spacing: Theme.s(14)
    width: Math.min(parent ? parent.width - Theme.s(64) : Theme.s(460), Theme.s(460))

    Rectangle {
        anchors.horizontalCenter: parent.horizontalCenter
        width: Theme.s(64); height: width; radius: width / 2
        color: Theme.surface2
        border.width: 1
        border.color: Theme.line
        Icon { anchors.centerIn: parent; name: root.icon; size: Theme.s(26); color: Theme.accent; visible: !root.busy }
        Spinner { anchors.centerIn: parent; visible: root.busy; size: Theme.s(26) }
    }
    Text {
        width: parent.width
        horizontalAlignment: Text.AlignHCenter
        text: root.title
        color: Theme.text
        font.family: Theme.family
        font.pixelSize: Theme.heading
        font.weight: Font.DemiBold
        wrapMode: Text.Wrap
    }
    Text {
        visible: root.message.length > 0
        width: parent.width
        horizontalAlignment: Text.AlignHCenter
        text: root.message
        color: Theme.text2
        font.family: Theme.family
        font.pixelSize: Theme.body
        lineHeight: 1.35
        wrapMode: Text.Wrap
    }
    Item { width: 1; height: Theme.s(4); visible: root.actionText.length > 0 }
    PillButton {
        visible: root.actionText.length > 0
        anchors.horizontalCenter: parent.horizontalCenter
        kind: "primary"
        text: root.actionText
        onClicked: root.action()
    }
}
