import QtQuick
import Dustan

// Scrubbable timeline. `value` is 0–1; seek(fraction) fires on release (or key press).
FocusScope {
    id: root
    property real value: 0
    property real buffered: 0
    property bool interactive: true
    property color fill: Theme.accent
    property real trackHeight: Theme.s(4)
    readonly property bool dragging: area.pressed
    readonly property real hoverFraction: area.containsMouse ? fraction(area.mouseX) : -1
    readonly property real shown: dragging ? dragValue : value
    property real dragValue: 0
    signal seek(real fraction)
    signal moved(real fraction)     // continuous while dragging
    signal step(real direction)

    implicitHeight: Theme.s(22)
    activeFocusOnTab: interactive

    function fraction(x) { return Math.max(0, Math.min(1, x / width)) }

    Keys.onLeftPressed: event => { if (interactive) { root.step(-1); event.accepted = true } }
    Keys.onRightPressed: event => { if (interactive) { root.step(1); event.accepted = true } }

    readonly property bool emphasised: (area.containsMouse || dragging || activeFocus) && interactive

    Rectangle {
        id: track
        anchors.verticalCenter: parent.verticalCenter
        width: parent.width
        height: root.emphasised ? root.trackHeight * 1.5 : root.trackHeight
        radius: height / 2
        color: Qt.rgba(1, 1, 1, 0.14)
        Behavior on height { NumberAnimation { duration: Theme.fast } }
        Rectangle {
            width: parent.width * Math.max(0, Math.min(1, root.buffered))
            height: parent.height
            radius: parent.radius
            color: Qt.rgba(1, 1, 1, 0.12)
        }
        Rectangle {
            width: parent.width * Math.max(0, Math.min(1, root.shown))
            height: parent.height
            radius: parent.radius
            color: root.fill
        }
        Rectangle {
            visible: root.hoverFraction >= 0 && !root.dragging
            x: parent.width * root.hoverFraction - width / 2
            width: Theme.s(2); height: parent.height
            color: Qt.rgba(1, 1, 1, 0.5)
        }
    }
    Rectangle {
        id: handle
        width: Theme.s(14); height: width; radius: width / 2
        color: Theme.text
        x: track.width * Math.max(0, Math.min(1, root.shown)) - width / 2
        anchors.verticalCenter: parent.verticalCenter
        scale: root.emphasised ? 1 : 0
        border.width: root.activeFocus ? Theme.s(2) : 0
        border.color: Theme.focus
        Behavior on scale { NumberAnimation { duration: Theme.fast; easing.type: Easing.OutCubic } }
    }
    MouseArea {
        id: area
        anchors.fill: parent
        anchors.topMargin: -Theme.s(6)
        anchors.bottomMargin: -Theme.s(6)
        enabled: root.interactive
        hoverEnabled: true
        preventStealing: true
        cursorShape: root.interactive ? Qt.PointingHandCursor : Qt.ArrowCursor
        onPressed: mouse => { root.dragValue = root.fraction(mouse.x); root.moved(root.dragValue) }
        onPositionChanged: mouse => { if (pressed) { root.dragValue = root.fraction(mouse.x); root.moved(root.dragValue) } }
        onReleased: root.seek(root.dragValue)
    }
}
