import QtQuick
import QtQuick.Effects
import Dustan

// Transient message at the bottom of the window.
Item {
    id: root
    property string message
    property string kind: "info"     // info | error | success
    width: Math.min(parent.width - Theme.s(48), Math.max(Theme.s(280), label.implicitWidth + Theme.s(72)))
    height: Math.max(Theme.s(48), label.implicitHeight + Theme.s(24))
    opacity: 0
    visible: opacity > 0
    y: shown ? 0 : Theme.s(12)
    property bool shown: false

    function show(text, type) {
        message = text
        kind = type || "info"
        shown = true
        hideTimer.restart()
    }

    states: State { when: root.shown; PropertyChanges { root.opacity: 1 } }
    transitions: Transition { NumberAnimation { properties: "opacity,y"; duration: Theme.normal; easing.type: Easing.OutCubic } }
    Behavior on y { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
    Timer { id: hideTimer; interval: root.kind === "error" ? 6500 : 3800; onTriggered: root.shown = false }

    Rectangle {
        anchors.fill: parent
        radius: Theme.radiusM
        color: Theme.surface3
        border.width: 1
        border.color: root.kind === "error" ? Qt.rgba(0.94, 0.36, 0.31, 0.5) : Theme.lineStrong
        layer.enabled: true
        layer.effect: MultiEffect { shadowEnabled: true; shadowColor: "#000000"; shadowOpacity: 0.5; shadowBlur: 0.8; shadowVerticalOffset: 6 }
    }
    Icon {
        id: glyph
        anchors { left: parent.left; leftMargin: Theme.s(16); verticalCenter: parent.verticalCenter }
        name: root.kind === "error" ? "warning" : root.kind === "success" ? "check" : "info"
        color: root.kind === "error" ? Theme.live : root.kind === "success" ? Theme.positive : Theme.accent
        size: Theme.s(18)
    }
    Text {
        id: label
        anchors { left: glyph.right; leftMargin: Theme.s(12); right: parent.right; rightMargin: Theme.s(18); verticalCenter: parent.verticalCenter }
        text: root.message
        color: Theme.text
        font.family: Theme.family
        font.pixelSize: Theme.body
        wrapMode: Text.Wrap
        maximumLineCount: 4
        elide: Text.ElideRight
    }
    MouseArea { anchors.fill: parent; onClicked: root.shown = false }
}
