import QtQuick
import QtQuick.Controls.Basic as T
import Dustan

// Round icon button with hover, press and keyboard focus states.
T.AbstractButton {
    id: control
    property string glyph
    property real iconSize: Theme.s(20)
    property real diameter: Theme.s(36)
    property color iconColor: checked ? Theme.accent : (hovered ? Theme.text : Theme.text2)
    property string tip
    property bool filled: false     // solid circle, for primary transport buttons
    property string badge           // small text under the glyph, e.g. "10"

    implicitWidth: diameter
    implicitHeight: diameter
    focusPolicy: Qt.StrongFocus
    hoverEnabled: true
    Accessible.name: tip

    background: Rectangle {
        radius: width / 2
        color: control.filled ? (control.down ? Theme.accentHover : control.hovered ? Theme.accentHover : Theme.accent)
                              : (control.down ? Theme.pressed : control.hovered ? Theme.hover : "transparent")
        border.width: control.visualFocus ? Theme.s(2) : 0
        border.color: Theme.focus
        Behavior on color { ColorAnimation { duration: Theme.fast } }
    }

    contentItem: Item {
        Icon {
            anchors.centerIn: parent
            anchors.verticalCenterOffset: control.badge ? -Theme.s(1) : 0
            name: control.glyph
            size: control.iconSize
            color: control.filled ? Theme.accentText : control.iconColor
            Behavior on color { ColorAnimation { duration: Theme.fast } }
        }
        Text {
            visible: control.badge.length > 0
            anchors.centerIn: parent
            anchors.verticalCenterOffset: Theme.s(1)
            text: control.badge
            font.family: Theme.family
            font.pixelSize: Math.max(8, control.iconSize * 0.36)
            font.weight: Font.DemiBold
            color: control.iconColor
        }
    }

    scale: down ? 0.94 : 1
    Behavior on scale { NumberAnimation { duration: Theme.fast; easing.type: Easing.OutCubic } }

    T.ToolTip.visible: tip.length > 0 && hovered && !down
    T.ToolTip.delay: 600
    T.ToolTip.text: tip
}
