import QtQuick
import Dustan

// 16:9 card for series, films and episodes.
FocusScope {
    id: card
    property string path
    property string poster
    property real duration: 0
    property real progress: 0
    property bool watched: false
    property string title
    property string subtitle
    property string badge
    signal open()
    signal play()

    Keys.onReturnPressed: open()
    Keys.onEnterPressed: open()
    Keys.onSpacePressed: play()

    readonly property bool lifted: area.containsMouse || playButton.hovered || activeFocus

    Item {
        id: frame
        width: parent.width
        height: Math.round(width * 9 / 16)
        VideoThumb {
            id: thumb
            anchors.fill: parent
            path: card.path
            poster: card.poster
            duration: card.duration
            progress: card.progress
            watched: card.watched
            fallbackText: card.title
            radius: Theme.radiusM
            scale: card.lifted ? 1.02 : 1
            Behavior on scale { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
        }
        Rectangle {
            anchors.fill: thumb
            scale: thumb.scale
            radius: Theme.radiusM
            color: "transparent"
            border.width: card.activeFocus ? Theme.s(3) : 1
            border.color: card.activeFocus ? Theme.focus : Theme.line
        }
        Rectangle {
            visible: card.badge.length > 0
            anchors { left: parent.left; top: parent.top; margins: Theme.s(10) }
            height: Theme.s(22)
            width: badgeText.implicitWidth + Theme.s(14)
            radius: Theme.s(4)
            color: Qt.rgba(0, 0, 0, 0.6)
            Text {
                id: badgeText
                anchors.centerIn: parent
                text: card.badge
                color: Theme.text
                font.family: Theme.family
                font.pixelSize: Theme.overline
                font.weight: Font.DemiBold
                font.letterSpacing: 0.6
            }
        }
        IconButton {
            id: playButton
            anchors.centerIn: parent
            opacity: card.lifted ? 1 : 0
            scale: card.lifted ? 1 : 0.85
            filled: true
            glyph: "play"
            diameter: Theme.s(52)
            iconSize: Theme.s(24)
            focusPolicy: Qt.NoFocus
            tip: card.progress > 0 && !card.watched ? qsTr("Resume") : qsTr("Play")
            onClicked: card.play()
            Behavior on opacity { NumberAnimation { duration: Theme.normal } }
            Behavior on scale { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutBack } }
        }
    }
    Column {
        anchors { top: frame.bottom; topMargin: Theme.s(12); left: parent.left; right: parent.right }
        spacing: Theme.s(3)
        Text {
            width: parent.width
            text: card.title
            color: Theme.text
            font.family: Theme.family
            font.pixelSize: Theme.body
            font.weight: Font.Medium
            elide: Text.ElideRight
        }
        Text {
            width: parent.width
            text: card.subtitle
            color: Theme.text3
            font.family: Theme.family
            font.pixelSize: Theme.caption
            elide: Text.ElideRight
        }
    }
    MouseArea {
        id: area
        anchors { left: parent.left; right: parent.right; top: parent.top }
        height: frame.height
        hoverEnabled: true
        cursorShape: Qt.PointingHandCursor
        z: -1
        onClicked: card.open()
    }
}
