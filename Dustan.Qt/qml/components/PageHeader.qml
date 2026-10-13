import QtQuick
import QtQuick.Layouts
import Dustan

// Page title row with optional back button and trailing actions.
Item {
    id: root
    property string title
    property string subtitle
    property bool canGoBack: false
    default property alias actions: actionRow.data
    signal back()

    implicitHeight: Theme.s(84)

    RowLayout {
        anchors.fill: parent
        spacing: Theme.s(14)
        IconButton {
            visible: root.canGoBack
            glyph: "back"
            tip: qsTr("Back")
            onClicked: root.back()
        }
        ColumnLayout {
            spacing: Theme.s(2)
            Layout.fillWidth: true
            Text {
                text: root.title
                color: Theme.text
                font.family: Theme.family
                font.pixelSize: Theme.title
                font.weight: Font.DemiBold
                font.letterSpacing: -0.5
                elide: Text.ElideRight
                Layout.fillWidth: true
            }
            Text {
                visible: root.subtitle.length > 0
                text: root.subtitle
                color: Theme.text3
                font.family: Theme.family
                font.pixelSize: Theme.label
                elide: Text.ElideRight
                Layout.fillWidth: true
            }
        }
        Row {
            id: actionRow
            spacing: Theme.s(10)
            Layout.alignment: Qt.AlignVCenter
        }
    }
}
