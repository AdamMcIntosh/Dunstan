import QtQuick
import QtQuick.Controls.Basic as T
import Dustan

// Text button: "primary" (brass), "secondary" (raised) or "ghost" (outline).
T.AbstractButton {
    id: control
    property string kind: "secondary"
    property string glyph
    property bool compact: false
    property color tone: kind === "primary" ? Theme.accentText : Theme.text

    implicitHeight: Theme.s(compact ? 32 : 40)
    implicitWidth: row.implicitWidth + Theme.s(compact ? 26 : 34)
    focusPolicy: Qt.StrongFocus
    hoverEnabled: true
    opacity: enabled ? 1 : 0.4
    Accessible.name: text

    background: Rectangle {
        radius: height / 2
        color: control.kind === "primary" ? (control.hovered || control.down ? Theme.accentHover : Theme.accent)
             : control.kind === "secondary" ? (control.down ? Theme.surface3 : control.hovered ? Qt.lighter(Theme.surface3, 1.18) : Theme.surface3)
             : (control.down ? Theme.pressed : control.hovered ? Theme.hover : "transparent")
        border.width: control.visualFocus ? Theme.s(2) : (control.kind === "ghost" ? 1 : 0)
        border.color: control.visualFocus ? Theme.focus : Theme.lineStrong
        Behavior on color { ColorAnimation { duration: Theme.fast } }
    }

    contentItem: Item {
        Row {
            id: row
            anchors.centerIn: parent
            spacing: Theme.s(8)
            Icon {
                visible: control.glyph.length > 0
                anchors.verticalCenter: parent.verticalCenter
                name: control.glyph
                size: Theme.s(control.compact ? 15 : 17)
                color: control.tone
            }
            Text {
                anchors.verticalCenter: parent.verticalCenter
                text: control.text
                color: control.tone
                font.family: Theme.family
                font.pixelSize: control.compact ? Theme.label : Theme.body
                font.weight: Font.DemiBold
            }
        }
    }

    scale: down ? 0.97 : 1
    Behavior on scale { NumberAnimation { duration: Theme.fast; easing.type: Easing.OutCubic } }
}
