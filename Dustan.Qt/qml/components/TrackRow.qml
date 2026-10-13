import QtQuick
import QtQuick.Layouts
import Dustan

// One song in a list. Double-click, Enter or the leading play glyph starts playback.
FocusScope {
    id: row
    property int number: 0
    property string title
    property string subtitle
    property string album
    property real duration: 0
    property url art
    property bool showArt: false
    property bool showAlbum: false
    property bool current: false
    property bool playing: false
    property bool editable: true
    property bool highlighted: false
    signal activated()
    signal edit()
    signal albumClicked()

    implicitHeight: Theme.s(showArt ? 56 : 50)
    activeFocusOnTab: false
    Keys.onReturnPressed: activated()
    Keys.onEnterPressed: activated()

    Rectangle {
        anchors.fill: parent
        radius: Theme.radiusS
        color: area.pressed ? Theme.pressed : row.current ? Qt.rgba(0.89, 0.725, 0.42, 0.08)
             : (area.containsMouse || row.highlighted) ? Theme.hover : "transparent"
        border.width: row.activeFocus ? Theme.s(2) : 0
        border.color: Theme.focus
        Behavior on color { ColorAnimation { duration: Theme.fast } }
    }

    MouseArea {
        id: area
        anchors.fill: parent
        hoverEnabled: true
        acceptedButtons: Qt.LeftButton
        onClicked: row.forceActiveFocus()
        onDoubleClicked: row.activated()
    }

    RowLayout {
        anchors { fill: parent; leftMargin: Theme.s(10); rightMargin: Theme.s(10) }
        spacing: Theme.s(14)

        Item {
            Layout.preferredWidth: Theme.s(28)
            Layout.fillHeight: true
            EqualizerBars {
                anchors.centerIn: parent
                visible: row.current && !area.containsMouse
                playing: row.playing
                maxHeight: Theme.s(13)
            }
            Text {
                anchors.centerIn: parent
                visible: !row.current && !area.containsMouse && row.number > 0
                text: row.number
                color: Theme.text3
                font.family: Theme.family
                font.pixelSize: Theme.label
                font.features: { "tnum": 1 }
            }
            IconButton {
                anchors.centerIn: parent
                visible: area.containsMouse
                glyph: "play"
                diameter: Theme.s(30)
                iconSize: Theme.s(15)
                iconColor: Theme.text
                focusPolicy: Qt.NoFocus
                onClicked: row.activated()
            }
        }
        Artwork {
            visible: row.showArt
            Layout.preferredWidth: Theme.s(40)
            Layout.preferredHeight: Theme.s(40)
            source: row.art
            name: row.album
            radius: Theme.s(4)
        }
        ColumnLayout {
            Layout.fillWidth: true
            Layout.preferredWidth: 3
            spacing: Theme.s(2)
            Text {
                Layout.fillWidth: true
                text: row.title
                color: row.current ? Theme.accent : Theme.text
                font.family: Theme.family
                font.pixelSize: Theme.body
                font.weight: Font.Medium
                elide: Text.ElideRight
            }
            Text {
                Layout.fillWidth: true
                visible: row.subtitle.length > 0
                text: row.subtitle
                color: Theme.text3
                font.family: Theme.family
                font.pixelSize: Theme.caption
                elide: Text.ElideRight
            }
        }
        Text {
            visible: row.showAlbum && row.width > Theme.s(720)
            Layout.fillWidth: true
            Layout.preferredWidth: 2
            text: row.album
            color: albumArea.containsMouse ? Theme.text : Theme.text2
            font.family: Theme.family
            font.pixelSize: Theme.label
            font.underline: albumArea.containsMouse
            elide: Text.ElideRight
            MouseArea {
                id: albumArea
                anchors.fill: parent
                hoverEnabled: true
                cursorShape: Qt.PointingHandCursor
                onClicked: row.albumClicked()
            }
        }
        IconButton {
            visible: row.editable
            opacity: area.containsMouse || hovered ? 1 : 0
            glyph: "edit"
            tip: qsTr("Edit tags")
            diameter: Theme.s(30)
            iconSize: Theme.s(15)
            focusPolicy: Qt.NoFocus
            onClicked: row.edit()
        }
        Text {
            Layout.preferredWidth: Theme.s(52)
            horizontalAlignment: Text.AlignRight
            text: Theme.clock(row.duration)
            color: Theme.text3
            font.family: Theme.family
            font.pixelSize: Theme.label
            font.features: { "tnum": 1 }
        }
    }
}
