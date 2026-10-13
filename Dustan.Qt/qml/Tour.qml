import QtQuick
import QtQuick.Controls.Basic as T
import Dustan

// Development aid (--tour <folder>): visits each screen with the real libraries, saves a
// screenshot of each, then quits. It only reads data and plays media; it never edits libraries.
Item {
    id: tour
    readonly property var app: T.ApplicationWindow.window
    property int index: -1
    property var album: null
    property var show: null
    property var channels: []

    function shot(name) {
        const ok = Platform.saveWindow(app, Platform.tourDirectory + "/" + name + ".png")
        console.info("tour:", name, ok ? "saved" : "FAILED")
    }

    readonly property var steps: [
        { wait: 3500, run: () => { app.prefs.musicTab = 0; app.navigate("music") } },
        { wait: 2500, run: () => shot("01-music-albums") },
        { wait: 2500, run: () => { app.prefs.musicTab = 1 } },
        { wait: 400, run: () => shot("02-music-artists") },
        { wait: 3000, run: () => { app.prefs.musicTab = 2 } },
        { wait: 400, run: () => shot("03-music-songs") },
        { wait: 200, run: () => {
            app.prefs.musicTab = 0
            Bridge.call("music.albums", { sort: "recent" }, list => {
                tour.album = list.find(a => a.art && a.trackCount >= 8 && a.trackCount <= 16) || list[0]
                app.openAlbum(tour.album.id)
            })
        } },
        { wait: 3000, run: () => { if (tour.album) app.playMusic({ source: "album", id: tour.album.id, shuffle: false }) } },
        { wait: 4000, run: () => shot("04-album") },
        { wait: 300, run: () => app.navigate("nowplaying") },
        { wait: 3500, run: () => shot("05-now-playing") },
        { wait: 300, run: () => { app.music.command("pause"); app.navigate("videos") } },
        { wait: 25000, run: () => shot("06-videos") },
        { wait: 200, run: () => Bridge.call("video.overview", { filter: "shows" }, r => {
            tour.show = r.titles.find(t => t.seasonCount >= 3 && t.title.indexOf("Star Trek") >= 0) || r.titles.find(t => t.seasonCount >= 3) || r.titles[0]
            app.openShow(tour.show.key)
        }) },
        // Stress: open and close the page while dozens of thumbnail jobs are queued or running.
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 220, run: () => app.goBack() },
        { wait: 220, run: () => app.openShow(tour.show.key) },
        { wait: 12000, run: () => shot("07-show") },
        { wait: 300, run: () => app.navigate("tv") },
        { wait: 6000, run: () => shot("08-tv-guide") },
        { wait: 200, run: () => app.channelSheet.open() },
        { wait: 2500, run: () => {
            const sheet = app.channelSheet
            const show = sheet.sources.find(s => s.seasons.length >= 5) || sheet.sources[0]
            sheet.toggleExpanded(show.key)
            sheet.toggleSeason(show.seasons[0].key)
            sheet.toggleSeason(show.seasons[1].key)
        } },
        { wait: 800, run: () => shot("08b-new-channel") },
        { wait: 200, run: () => { app.channelSheet.close(); app.editAlbum(tour.album) } },
        { wait: 2000, run: () => shot("08c-edit-tags") },
        { wait: 200, run: () => app.tagSheet.close() },
        { wait: 300, run: () => app.navigate("settings") },
        { wait: 2500, run: () => shot("09-settings") },
        { wait: 300, run: () => app.playVideo(tour.show.nextUp, false) },
        { wait: 9000, run: () => app.playerItem.poke() },
        { wait: 600, run: () => shot("10-player") },
        { wait: 300, run: () => Bridge.call("tv.channels", {}, list => {
            tour.channels = list
            if (list.length) app.watchChannel(list[0].id)
        }) },
        { wait: 9000, run: () => app.playerItem.poke() },
        { wait: 600, run: () => { if (tour.channels.length) shot("11-live-tv") } },
        { wait: 500, run: () => Qt.quit() },
    ]

    Timer {
        id: timer
        onTriggered: {
            tour.steps[tour.index].run()
            tour.next()
        }
    }
    function next() {
        index++
        if (index >= steps.length) return
        timer.interval = steps[index].wait
        timer.start()
    }
    Connections {
        target: Bridge
        function onReadyChanged() { if (Bridge.ready && tour.index < 0) tour.next() }
    }
    Component.onCompleted: if (Bridge.ready) next()
}
