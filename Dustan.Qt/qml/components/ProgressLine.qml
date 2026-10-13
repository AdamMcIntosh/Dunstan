import QtQuick
import Dustan

// Thin progress indicator for resume points.
Rectangle {
    id: root
    property real value: 0
    implicitHeight: Theme.s(3)
    radius: height / 2
    color: Qt.rgba(1, 1, 1, 0.16)
    Rectangle {
        width: parent.width * Math.max(0, Math.min(1, root.value))
        height: parent.height
        radius: parent.radius
        color: Theme.accent
    }
}
