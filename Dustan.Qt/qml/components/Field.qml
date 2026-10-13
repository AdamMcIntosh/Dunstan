import QtQuick
import QtQuick.Layouts
import Dustan

// Labelled single-line text field.
ColumnLayout {
    id: root
    property string label
    property string placeholder
    property string hint
    property alias text: input.text
    property alias input: input
    property bool numeric: false
    signal accepted()
    spacing: Theme.s(6)

    Text {
        visible: root.label.length > 0
        text: root.label
        color: Theme.text2
        font.family: Theme.family
        font.pixelSize: Theme.label
        font.weight: Font.Medium
    }
    Rectangle {
        Layout.fillWidth: true
        implicitHeight: Theme.s(40)
        radius: Theme.radiusS
        color: input.activeFocus ? Theme.surface3 : Theme.surface2
        border.width: 1
        border.color: input.activeFocus ? Theme.accent : Theme.line
        Behavior on border.color { ColorAnimation { duration: Theme.fast } }
        TextInput {
            id: input
            anchors { fill: parent; leftMargin: Theme.s(12); rightMargin: Theme.s(12) }
            verticalAlignment: TextInput.AlignVCenter
            color: Theme.text
            selectionColor: Theme.accentSoft
            selectedTextColor: Theme.text
            selectByMouse: true
            clip: true
            font.family: Theme.family
            font.pixelSize: Theme.body
            inputMethodHints: root.numeric ? Qt.ImhDigitsOnly : Qt.ImhNone
            validator: root.numeric ? digits : null
            onAccepted: root.accepted()
            // Show the start of long values (album titles, paths) until the field is edited.
            onTextChanged: if (!activeFocus) cursorPosition = 0
            IntValidator { id: digits; bottom: 0; top: 9999 }
            Text {
                anchors.fill: parent
                verticalAlignment: Text.AlignVCenter
                text: root.placeholder
                color: Theme.text3
                font: input.font
                visible: !input.text.length && !input.activeFocus
                elide: Text.ElideRight
            }
        }
    }
    Text {
        visible: root.hint.length > 0
        Layout.fillWidth: true
        text: root.hint
        color: Theme.text3
        font.family: Theme.family
        font.pixelSize: Theme.caption
        wrapMode: Text.Wrap
    }
}
