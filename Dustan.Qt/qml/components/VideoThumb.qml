import QtQuick
import Dustan

// 16:9 video artwork: local poster art when present, otherwise a frame extracted by ffmpeg.
Artwork {
    id: root
    property string path
    property string poster
    property real duration: 0
    property real progress: 0
    property bool watched: false
    property bool showProgress: true

    name: path
    fallbackIcon: "videos"
    source: poster ? Platform.fileUrl(poster) : Platform.thumbnailUrl(path, duration)

    ProgressLine {
        visible: root.showProgress && root.progress > 0.005 && !root.watched
        anchors { left: parent.left; right: parent.right; bottom: parent.bottom; margins: Theme.s(10) }
        value: root.progress
    }

    Rectangle {
        visible: root.watched
        anchors { right: parent.right; top: parent.top; margins: Theme.s(8) }
        width: Theme.s(24); height: width; radius: width / 2
        color: Qt.rgba(0, 0, 0, 0.55)
        Icon { anchors.centerIn: parent; name: "check"; size: Theme.s(14); color: Theme.text; weight: 2.4 }
    }
}
