import QtQuick
import QtQuick.Effects
import Dustan

// Rounded artwork that fades in once decoded. Without an image it shows a generated cover:
// a muted gradient seeded from the name, with initials or an icon.
Item {
    id: root
    property url source
    property string name
    property real radius: Theme.radiusS
    property bool circle: false
    property string fallbackIcon       // e.g. "videos" for video tiles
    property string fallbackText       // title shown on icon fallbacks
    property int decodeWidth: Math.ceil(width * Screen.devicePixelRatio)
    property int decodeHeight: Math.ceil(height * Screen.devicePixelRatio)
    readonly property bool ready: image.status === Image.Ready
    readonly property real cornerRadius: circle ? Math.min(width, height) / 2 : radius

    Rectangle {
        anchors.fill: parent
        radius: root.cornerRadius
        visible: !root.ready || image.opacity < 1
        gradient: Gradient {
            GradientStop { position: 0; color: Theme.tint(root.name, root.fallbackIcon ? 0.17 : 0.30) }
            GradientStop { position: 1; color: Theme.tint(root.name, root.fallbackIcon ? 0.09 : 0.15) }
        }
        Text {
            visible: !root.fallbackIcon
            anchors.centerIn: parent
            text: Theme.initials(root.name)
            color: Qt.rgba(1, 1, 1, 0.5)
            font.family: Theme.family
            font.pixelSize: Math.max(10, parent.height * 0.3)
            font.weight: Font.DemiBold
            font.letterSpacing: -0.5
        }
        Column {
            visible: root.fallbackIcon.length > 0
            anchors.centerIn: parent
            width: parent.width - Theme.s(24)
            spacing: Theme.s(8)
            Icon {
                anchors.horizontalCenter: parent.horizontalCenter
                name: root.fallbackIcon
                size: Math.min(Theme.s(28), root.height * 0.25)
                color: Qt.rgba(1, 1, 1, 0.35)
            }
            Text {
                visible: root.fallbackText.length > 0 && root.height > Theme.s(90)
                width: parent.width
                horizontalAlignment: Text.AlignHCenter
                text: root.fallbackText
                color: Qt.rgba(1, 1, 1, 0.55)
                font.family: Theme.family
                font.pixelSize: Theme.label
                font.weight: Font.Medium
                elide: Text.ElideRight
                maximumLineCount: 2
                wrapMode: Text.Wrap
            }
        }
    }

    Image {
        id: image
        anchors.fill: parent
        source: root.source
        asynchronous: true
        cache: true
        smooth: true
        mipmap: true
        fillMode: Image.PreserveAspectCrop
        sourceSize: Qt.size(root.decodeWidth, root.decodeHeight)
        visible: false
        opacity: root.ready ? 1 : 0
        Behavior on opacity { NumberAnimation { duration: Theme.slow; easing.type: Easing.OutCubic } }
    }

    MultiEffect {
        anchors.fill: parent
        source: image
        visible: root.ready
        opacity: image.opacity
        maskEnabled: true
        maskSource: mask
        maskThresholdMin: 0.5
        maskSpreadAtMin: 1.0
    }

    Item {
        id: mask
        anchors.fill: parent
        layer.enabled: true
        visible: false
        Rectangle {
            anchors.fill: parent
            radius: root.cornerRadius
            antialiasing: true
        }
    }
}
