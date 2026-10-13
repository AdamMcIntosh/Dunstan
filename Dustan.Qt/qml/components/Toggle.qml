import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import Dustan

// Labelled switch.
T.AbstractButton {
    id: control
    property string description
    checkable: true
    hoverEnabled: true
    focusPolicy: Qt.StrongFocus
    implicitHeight: Math.max(Theme.s(44), layout.implicitHeight + Theme.s(8))
    implicitWidth: layout.implicitWidth

    background: Item {}
    contentItem: RowLayout {
        id: layout
        spacing: Theme.s(16)
        ColumnLayout {
            spacing: Theme.s(2)
            Layout.fillWidth: true
            Text {
                text: control.text
                color: Theme.text
                font.family: Theme.family
                font.pixelSize: Theme.body
                font.weight: Font.Medium
                Layout.fillWidth: true
                wrapMode: Text.Wrap
            }
            Text {
                visible: control.description.length > 0
                text: control.description
                color: Theme.text3
                font.family: Theme.family
                font.pixelSize: Theme.caption
                Layout.fillWidth: true
                wrapMode: Text.Wrap
            }
        }
        Rectangle {
            implicitWidth: Theme.s(40); implicitHeight: Theme.s(24)
            radius: height / 2
            color: control.checked ? Theme.accent : Theme.surface3
            border.width: control.visualFocus ? Theme.s(2) : 1
            border.color: control.visualFocus ? Theme.focus : Theme.line
            Behavior on color { ColorAnimation { duration: Theme.normal } }
            Rectangle {
                width: parent.height - Theme.s(6); height: width; radius: width / 2
                y: Theme.s(3)
                x: control.checked ? parent.width - width - Theme.s(3) : Theme.s(3)
                color: control.checked ? Theme.accentText : Theme.text2
                Behavior on x { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
            }
        }
    }
}
