import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import QtQuick.Effects
import Dustan

// Modal dialog card over a dimmed scrim.
T.Popup {
    id: sheet
    property string title
    property string subtitle
    property real preferredWidth: Theme.s(620)
    property real preferredHeight: -1
    default property alias content: body.data
    property alias footer: footerRow.data

    parent: T.Overlay.overlay
    anchors.centerIn: parent
    modal: true
    focus: true
    closePolicy: T.Popup.CloseOnEscape
    width: Math.min(parent ? parent.width - Theme.s(48) : preferredWidth, preferredWidth)
    height: preferredHeight > 0 ? Math.min(parent.height - Theme.s(48), preferredHeight) : implicitHeight
    padding: 0

    T.Overlay.modal: Rectangle {
        color: Theme.scrim
        Behavior on opacity { NumberAnimation { duration: Theme.normal } }
    }
    enter: Transition {
        NumberAnimation { property: "opacity"; from: 0; to: 1; duration: Theme.normal }
        NumberAnimation { property: "scale"; from: 0.97; to: 1; duration: Theme.slow; easing.type: Easing.OutCubic }
    }
    exit: Transition { NumberAnimation { property: "opacity"; to: 0; duration: Theme.fast } }

    background: Rectangle {
        radius: Theme.radiusL
        color: Theme.surface
        border.width: 1
        border.color: Theme.lineStrong
        layer.enabled: true
        layer.effect: MultiEffect { shadowEnabled: true; shadowColor: "#000000"; shadowOpacity: 0.6; shadowBlur: 1.0; shadowVerticalOffset: 16 }
    }

    contentItem: ColumnLayout {
        spacing: 0
        RowLayout {
            Layout.fillWidth: true
            Layout.margins: Theme.s(24)
            Layout.bottomMargin: Theme.s(12)
            spacing: Theme.s(12)
            ColumnLayout {
                Layout.fillWidth: true
                spacing: Theme.s(4)
                Text {
                    text: sheet.title
                    color: Theme.text
                    font.family: Theme.family
                    font.pixelSize: Theme.heading
                    font.weight: Font.DemiBold
                    Layout.fillWidth: true
                    elide: Text.ElideRight
                }
                Text {
                    visible: sheet.subtitle.length > 0
                    text: sheet.subtitle
                    color: Theme.text3
                    font.family: Theme.family
                    font.pixelSize: Theme.label
                    Layout.fillWidth: true
                    wrapMode: Text.Wrap
                }
            }
            IconButton { glyph: "close"; tip: qsTr("Close"); onClicked: sheet.close(); Layout.alignment: Qt.AlignTop }
        }
        Item {
            id: body
            Layout.fillWidth: true
            Layout.fillHeight: true
            Layout.leftMargin: Theme.s(24)
            Layout.rightMargin: Theme.s(24)
            implicitHeight: childrenRect.height
        }
        Rectangle { Layout.fillWidth: true; Layout.topMargin: Theme.s(16); Layout.preferredHeight: 1; color: Theme.line; visible: footerRow.children.length > 0 }
        RowLayout {
            id: footerRow
            visible: children.length > 0
            Layout.fillWidth: true
            Layout.margins: Theme.s(16)
            Layout.leftMargin: Theme.s(24)
            Layout.rightMargin: Theme.s(24)
            spacing: Theme.s(10)
        }
    }
}
