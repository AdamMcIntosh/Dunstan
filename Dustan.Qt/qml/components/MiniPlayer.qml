import QtQuick
import QtQuick.Layouts
import Dustan

// Persistent music transport along the bottom of the window.
Rectangle {
    id: bar
    property var music
    signal openNowPlaying()
    signal openAlbum(var albumId)

    color: Theme.surface
    implicitHeight: Theme.s(84)

    readonly property var track: music ? music.track : null

    Rectangle { anchors { left: parent.left; right: parent.right; top: parent.top } height: 1; color: Theme.line }

    RowLayout {
        anchors { fill: parent; leftMargin: Theme.s(18); rightMargin: Theme.s(22) }
        spacing: Theme.s(20)

        // Now playing summary
        Item {
            Layout.preferredWidth: Math.max(Theme.s(220), bar.width * 0.28)
            Layout.fillHeight: true
            RowLayout {
                anchors.fill: parent
                spacing: Theme.s(14)
                Artwork {
                    Layout.preferredWidth: Theme.s(54)
                    Layout.preferredHeight: Theme.s(54)
                    source: bar.track && bar.track.art ? Platform.fileUrl(bar.track.art) : ""
                    name: bar.track ? bar.track.album : ""
                    radius: Theme.s(5)
                    MouseArea { anchors.fill: parent; cursorShape: Qt.PointingHandCursor; onClicked: bar.openNowPlaying() }
                }
                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: Theme.s(3)
                    Text {
                        Layout.fillWidth: true
                        text: bar.track ? bar.track.title : ""
                        color: Theme.text
                        font.family: Theme.family
                        font.pixelSize: Theme.body
                        font.weight: Font.DemiBold
                        elide: Text.ElideRight
                        MouseArea { anchors.fill: parent; cursorShape: Qt.PointingHandCursor; onClicked: bar.openNowPlaying() }
                    }
                    Text {
                        Layout.fillWidth: true
                        text: bar.track ? bar.track.artist + "  ·  " + bar.track.album : ""
                        color: Theme.text3
                        font.family: Theme.family
                        font.pixelSize: Theme.caption
                        elide: Text.ElideRight
                        MouseArea {
                            anchors.fill: parent
                            cursorShape: Qt.PointingHandCursor
                            onClicked: if (bar.track && bar.track.albumId) bar.openAlbum(bar.track.albumId)
                        }
                    }
                }
            }
        }

        // Transport and timeline
        ColumnLayout {
            Layout.fillWidth: true
            Layout.maximumWidth: Theme.s(640)
            Layout.alignment: Qt.AlignVCenter
            spacing: Theme.s(2)
            Row {
                Layout.alignment: Qt.AlignHCenter
                spacing: Theme.s(10)
                IconButton {
                    anchors.verticalCenter: parent.verticalCenter
                    glyph: "shuffle"; checked: bar.music.shuffle; tip: qsTr("Shuffle")
                    iconSize: Theme.s(17)
                    onClicked: bar.music.command("shuffle")
                }
                IconButton {
                    anchors.verticalCenter: parent.verticalCenter
                    glyph: "previous"; tip: qsTr("Previous"); iconSize: Theme.s(18)
                    onClicked: bar.music.command("previous")
                }
                IconButton {
                    anchors.verticalCenter: parent.verticalCenter
                    filled: true
                    glyph: bar.music.playing ? "pause" : "play"
                    tip: bar.music.playing ? qsTr("Pause") : qsTr("Play")
                    diameter: Theme.s(40); iconSize: Theme.s(19)
                    onClicked: bar.music.command("toggle")
                }
                IconButton {
                    anchors.verticalCenter: parent.verticalCenter
                    glyph: "next"; tip: qsTr("Next"); iconSize: Theme.s(18)
                    onClicked: bar.music.command("next")
                }
                IconButton {
                    anchors.verticalCenter: parent.verticalCenter
                    glyph: bar.music.repeat === "one" ? "repeatOne" : "repeat"
                    checked: bar.music.repeat !== "off"
                    tip: bar.music.repeat === "one" ? qsTr("Repeat one") : bar.music.repeat === "all" ? qsTr("Repeat all") : qsTr("Repeat off")
                    iconSize: Theme.s(17)
                    onClicked: bar.music.command("repeat")
                }
            }
            RowLayout {
                Layout.fillWidth: true
                spacing: Theme.s(10)
                Text {
                    text: Theme.clock(seek.dragging ? seek.dragValue * bar.music.duration : bar.music.position)
                    color: Theme.text3
                    font.family: Theme.family
                    font.pixelSize: Theme.caption
                    font.features: { "tnum": 1 }
                    Layout.preferredWidth: Theme.s(44)
                    horizontalAlignment: Text.AlignRight
                }
                SeekBar {
                    id: seek
                    Layout.fillWidth: true
                    value: bar.music.duration > 0 ? bar.music.position / bar.music.duration : 0
                    interactive: bar.music.duration > 0
                    onSeek: fraction => bar.music.seek(fraction * bar.music.duration)
                    onStep: direction => bar.music.seek(bar.music.position + direction * 10)
                }
                Text {
                    text: Theme.clock(bar.music.duration)
                    color: Theme.text3
                    font.family: Theme.family
                    font.pixelSize: Theme.caption
                    font.features: { "tnum": 1 }
                    Layout.preferredWidth: Theme.s(44)
                }
            }
        }

        // Queue and volume
        RowLayout {
            Layout.preferredWidth: Math.max(Theme.s(200), bar.width * 0.2)
            Layout.alignment: Qt.AlignVCenter | Qt.AlignRight
            spacing: Theme.s(8)
            Item { Layout.fillWidth: true }
            IconButton {
                glyph: "queue"; tip: qsTr("Now Playing and queue"); iconSize: Theme.s(18)
                onClicked: bar.openNowPlaying()
            }
            IconButton {
                glyph: bar.music.volume <= 0.001 ? "mute" : bar.music.volume < 0.5 ? "volumeLow" : "volume"
                tip: qsTr("Mute"); iconSize: Theme.s(18)
                onClicked: bar.music.toggleMute()
            }
            SeekBar {
                Layout.preferredWidth: Theme.s(100)
                value: bar.music.volume
                fill: Theme.text2
                onMoved: fraction => bar.music.setVolume(fraction)
                onSeek: fraction => bar.music.setVolume(fraction)
                onStep: direction => bar.music.setVolume(Math.max(0, Math.min(1, bar.music.volume + direction * 0.05)))
            }
        }
    }
}
