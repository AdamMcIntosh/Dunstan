import QtQuick
import QtQuick.Controls.Basic as T
import Dustan

// Unobtrusive scrollbar that widens on hover.
T.ScrollBar {
    id: control
    implicitWidth: Theme.s(10)
    padding: Theme.s(2)
    policy: T.ScrollBar.AsNeeded
    minimumSize: 0.06
    contentItem: Rectangle {
        implicitWidth: control.hovered || control.pressed ? Theme.s(6) : Theme.s(4)
        radius: width / 2
        color: control.pressed ? Qt.rgba(1, 1, 1, 0.42) : Qt.rgba(1, 1, 1, control.hovered ? 0.3 : 0.16)
        opacity: control.active || control.hovered ? 1 : 0
        Behavior on opacity { NumberAnimation { duration: Theme.normal } }
        Behavior on implicitWidth { NumberAnimation { duration: Theme.fast } }
    }
    background: Item {}
}
