import QtQuick
import QtQuick.Layouts
import Dustan

RowLayout {
    id: root
    property string text
    property string detail
    property string actionText
    signal action()
    spacing: Theme.s(12)

    Text {
        text: root.text
        color: Theme.text
        font.family: Theme.family
        font.pixelSize: Theme.heading
        font.weight: Font.DemiBold
        font.letterSpacing: -0.2
    }
    Text {
        visible: root.detail.length > 0
        text: root.detail
        color: Theme.text3
        font.family: Theme.family
        font.pixelSize: Theme.label
        Layout.alignment: Qt.AlignBaseline
    }
    Item { Layout.fillWidth: true }
    PillButton {
        visible: root.actionText.length > 0
        text: root.actionText
        kind: "ghost"
        compact: true
        onClicked: root.action()
    }
}
