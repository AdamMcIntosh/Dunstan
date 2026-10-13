import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Dialogs
import QtQuick.Layouts
import Dustan

// Edits tags for one track or a whole album, written to the files on the NAS.
Sheet {
    id: sheet
    readonly property var app: T.ApplicationWindow.window
    property string scope: "track"
    property var track: null
    property var album: null
    property url preview
    property string artworkFile: ""
    property bool removeArtwork: false
    property bool busy: false
    property string error: ""
    readonly property bool albumScope: scope === "album"

    title: albumScope ? qsTr("Edit album") : qsTr("Edit track")
    subtitle: albumScope
              ? qsTr("Changes are written to every track of this album on the NAS. The album artist is also written as each track's artist, except for Various Artists compilations.")
              : (track ? track.file : "")
    preferredWidth: Theme.s(760)

    function reset() {
        error = ""
        artworkFile = ""
        removeArtwork = false
        busy = false
    }
    function openTrack(t) {
        reset()
        scope = "track"
        track = t
        album = null
        titleField.text = t.title
        artistField.text = t.artist
        albumField.text = t.album
        albumArtistField.text = t.albumArtist
        trackField.text = t.trackNumber > 0 ? String(t.trackNumber) : ""
        discField.text = t.discNumber > 0 ? String(t.discNumber) : ""
        yearField.text = t.year > 0 ? String(t.year) : ""
        genreField.text = t.genre
        preview = t.art ? Platform.fileUrl(t.art) : ""
        open()
    }
    function openAlbum(a) {
        reset()
        scope = "album"
        album = a
        track = null
        albumField.text = a.name
        albumArtistField.text = a.artist
        yearField.text = a.year > 0 ? String(a.year) : ""
        genreField.text = ""
        preview = a.art ? Platform.fileUrl(a.art) : ""
        // The genre lives on tracks; read it from the album.
        Bridge.call("music.album", { id: a.id }, result => { if (result) genreField.text = result.genre })
        open()
    }
    function save() {
        error = ""
        busy = true
        const params = {
            albumId: albumScope ? album.id : (track.albumId || 0),
            album: albumField.text, albumArtist: albumArtistField.text,
            year: parseInt(yearField.text) || 0, genre: genreField.text,
            artworkFile: artworkFile, removeArtwork: removeArtwork
        }
        if (!albumScope) {
            params.trackId = track.id
            params.title = titleField.text
            params.artist = artistField.text
            params.trackNumber = parseInt(trackField.text) || 0
            params.discNumber = parseInt(discField.text) || 0
        }
        Bridge.call(albumScope ? "music.editAlbum" : "music.editTrack", params, (result, err) => {
            busy = false
            if (err) { error = err; return }
            if (result.failed > 0 && result.succeeded === 0) { error = result.errors.slice(0, 4).join("\n"); return }
            sheet.close()
            if (result.failed > 0)
                app.toast(qsTr("Saved %1 files; %2 could not be written: %3").arg(result.succeeded).arg(result.failed).arg(result.errors[0] || ""), "error")
            else
                app.toast(qsTr("Tags saved to %1.").arg(Theme.plural(result.succeeded, qsTr("file"))), "success")
        })
    }

    RowLayout {
        width: parent.width
        spacing: Theme.s(24)

        ColumnLayout {
            Layout.alignment: Qt.AlignTop
            Layout.preferredWidth: Theme.s(176)
            Layout.maximumWidth: Theme.s(176)
            spacing: Theme.s(10)
            Artwork {
                Layout.preferredWidth: Theme.s(176)
                Layout.preferredHeight: Theme.s(176)
                source: sheet.removeArtwork ? "" : sheet.preview
                name: albumField.text
                radius: Theme.radiusM
            }
            PillButton {
                Layout.fillWidth: true
                compact: true
                glyph: "image"
                text: qsTr("Choose image…")
                onClicked: imageDialog.open()
            }
            PillButton {
                Layout.fillWidth: true
                compact: true
                kind: "ghost"
                glyph: "trash"
                text: sheet.removeArtwork ? qsTr("Keep artwork") : qsTr("Remove artwork")
                onClicked: { sheet.removeArtwork = !sheet.removeArtwork; if (sheet.removeArtwork) sheet.artworkFile = "" }
            }
        }

        GridLayout {
            Layout.fillWidth: true
            Layout.alignment: Qt.AlignTop
            columns: 2
            columnSpacing: Theme.s(14)
            rowSpacing: Theme.s(12)
            Field { id: titleField; visible: !sheet.albumScope; label: qsTr("Title"); Layout.fillWidth: true; Layout.columnSpan: 2 }
            Field { id: artistField; visible: !sheet.albumScope; label: qsTr("Artist"); Layout.fillWidth: true; Layout.columnSpan: 2 }
            Field { id: albumField; label: qsTr("Album"); Layout.fillWidth: true; Layout.columnSpan: 2 }
            Field { id: albumArtistField; label: qsTr("Album artist"); Layout.fillWidth: true; Layout.columnSpan: 2 }
            Field { id: trackField; visible: !sheet.albumScope; label: qsTr("Track number"); numeric: true; Layout.fillWidth: true }
            Field { id: discField; visible: !sheet.albumScope; label: qsTr("Disc number"); numeric: true; Layout.fillWidth: true }
            Field { id: yearField; label: qsTr("Year"); numeric: true; Layout.fillWidth: true }
            Field { id: genreField; label: qsTr("Genre"); Layout.fillWidth: true }
        }
    }

    footer: [
        Text {
            Layout.fillWidth: true
            text: sheet.error
            color: Theme.live
            font.family: Theme.family; font.pixelSize: Theme.label
            wrapMode: Text.Wrap
            maximumLineCount: 4
            elide: Text.ElideRight
        },
        PillButton { text: qsTr("Cancel"); kind: "ghost"; enabled: !sheet.busy; onClicked: sheet.close() },
        PillButton { text: sheet.busy ? qsTr("Saving…") : qsTr("Save tags"); kind: "primary"; enabled: !sheet.busy; onClicked: sheet.save() }
    ]

    FileDialog {
        id: imageDialog
        title: qsTr("Choose artwork")
        nameFilters: [qsTr("Images (*.jpg *.jpeg *.png)")]
        onAccepted: {
            sheet.artworkFile = Platform.localPath(selectedFile)
            sheet.preview = selectedFile
            sheet.removeArtwork = false
        }
    }
}
