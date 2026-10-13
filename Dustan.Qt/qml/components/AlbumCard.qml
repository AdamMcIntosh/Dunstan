import QtQuick
import Dustan

// Square artwork card for albums and artists, with a hover play button.
FocusScope {
    id: card
    property url art
    property string title
    property string subtitle
    property bool circle: false
    property bool playable: true
    property bool selected: false
    signal open()
    signal play()

    Keys.onReturnPressed: open()
    Keys.onEnterPressed: open()
    Keys.onSpacePressed: if (playable) play()

    readonly property bool lifted: area.containsMouse || playButton.hovered || activeFocus || selected

    Item {
        id: cover
        width: parent.width
        height: width
        Artwork {
            id: artwork
            anchors.fill: parent
            source: card.art
            name: card.title
            circle: card.circle
            radius: Theme.radiusS
            scale: card.lifted ? 1.025 : 1
            Behavior on scale { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
        }
        Rectangle {
            anchors.fill: artwork
            scale: artwork.scale
            radius: card.circle ? width / 2 : Theme.radiusS
            color: "transparent"
            border.width: card.activeFocus ? Theme.s(3) : 1
            border.color: card.activeFocus ? Theme.focus : Theme.line
        }
        IconButton {
            id: playButton
            anchors { right: parent.right; bottom: parent.bottom; margins: Theme.s(10) }
            visible: card.playable
            opacity: card.lifted ? 1 : 0
            scale: card.lifted ? 1 : 0.85
            filled: true
            glyph: "play"
            diameter: Theme.s(44)
            iconSize: Theme.s(20)
            tip: qsTr("Play")
            focusPolicy: Qt.NoFocus
            onClicked: card.play()
            Behavior on opacity { NumberAnimation { duration: Theme.normal } }
            Behavior on scale { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutBack } }
        }
    }
    Column {
        anchors { top: cover.bottom; topMargin: Theme.s(12); left: parent.left; right: parent.right }
        spacing: Theme.s(3)
        Text {
            width: parent.width
            horizontalAlignment: card.circle ? Text.AlignHCenter : Text.AlignLeft
            text: card.title
            color: Theme.text
            font.family: Theme.family
            font.pixelSize: Theme.body
            font.weight: Font.Medium
            elide: Text.ElideRight
        }
        Text {
            width: parent.width
            horizontalAlignment: card.circle ? Text.AlignHCenter : Text.AlignLeft
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
        height: cover.height
        hoverEnabled: true
        cursorShape: Qt.PointingHandCursor
        z: -1
        onClicked: card.open()
    }
}
