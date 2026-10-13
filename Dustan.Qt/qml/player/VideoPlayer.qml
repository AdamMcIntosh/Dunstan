import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Dialogs
import QtQuick.Layouts
import Dustan

// Full-window video playback for library videos and live TV channels.
FocusScope {
    id: player
    signal closed()

    readonly property var app: T.ApplicationWindow.window
    property var item: null              // library video (episode/film) being played
    property var channel: null           // { id, name } when watching live TV
    property var programme: null         // the live programme being shown
    property var channelList: []
    readonly property bool live: channel !== null
    property string title
    property string subtitle
    property bool completed: false
    property bool enteredFullScreen: false
    property var upNext: null
    property int countdown: 0
    property string failure: ""
    property bool controlsPinned: false
    property bool showControls: true

    focus: true
    readonly property alias engine: mpv

    // ---- entry points ---------------------------------------------------------------------------------
    function enterFullScreen() {
        if (app.prefs.videoFullScreen && !Platform.tourDirectory && app.visibility !== T.ApplicationWindow.FullScreen) {
            enteredFullScreen = true
            app.showFullScreen()
        }
    }
    function applyPreferences() {
        mpv.setPreferences(app.prefs.audioLanguages, app.prefs.subtitleLanguages, app.prefs.subtitlesOn)
        mpv.volume = app.prefs.videoVolume
    }
    function playVideo(video, resume) {
        channel = null
        programme = null
        item = video
        completed = false
        upNext = null
        failure = ""
        title = video.showTitle
        subtitle = video.isMovie ? "" : [video.code, video.name].filter(x => x).join("  ·  ")
        applyPreferences()
        enterFullScreen()
        if (!Platform.exists(video.path)) {
            failure = qsTr("This video isn't available: %1. Check that the NAS share is mounted.").arg(video.path)
            return
        }
        mpv.load(video.path, resume ? video.position : 0)
        poke()
    }
    function watchChannel(id) {
        item = null
        upNext = null
        failure = ""
        channel = { id: id, name: "" }
        applyPreferences()
        enterFullScreen()
        Bridge.call("tv.channels", {}, (result, error) => { if (!error) player.channelList = result })
        tune()
    }
    function tune() {
        if (!channel) return
        Bridge.call("tv.tune", { id: channel.id }, (result, error) => {
            if (error) { failure = error; return }
            failure = ""
            channel = { id: result.id, name: result.name }
            programme = result.programme
            title = result.programme.title
            subtitle = result.programme.subtitle
            boundary.interval = Math.max(500, result.programme.end - Date.now() + 400)
            boundary.restart()
            if (!result.programme.available) {
                failure = qsTr("“%1” is unavailable on the NAS. The channel moves on at %2.").arg(result.programme.title).arg(Theme.timeOfDay(result.programme.end))
                mpv.stop()
                return
            }
            mpv.load(result.programme.path, result.programme.offset)
            poke()
        })
    }
    function switchChannel(step) {
        if (!live || channelList.length < 2) return
        const index = channelList.findIndex(c => c.id === channel.id)
        const next = channelList[(index + step + channelList.length) % channelList.length]
        channel = { id: next.id, name: next.name }
        tune()
        app.toast(next.name)
    }
    function close() {
        saveProgress(false)
        mpv.stop()
        if (enteredFullScreen && app.visibility === T.ApplicationWindow.FullScreen) app.showNormal()
        closed()
    }

    // ---- progress ----------------------------------------------------------------------------------------
    function saveProgress(done) {
        if (live || !item) return
        if (!done && (!mpv.loaded || mpv.position < 1)) return
        Bridge.call("video.progress", { path: item.path, seconds: done ? 0 : mpv.position, completed: done })
    }
    Timer { interval: 10000; repeat: true; running: !player.live && mpv.loaded && !mpv.paused; onTriggered: player.saveProgress(false) }
    Timer { id: boundary; onTriggered: player.tune() }

    // ---- controls visibility -----------------------------------------------------------------------------
    function poke() {
        showControls = true
        hideTimer.restart()
    }
    readonly property bool controlsShown: showControls || mpv.paused || controlsPinned || failure.length > 0 || !mpv.loaded
    Timer { id: hideTimer; interval: 3200; onTriggered: player.showControls = false }

    Rectangle { anchors.fill: parent; color: "black" }

    MpvPlayer {
        id: mpv
        anchors.fill: parent
        // Keep subtitles above the transport while it is on screen.
        subtitleLift: player.controlsShown ? (bottom.height - Theme.s(10)) / Math.max(1, height) : 0
        onVolumeChanged: if (mpv.loaded) player.app.prefs.videoVolume = volume
        onEndReached: {
            if (player.live) return
            player.completed = true
            player.saveProgress(true)
            Bridge.call("video.after", { path: player.item.path }, (next, error) => {
                if (error || !next || !player.app.prefs.autoplayNext) return
                player.upNext = next
                player.countdown = 10
                countdownTimer.restart()
            })
        }
        onPlaybackFailed: message => {
            player.failure = player.live ? message + " " + qsTr("The channel moves on at the next programme.") : message
        }
    }

    Timer {
        id: countdownTimer
        interval: 1000; repeat: true
        running: false
        onTriggered: {
            if (--player.countdown <= 0) { stop(); player.playVideo(player.upNext, false) }
        }
    }

    MouseArea {
        anchors.fill: parent
        hoverEnabled: true
        acceptedButtons: Qt.LeftButton
        cursorShape: player.controlsShown ? Qt.ArrowCursor : Qt.BlankCursor
        onPositionChanged: player.poke()
        onClicked: clickTimer.restart()
        onDoubleClicked: { clickTimer.stop(); player.app.toggleFullScreen() }
        Timer { id: clickTimer; interval: 240; onTriggered: { mpv.togglePause(); flash.trigger(mpv.paused ? "play" : "pause") } }
    }

    // Centre feedback when toggling.
    Rectangle {
        id: flash
        anchors.centerIn: parent
        width: Theme.s(96); height: width; radius: width / 2
        color: Qt.rgba(0, 0, 0, 0.5)
        opacity: 0
        property string glyph: "play"
        function trigger(name) { glyph = name; flashAnim.restart() }
        Icon { anchors.centerIn: parent; anchors.horizontalCenterOffset: flash.glyph === "play" ? Theme.s(3) : 0; name: flash.glyph; size: Theme.s(40) }
        SequentialAnimation {
            id: flashAnim
            ParallelAnimation {
                NumberAnimation { target: flash; property: "opacity"; from: 0.95; to: 0; duration: 650; easing.type: Easing.InCubic }
                NumberAnimation { target: flash; property: "scale"; from: 0.9; to: 1.25; duration: 650; easing.type: Easing.OutCubic }
            }
        }
    }

    Spinner {
        anchors.centerIn: parent
        size: Theme.s(48)
        color: Theme.text
        visible: opacity > 0
        opacity: (mpv.buffering || (!mpv.loaded && player.failure === "" && mpv.source !== "")) && spinnerDelay.ready ? 0.9 : 0
        Behavior on opacity { NumberAnimation { duration: Theme.normal } }
        Timer {
            id: spinnerDelay
            property bool ready: false
            interval: 450
            running: mpv.buffering || !mpv.loaded
            onRunningChanged: if (!running) ready = false
            onTriggered: ready = true
        }
    }

    // ---- top bar -----------------------------------------------------------------------------------------
    Item {
        anchors { left: parent.left; right: parent.right; top: parent.top }
        height: Theme.s(150)
        opacity: player.controlsShown ? 1 : 0
        visible: opacity > 0
        Behavior on opacity { NumberAnimation { duration: Theme.slow; easing.type: Easing.OutCubic } }
        Rectangle {
            anchors.fill: parent
            gradient: Gradient {
                GradientStop { position: 0; color: Qt.rgba(0, 0, 0, 0.75) }
                GradientStop { position: 1; color: "transparent" }
            }
        }
        RowLayout {
            anchors { left: parent.left; right: parent.right; top: parent.top; margins: Theme.s(28) }
            spacing: Theme.s(18)
            IconButton { glyph: "back"; tip: qsTr("Close player"); iconColor: Theme.text; diameter: Theme.s(44); iconSize: Theme.s(22); onClicked: player.close() }
            ColumnLayout {
                Layout.fillWidth: true
                spacing: Theme.s(4)
                RowLayout {
                    spacing: Theme.s(10)
                    Rectangle {
                        visible: player.live
                        implicitWidth: liveLabel.implicitWidth + Theme.s(16); implicitHeight: Theme.s(22); radius: Theme.s(4)
                        color: Theme.live
                        Text { id: liveLabel; anchors.centerIn: parent; text: qsTr("LIVE"); color: "white"; font.family: Theme.family; font.pixelSize: Theme.overline; font.weight: Font.Bold; font.letterSpacing: 0.9 }
                    }
                    Text {
                        visible: player.live
                        text: player.channel ? player.channel.name.toUpperCase() : ""
                        color: Theme.accent
                        font.family: Theme.family; font.pixelSize: Theme.overline; font.weight: Font.DemiBold; font.letterSpacing: 1.2
                    }
                }
                Text {
                    Layout.fillWidth: true
                    text: player.title
                    color: Theme.text
                    font.family: Theme.family; font.pixelSize: Theme.s(24); font.weight: Font.DemiBold; font.letterSpacing: -0.3
                    elide: Text.ElideRight
                }
                Text {
                    Layout.fillWidth: true
                    visible: player.subtitle.length > 0
                    text: player.subtitle
                    color: Theme.text2
                    font.family: Theme.family; font.pixelSize: Theme.body
                    elide: Text.ElideRight
                }
            }
            Text {
                Layout.alignment: Qt.AlignTop
                visible: mpv.loaded
                text: mpv.videoInfo
                color: Theme.text3
                font.family: Theme.family; font.pixelSize: Theme.caption
            }
        }
    }

    // ---- bottom bar ---------------------------------------------------------------------------------------
    Item {
        id: bottom
        anchors { left: parent.left; right: parent.right; bottom: parent.bottom }
        height: Theme.s(170)
        opacity: player.controlsShown ? 1 : 0
        visible: opacity > 0
        Behavior on opacity { NumberAnimation { duration: Theme.slow; easing.type: Easing.OutCubic } }
        HoverHandler { onHoveredChanged: player.controlsPinned = hovered }
        Rectangle {
            anchors.fill: parent
            gradient: Gradient {
                GradientStop { position: 0; color: "transparent" }
                GradientStop { position: 1; color: Qt.rgba(0, 0, 0, 0.82) }
            }
        }
        ColumnLayout {
            anchors { left: parent.left; right: parent.right; bottom: parent.bottom; margins: Theme.s(28); bottomMargin: Theme.s(24) }
            spacing: Theme.s(10)

            // Timeline: seekable for library videos, read-only programme progress for live TV.
            RowLayout {
                Layout.fillWidth: true
                spacing: Theme.s(14)
                Text {
                    text: player.live && player.programme ? Theme.timeOfDay(player.programme.start)
                        : Theme.clock(timeline.dragging ? timeline.dragValue * mpv.duration : mpv.position)
                    color: Theme.text
                    font.family: Theme.family; font.pixelSize: Theme.label; font.weight: Font.Medium; font.features: { "tnum": 1 }
                }
                SeekBar {
                    id: timeline
                    Layout.fillWidth: true
                    interactive: !player.live && mpv.seekable
                    trackHeight: Theme.s(5)
                    value: player.live && player.programme
                           ? (Date.now() - player.programme.start) / (player.programme.end - player.programme.start) + clock.tick * 0
                           : mpv.duration > 0 ? mpv.position / mpv.duration : 0
                    onSeek: fraction => { mpv.seek(fraction * mpv.duration); player.poke() }
                    onStep: direction => { mpv.seekRelative(direction > 0 ? 30 : -10); player.poke() }
                    Rectangle {
                        // Scrub preview time
                        visible: timeline.hoverFraction >= 0 && timeline.interactive
                        x: Math.max(0, Math.min(timeline.width - width, timeline.width * timeline.hoverFraction - width / 2))
                        y: -height - Theme.s(10)
                        width: previewText.implicitWidth + Theme.s(16); height: Theme.s(26); radius: Theme.s(6)
                        color: Theme.surface3
                        Text { id: previewText; anchors.centerIn: parent; text: Theme.clock(timeline.hoverFraction * mpv.duration); color: Theme.text; font.family: Theme.family; font.pixelSize: Theme.caption; font.features: { "tnum": 1 } }
                    }
                }
                Text {
                    text: player.live && player.programme ? Theme.timeOfDay(player.programme.end)
                        : "−" + Theme.clock(Math.max(0, mpv.duration - mpv.position))
                    color: Theme.text2
                    font.family: Theme.family; font.pixelSize: Theme.label; font.features: { "tnum": 1 }
                }
            }

            RowLayout {
                Layout.fillWidth: true
                spacing: Theme.s(8)
                IconButton {
                    filled: true
                    focus: true
                    glyph: mpv.paused || player.completed ? "play" : "pause"
                    tip: mpv.paused ? qsTr("Play (Space)") : qsTr("Pause (Space)")
                    diameter: Theme.s(52); iconSize: Theme.s(24)
                    onClicked: { mpv.togglePause(); player.poke() }
                }
                IconButton {
                    visible: !player.live
                    glyph: "rewind"; badge: "10"; tip: qsTr("Back 10 seconds (←)")
                    iconColor: Theme.text; diameter: Theme.s(46); iconSize: Theme.s(26)
                    onClicked: { mpv.seekRelative(-10); player.poke() }
                }
                IconButton {
                    visible: !player.live
                    glyph: "forward"; badge: "30"; tip: qsTr("Forward 30 seconds (→)")
                    iconColor: Theme.text; diameter: Theme.s(46); iconSize: Theme.s(26)
                    onClicked: { mpv.seekRelative(30); player.poke() }
                }
                PillButton {
                    visible: player.live
                    compact: true
                    glyph: "live"
                    text: qsTr("Go live")
                    onClicked: { player.tune(); player.poke() }
                }
                IconButton {
                    visible: player.live && player.channelList.length > 1
                    glyph: "chevronUp"; tip: qsTr("Previous channel (Page Up)")
                    iconColor: Theme.text
                    onClicked: player.switchChannel(-1)
                }
                IconButton {
                    visible: player.live && player.channelList.length > 1
                    glyph: "chevronDown"; tip: qsTr("Next channel (Page Down)")
                    iconColor: Theme.text
                    onClicked: player.switchChannel(1)
                }
                Text {
                    Layout.leftMargin: Theme.s(10)
                    visible: !player.live && mpv.duration > 0
                    text: Theme.clock(mpv.position) + "  /  " + Theme.clock(mpv.duration)
                    color: Theme.text2
                    font.family: Theme.family; font.pixelSize: Theme.label; font.features: { "tnum": 1 }
                }
                Item { Layout.fillWidth: true }

                IconButton {
                    id: audioButton
                    glyph: "audio"; tip: qsTr("Audio track (A)")
                    iconColor: Theme.text; diameter: Theme.s(44); iconSize: Theme.s(22)
                    enabled: mpv.audioTracks.length > 0
                    opacity: enabled ? 1 : 0.4
                    onClicked: player.openAudioMenu(this)
                }
                IconButton {
                    id: subtitleButton
                    glyph: "subtitles"; tip: qsTr("Subtitles (S)")
                    checked: mpv.subtitleTrack > 0
                    iconColor: checked ? Theme.accent : Theme.text
                    diameter: Theme.s(44); iconSize: Theme.s(22)
                    onClicked: player.openSubtitleMenu(this)
                }
                IconButton {
                    visible: !player.live
                    glyph: "speed"; tip: qsTr("Playback speed")
                    badge: mpv.speed !== 1 ? mpv.speed + "×" : ""
                    iconColor: Theme.text; diameter: Theme.s(44); iconSize: Theme.s(22)
                    onClicked: player.openSpeedMenu(this)
                }
                IconButton {
                    glyph: mpv.muted || mpv.volume <= 0 ? "mute" : mpv.volume < 50 ? "volumeLow" : "volume"
                    tip: qsTr("Mute (M)")
                    iconColor: Theme.text; diameter: Theme.s(44); iconSize: Theme.s(22)
                    onClicked: { mpv.muted = !mpv.muted; player.poke() }
                }
                SeekBar {
                    Layout.preferredWidth: Theme.s(110)
                    value: mpv.muted ? 0 : mpv.volume / 100
                    fill: Theme.text
                    onMoved: fraction => { mpv.muted = false; mpv.volume = fraction * 100; player.poke() }
                    onSeek: fraction => { mpv.muted = false; mpv.volume = fraction * 100 }
                    onStep: direction => mpv.volume = Math.max(0, Math.min(100, mpv.volume + direction * 5))
                }
                IconButton {
                    glyph: player.app.visibility === T.ApplicationWindow.FullScreen ? "exitFullscreen" : "fullscreen"
                    tip: qsTr("Full screen (F)")
                    iconColor: Theme.text; diameter: Theme.s(44); iconSize: Theme.s(22)
                    onClicked: player.app.toggleFullScreen()
                }
            }
        }
    }
    Timer { id: clock; property int tick: 0; interval: 1000; repeat: true; running: player.live; onTriggered: tick++ }

    // ---- up next ------------------------------------------------------------------------------------------
    Rectangle {
        visible: player.upNext !== null && player.countdown > 0
        anchors { right: parent.right; bottom: parent.bottom; margins: Theme.s(32); bottomMargin: Theme.s(190) }
        width: Theme.s(400); height: upNextLayout.implicitHeight + Theme.s(32)
        radius: Theme.radiusL
        color: Qt.rgba(0.08, 0.086, 0.105, 0.94)
        border.width: 1; border.color: Theme.lineStrong
        ColumnLayout {
            id: upNextLayout
            anchors { fill: parent; margins: Theme.s(16) }
            spacing: Theme.s(12)
            RowLayout {
                spacing: Theme.s(14)
                VideoThumb {
                    Layout.preferredWidth: Theme.s(144); Layout.preferredHeight: Theme.s(81)
                    path: player.upNext ? player.upNext.path : ""
                    duration: player.upNext ? player.upNext.duration : 0
                    radius: Theme.radiusS
                }
                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: Theme.s(3)
                    Text { text: qsTr("UP NEXT IN %1").arg(player.countdown); color: Theme.accent; font.family: Theme.family; font.pixelSize: Theme.overline; font.weight: Font.DemiBold; font.letterSpacing: 1.1 }
                    Text { Layout.fillWidth: true; text: player.upNext ? player.upNext.name : ""; color: Theme.text; font.family: Theme.family; font.pixelSize: Theme.body; font.weight: Font.DemiBold; elide: Text.ElideRight }
                    Text { Layout.fillWidth: true; text: player.upNext ? player.upNext.code : ""; color: Theme.text3; font.family: Theme.family; font.pixelSize: Theme.caption }
                }
            }
            RowLayout {
                spacing: Theme.s(8)
                PillButton { kind: "primary"; glyph: "play"; text: qsTr("Play now"); onClicked: { countdownTimer.stop(); player.playVideo(player.upNext, false) } }
                PillButton { kind: "ghost"; text: qsTr("Cancel"); onClicked: { countdownTimer.stop(); player.countdown = 0 } }
            }
        }
    }

    // ---- failure ----------------------------------------------------------------------------------------
    Rectangle {
        visible: player.failure.length > 0
        anchors.centerIn: parent
        width: Math.min(parent.width - Theme.s(64), Theme.s(520))
        height: failureLayout.implicitHeight + Theme.s(48)
        radius: Theme.radiusL
        color: Qt.rgba(0.08, 0.086, 0.105, 0.96)
        border.width: 1; border.color: Theme.lineStrong
        ColumnLayout {
            id: failureLayout
            anchors { fill: parent; margins: Theme.s(24) }
            spacing: Theme.s(12)
            Icon { name: "warning"; color: Theme.live; size: Theme.s(28) }
            Text { Layout.fillWidth: true; text: qsTr("Playback problem"); color: Theme.text; font.family: Theme.family; font.pixelSize: Theme.heading; font.weight: Font.DemiBold }
            Text { Layout.fillWidth: true; text: player.failure; color: Theme.text2; font.family: Theme.family; font.pixelSize: Theme.body; wrapMode: Text.Wrap; lineHeight: 1.3 }
            RowLayout {
                spacing: Theme.s(8)
                PillButton { kind: "primary"; text: qsTr("Close player"); onClicked: player.close() }
                PillButton {
                    kind: "ghost"
                    text: player.live ? qsTr("Retry now") : qsTr("Try again")
                    onClicked: {
                        if (player.live) player.tune()
                        else { player.failure = ""; mpv.load(player.item.path, player.item.position) }
                    }
                }
            }
        }
    }

    // ---- menus -----------------------------------------------------------------------------------------
    readonly property var languageNames: ({
        eng: "English", en: "English", spa: "Spanish", es: "Spanish", fre: "French", fra: "French", fr: "French",
        ger: "German", deu: "German", de: "German", ita: "Italian", it: "Italian", jpn: "Japanese", ja: "Japanese",
        por: "Portuguese", pt: "Portuguese", rus: "Russian", ru: "Russian", chi: "Chinese", zho: "Chinese", zh: "Chinese",
        kor: "Korean", ko: "Korean", dut: "Dutch", nld: "Dutch", nl: "Dutch", swe: "Swedish", sv: "Swedish",
        nor: "Norwegian", no: "Norwegian", dan: "Danish", da: "Danish", fin: "Finnish", fi: "Finnish", pol: "Polish",
        pl: "Polish", hun: "Hungarian", cze: "Czech", ces: "Czech", gre: "Greek", ell: "Greek", tur: "Turkish",
        ara: "Arabic", heb: "Hebrew", hin: "Hindi", tha: "Thai", vie: "Vietnamese", und: ""
    })
    function channelLayout(count) {
        return count === 1 ? "Mono" : count === 2 ? "Stereo" : count === 6 ? "5.1" : count === 8 ? "7.1" : count > 0 ? count + " ch" : ""
    }
    function trackText(track, index) {
        const language = track.lang ? (languageNames[track.lang] !== undefined ? languageNames[track.lang] : track.lang.toUpperCase()) : ""
        const parts = [track.title || "", language].filter(x => x && x.length)
        const unique = parts.filter((x, i) => parts.indexOf(x) === i)
        return unique.length ? unique.join(" · ") : qsTr("Track %1").arg(index + 1)
    }
    function openAudioMenu(anchor) {
        menu.mode = "audio"
        menu.heading = qsTr("Audio")
        menu.items = mpv.audioTracks.map((t, i) => ({
            text: trackText(t, i),
            detail: [(t.codec || "").toUpperCase(), channelLayout(t["demux-channel-count"] || 0)].filter(x => x).join(" "),
            checked: t.id === mpv.audioTrack, id: t.id
        }))
        menu.openAt(anchor, true)
    }
    function openSubtitleMenu(anchor) {
        menu.mode = "subtitles"
        menu.heading = qsTr("Subtitles")
        const items = [{ text: qsTr("Off"), checked: mpv.subtitleTrack <= 0, id: 0 }]
        mpv.subtitleTracks.forEach((t, i) => items.push({
            text: trackText(t, i) + (t.forced ? qsTr(" (forced)") : ""),
            detail: t.external ? qsTr("File") : (t.codec || "").toUpperCase(),
            checked: t.id === mpv.subtitleTrack, id: t.id
        }))
        items.push({ separator: true })
        items.push({ text: qsTr("Load subtitle file…"), icon: "folder", id: -1 })
        menu.items = items
        menu.openAt(anchor, true)
    }
    function openSpeedMenu(anchor) {
        menu.mode = "speed"
        menu.heading = qsTr("Speed")
        menu.items = [0.5, 0.75, 1, 1.25, 1.5, 2].map(v => ({ text: v === 1 ? qsTr("Normal") : v + "×", checked: Math.abs(mpv.speed - v) < 0.01, value: v }))
        menu.openAt(anchor, true)
    }
    PopupMenu {
        id: menu
        property string mode
        onOpened: player.controlsPinned = true
        onClosed: { player.controlsPinned = false; player.poke(); player.forceActiveFocus() }
        onTriggered: (index, entry) => {
            if (mode === "audio") mpv.audioTrack = entry.id
            else if (mode === "speed") mpv.speed = entry.value
            else if (entry.id === -1) subtitleDialog.open()
            else mpv.subtitleTrack = entry.id
        }
    }
    FileDialog {
        id: subtitleDialog
        title: qsTr("Load subtitle file")
        nameFilters: [qsTr("Subtitles (*.srt *.ass *.ssa *.vtt *.sub)"), qsTr("All files (*)")]
        currentFolder: player.item ? Platform.fileUrl(player.item.path.substring(0, player.item.path.lastIndexOf("/"))) : ""
        onAccepted: mpv.addSubtitleFile(Platform.localPath(selectedFile))
    }

    // ---- keyboard -------------------------------------------------------------------------------------
    Keys.onPressed: event => {
        let handled = true
        switch (event.key) {
        case Qt.Key_Space:
        case Qt.Key_K:
        case Qt.Key_MediaTogglePlayPause:
        case Qt.Key_MediaPlay:
        case Qt.Key_MediaPause:
            mpv.togglePause(); flash.trigger(mpv.paused ? "play" : "pause"); break
        case Qt.Key_Left: if (!live) mpv.seekRelative(-10); break
        case Qt.Key_Right: if (!live) mpv.seekRelative(30); break
        case Qt.Key_J: if (!live) mpv.seekRelative(-10); break
        case Qt.Key_L: if (!live) mpv.seekRelative(10); break
        case Qt.Key_Up: mpv.volume = Math.min(100, mpv.volume + 5); break
        case Qt.Key_Down: mpv.volume = Math.max(0, mpv.volume - 5); break
        case Qt.Key_M: mpv.muted = !mpv.muted; break
        case Qt.Key_F: app.toggleFullScreen(); break
        case Qt.Key_A: mpv.cycleAudio(); break
        case Qt.Key_S:
        case Qt.Key_C: mpv.cycleSubtitles(); break
        case Qt.Key_PageUp: switchChannel(-1); break
        case Qt.Key_PageDown: switchChannel(1); break
        case Qt.Key_N: if (upNext) { countdownTimer.stop(); playVideo(upNext, false) } break
        case Qt.Key_Escape:
            if (app.visibility === T.ApplicationWindow.FullScreen && !enteredFullScreen) app.showNormal()
            else close()
            break
        case Qt.Key_Backspace:
        case Qt.Key_Back:
            close(); break
        default: handled = false
        }
        if (handled) { event.accepted = true; poke() }
    }

    Component.onDestruction: saveProgress(false)
}
