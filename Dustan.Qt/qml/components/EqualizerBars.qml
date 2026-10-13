import QtQuick
import Dustan

// Three animated bars marking the playing track.
Row {
    id: root
    property bool playing: true
    property color color: Theme.accent
    property real barWidth: Theme.s(3)
    property real maxHeight: Theme.s(14)
    spacing: Theme.s(2)
    height: maxHeight

    Repeater {
        model: 3
        Rectangle {
            required property int index
            width: root.barWidth
            radius: width / 2
            color: root.color
            anchors.bottom: parent.bottom
            height: root.maxHeight * 0.35
            SequentialAnimation on height {
                running: root.playing
                loops: Animation.Infinite
                NumberAnimation { to: root.maxHeight * [0.95, 0.6, 0.8][index]; duration: [380, 290, 340][index]; easing.type: Easing.InOutSine }
                NumberAnimation { to: root.maxHeight * [0.3, 0.9, 0.4][index]; duration: [320, 360, 280][index]; easing.type: Easing.InOutSine }
            }
        }
    }
}
