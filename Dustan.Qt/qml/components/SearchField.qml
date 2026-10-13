import QtQuick
import Dustan

// Rounded search box; emits search() after a short pause in typing.
FocusScope {
    id: root
    property alias text: input.text
    property string placeholder: qsTr("Search")
    signal search(string text)

    implicitWidth: Theme.s(300)
    implicitHeight: Theme.s(38)

    function clear() { input.text = ""; root.search("") }

    Rectangle {
        anchors.fill: parent
        radius: height / 2
        color: input.activeFocus ? Theme.surface3 : Theme.surface2
        border.width: input.activeFocus ? 1 : 0
        border.color: Theme.lineStrong
        Behavior on color { ColorAnimation { duration: Theme.fast } }
    }
    Icon {
        id: glass
        anchors { left: parent.left; leftMargin: Theme.s(14); verticalCenter: parent.verticalCenter }
        name: "search"
        size: Theme.s(17)
        color: input.activeFocus ? Theme.text : Theme.text3
    }
    TextInput {
        id: input
        anchors { left: glass.right; leftMargin: Theme.s(10); right: clearButton.left; rightMargin: Theme.s(4); verticalCenter: parent.verticalCenter }
        focus: true
        color: Theme.text
        selectionColor: Theme.accentSoft
        selectedTextColor: Theme.text
        font.family: Theme.family
        font.pixelSize: Theme.body
        clip: true
        onTextEdited: debounce.restart()
        Keys.onEscapePressed: event => { if (text.length) root.clear(); else focus = false; event.accepted = true }
        Keys.onReturnPressed: { debounce.stop(); root.search(text) }
        Text {
            anchors.fill: parent
            verticalAlignment: Text.AlignVCenter
            text: root.placeholder
            color: Theme.text3
            font: input.font
            visible: !input.text.length
        }
    }
    IconButton {
        id: clearButton
        anchors { right: parent.right; rightMargin: Theme.s(4); verticalCenter: parent.verticalCenter }
        visible: input.text.length > 0
        glyph: "close"
        diameter: Theme.s(30)
        iconSize: Theme.s(14)
        focusPolicy: Qt.NoFocus
        onClicked: root.clear()
    }
    Timer { id: debounce; interval: 260; onTriggered: root.search(input.text) }
}
