import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Effects
import Dustan

// Lightweight menu. items: [{ text, detail, icon, checked, enabled, separator }]
T.Popup {
    id: menu
    property var items: []
    property string heading
    signal triggered(int index, var item)

    padding: Theme.s(6)
    focus: true
    closePolicy: T.Popup.CloseOnEscape | T.Popup.CloseOnPressOutside
    implicitWidth: Math.max(Theme.s(220), list.contentItemWidth + Theme.s(12))

    function openAt(item, alignRight) {
        const p = item.mapToItem(parent, 0, 0)
        x = alignRight ? p.x + item.width - width : p.x
        y = p.y + item.height + Theme.s(6)
        if (y + implicitHeight > parent.height - Theme.s(12)) y = p.y - implicitHeight - Theme.s(6)
        open()
    }

    enter: Transition {
        NumberAnimation { property: "opacity"; from: 0; to: 1; duration: Theme.fast }
        NumberAnimation { property: "scale"; from: 0.96; to: 1; duration: Theme.normal; easing.type: Easing.OutCubic }
    }
    exit: Transition { NumberAnimation { property: "opacity"; to: 0; duration: Theme.fast } }

    background: Item {
        Rectangle {
            id: panel
            anchors.fill: parent
            radius: Theme.radiusM
            color: Theme.surface2
            border.width: 1
            border.color: Theme.lineStrong
            layer.enabled: true
            layer.effect: MultiEffect { shadowEnabled: true; shadowColor: "#000000"; shadowOpacity: 0.55; shadowBlur: 0.9; shadowVerticalOffset: 8 }
        }
    }

    contentItem: Column {
        Text {
            visible: menu.heading.length > 0
            text: menu.heading.toUpperCase()
            leftPadding: Theme.s(12); topPadding: Theme.s(8); bottomPadding: Theme.s(6)
            color: Theme.text3
            font.family: Theme.family
            font.pixelSize: Theme.overline
            font.weight: Font.DemiBold
            font.letterSpacing: 1.1
        }
        ListView {
            id: list
            property real contentItemWidth: Theme.s(220)
            width: parent.width
            implicitHeight: Math.min(contentHeight, Theme.s(420))
            clip: true
            model: menu.items
            focus: true
            keyNavigationEnabled: true
            currentIndex: -1
            boundsBehavior: Flickable.StopAtBounds
            T.ScrollBar.vertical: ThinScrollBar {}
            Keys.onReturnPressed: if (currentIndex >= 0) activate(currentIndex)
            Keys.onEnterPressed: if (currentIndex >= 0) activate(currentIndex)
            function activate(i) {
                const item = menu.items[i]
                if (!item || item.separator || item.enabled === false) return
                menu.close()
                menu.triggered(i, item)
            }
            delegate: Item {
                id: entry
                required property int index
                required property var modelData
                width: ListView.view.width
                height: modelData.separator ? Theme.s(9) : Theme.s(38)
                Component.onCompleted: list.contentItemWidth = Math.max(list.contentItemWidth, label.implicitWidth + detailText.implicitWidth + Theme.s(90))
                Rectangle {
                    visible: entry.modelData.separator === true
                    anchors.centerIn: parent
                    width: parent.width - Theme.s(12); height: 1
                    color: Theme.line
                }
                Rectangle {
                    visible: !entry.modelData.separator
                    anchors.fill: parent
                    radius: Theme.radiusS
                    color: area.pressed ? Theme.pressed : (area.containsMouse || list.currentIndex === entry.index) ? Theme.hover : "transparent"
                }
                Row {
                    visible: !entry.modelData.separator
                    anchors { left: parent.left; leftMargin: Theme.s(10); right: parent.right; rightMargin: Theme.s(12); verticalCenter: parent.verticalCenter }
                    spacing: Theme.s(10)
                    opacity: entry.modelData.enabled === false ? 0.4 : 1
                    Item {
                        width: Theme.s(18); height: Theme.s(18)
                        anchors.verticalCenter: parent.verticalCenter
                        Icon {
                            anchors.centerIn: parent
                            name: entry.modelData.checked ? "check" : (entry.modelData.icon || "")
                            size: Theme.s(16)
                            color: entry.modelData.checked ? Theme.accent : Theme.text2
                            weight: entry.modelData.checked ? 2.2 : 1.75
                        }
                    }
                    Text {
                        id: label
                        anchors.verticalCenter: parent.verticalCenter
                        text: entry.modelData.text || ""
                        color: entry.modelData.checked ? Theme.text : Theme.text
                        font.family: Theme.family
                        font.pixelSize: Theme.body
                        font.weight: entry.modelData.checked ? Font.DemiBold : Font.Normal
                    }
                    Text {
                        id: detailText
                        anchors.verticalCenter: parent.verticalCenter
                        text: entry.modelData.detail || ""
                        color: Theme.text3
                        font.family: Theme.family
                        font.pixelSize: Theme.caption
                    }
                }
                MouseArea {
                    id: area
                    anchors.fill: parent
                    hoverEnabled: true
                    enabled: !entry.modelData.separator
                    onClicked: list.activate(entry.index)
                }
            }
        }
    }
}
