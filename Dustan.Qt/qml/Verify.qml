import QtQuick
import QtQuick.Controls.Basic as T
import Dustan

// Development aid (--verify-playback <list> --tour <folder>): plays each listed file through the
// real player, checks that it decodes, seeks, switches audio and shows subtitles, logs the
// results as JSON lines prefixed with "verify:" and saves one frame per file. Library data is
// only touched through normal progress saving.
Item {
    id: verify
    readonly property var app: T.ApplicationWindow.window
    readonly property var files: Platform.verifyFiles
    property int index: -1
    property var result: ({})
    property var queue: []
    property real started: 0

    function player() { return app.playerItem }
    function engine() { return app.playerItem ? app.playerItem.engine : null }
    function log(obj) { console.info("verify: " + JSON.stringify(obj)) }

    // Tiny sequencer: each step is [delayMs, fn]; fn may return false to finish the file early.
    function run(steps) { queue = steps; tick.interval = steps[0][0]; tick.restart() }
    Timer {
        id: tick
        onTriggered: {
            const step = verify.queue.shift()
            const keepGoing = step[1]()
            if (keepGoing === false) verify.queue = []
            if (verify.queue.length) { interval = verify.queue[0][0]; restart() }
            else verify.finishFile()
        }
    }

    function nextFile() {
        index++
        if (index >= files.length) { Qt.quit(); return }
        const path = files[index]
        result = { file: path }
        started = Date.now()
        app.playVideo({ path: path, showTitle: Platform.fileName(path), code: "", name: "", duration: 0, position: 0, watched: false, isMovie: true }, false)
        run([
            [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded],
            [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded],
            [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded], [500, waitLoaded],
            [3000, () => { result.positionAfter3s = round(engine().position); result.pausedFlag = engine().paused }],
            [10, () => { result.seekTarget = round(engine().duration * 0.5); engine().seek(engine().duration * 0.5) }],
            [3500, () => { result.positionAfterSeek = round(engine().position); result.seekOk = Math.abs(engine().position - result.seekTarget) < 8 }],
            [10, () => {
                const a = engine().audioTracks
                result.audioTracks = a.map(t => [t.lang || "", t.codec || "", t["demux-channel-count"] || 0, t.title || ""].join("/"))
                if (a.length > 1) { result.audioSwitchTo = a[1].id; engine().audioTrack = a[1].id }
            }],
            [1500, () => {
                if (result.audioSwitchTo !== undefined) result.audioSwitchOk = engine().audioTrack === result.audioSwitchTo
                const s = engine().subtitleTracks
                result.subtitleTracks = s.map(t => [t.lang || "", t.codec || "", t.external ? "external" : "embedded"].join("/"))
                if (s.length) { result.subtitleSelect = s[0].id; engine().subtitleTrack = s[0].id }
            }],
            [2500, () => {
                if (result.subtitleSelect !== undefined) result.subtitleSelectOk = engine().subtitleTrack === result.subtitleSelect
                result.stillPlaying = !engine().paused && !engine().ended
                result.positionEnd = round(engine().position)
                result.videoInfo = engine().videoInfo
                player().showControls = false
            }],
            [700, () => { Platform.saveWindow(app, Platform.tourDirectory + "/verify-" + (index + 1) + ".png") }],
        ])
    }
    function round(x) { return Math.round(x * 10) / 10 }
    function waitLoaded() {
        const e = engine()
        if (!e) return
        if (player().failure) { result.error = player().failure; return false }
        if (e.loaded && e.duration > 0 && e.position > 0.2) {
            if (result.loadMs === undefined) {
                result.loadMs = Date.now() - started
                result.duration = round(e.duration)
                // Skip the remaining wait steps.
                while (verify.queue.length && verify.queue[0][1] === waitLoaded) verify.queue.shift()
            }
        }
    }
    function finishFile() {
        if (result.loadMs === undefined && !result.error) result.error = "did not start within 9 s"
        log(result)
        if (player()) player().close()
        Qt.callLater(() => pause.restart())
    }
    Timer { id: pause; interval: 800; onTriggered: verify.nextFile() }

    Connections {
        target: Bridge
        function onReadyChanged() { if (Bridge.ready && verify.index < 0) verify.nextFile() }
    }
    Component.onCompleted: if (Bridge.ready) nextFile()
}
