import QtQuick
import QtQuick.Controls.Basic as T
import QtQuick.Layouts
import QtCore
import Dustan

T.ApplicationWindow {
    id: app
    width: 1440
    height: 900
    minimumWidth: 900
    minimumHeight: 600
    visible: true
    title: "Dustan"
    color: Theme.bg
    font.family: Theme.family
    font.pixelSize: Theme.body

    // ---- preferences (stored separately from the libraries, in Qt settings) ---------------------
    Settings {
        id: prefs
        category: "Interface"
        property string interfaceSize: "auto"
        property bool videoFullScreen: true
        property bool autoplayNext: true
        property string audioLanguages: ""
        property string subtitleLanguages: ""
        property bool subtitlesOn: true
        property real videoVolume: 100
        property string albumSort: "artist"
        property int musicTab: 0
    }
    property alias prefs: prefs

    // ---- interface scale: desktop by default, larger on big screens or in TV mode ---------------
    readonly property string sizeMode: Platform.interfaceSizeOverride || prefs.interfaceSize
    readonly property real autoScale: width > 2200 ? Math.min(2.0, width / 2200) : 1.0
    readonly property real chosenScale: ({ compact: 0.9, standard: 1.0, large: 1.2, tv: 1.5 })[sizeMode] || 1.0
    Binding { target: Theme; property: "scale"; value: app.sizeMode === "auto" ? app.autoScale : app.chosenScale * app.autoScale }

    readonly property bool compactRail: width < Theme.s(1180)
    readonly property real pagePadding: Theme.s(width < Theme.s(1100) ? 24 : 40)

    // ---- library summary ---------------------------------------------------------------------------
    property var info: ({})
    signal libraryChanged(string kind)
    function refreshInfo() {
        Bridge.call("app.info", {}, (result, error) => {
            if (error) return
            app.info = result
            Platform.setDataDirectory(result.dataDirectory)
        })
    }

    // ---- music playback (lives in the bridge; mirrored here) -----------------------------------------
    QtObject {
        id: musicState
        property var track: null
        property bool playing: false
        property real position: 0
        property real duration: 0
        property real volume: 0.8
        property bool shuffle: false
        property string repeat: "off"
        property int index: -1
        property int queueLength: 0
        property int queueRevision: 0
        property real lastVolume: 0.8
        property string engineError: ""
        readonly property bool active: track !== null

        function apply(state) {
            if (!state) return
            const changedTrack = (track ? track.id : -1) !== (state.track ? state.track.id : -1)
            track = state.track
            playing = state.playing
            duration = state.duration
            if (!seekGuard.running) position = state.position
            volume = state.volume
            shuffle = state.shuffle
            repeat = state.repeat
            if (changedTrack || index !== state.index || queueLength !== state.queueLength) queueRevision++
            index = state.index
            queueLength = state.queueLength
            engineError = state.engineError || ""
        }
        function command(action) {
            Bridge.call("player.command", { action: action }, (result, error) => error ? app.toast(error, "error") : apply(result))
        }
        function seek(seconds) {
            position = Math.max(0, Math.min(duration, seconds))
            seekGuard.restart()
            Bridge.call("player.seek", { seconds: position })
        }
        function setVolume(value) {
            volume = value
            if (value > 0.001) lastVolume = value
            Bridge.call("player.volume", { value: value })
        }
        function toggleMute() { setVolume(volume > 0.001 ? 0 : (lastVolume > 0.001 ? lastVolume : 0.8)) }
        function jump(i) { Bridge.call("player.jump", { index: i }) }
    }
    property alias music: musicState
    readonly property var playerItem: playerLoader.item
    property var channelSheet: null          // set by the TV guide; used by the development tour
    readonly property alias tagSheet: tagSheet

    Timer { id: seekGuard; interval: 900 }
    Timer {
        // Smooth the clock between bridge updates.
        interval: 250; repeat: true
        running: musicState.playing
        onTriggered: if (!seekGuard.running) musicState.position = Math.min(musicState.duration, musicState.position + 0.25)
    }

    Connections {
        target: Bridge
        function onReadyChanged() {
            if (!Bridge.ready) return
            app.refreshInfo()
            Bridge.call("player.state", {}, result => musicState.apply(result))
        }
        function onEvent(name, data) {
            switch (name) {
            case "player": musicState.apply(data); break
            case "playerTime":
                if (!seekGuard.running) { musicState.position = data.position; musicState.duration = data.duration }
                break
            case "trackFailed": app.toast(qsTr("Couldn't play “%1”. Skipping to the next track.").arg(data.title), "error"); break
            case "libraryChanged": app.refreshInfo(); app.libraryChanged(data.kind); break
            }
        }
    }

    // ---- navigation ------------------------------------------------------------------------------------
    property string section: "music"
    // Sections are created on first visit, so startup doesn't load video thumbnails or guides.
    property var visited: ({ music: true })
    function visit(name) {
        if (visited[name]) return
        const next = Object.assign({}, visited)
        next[name] = true
        visited = next
    }
    function navigate(name) {
        visit(name)
        section = name
        const stack = stackFor(name)
        if (stack && stack.depth > 1) stack.pop(null)
    }
    function stackFor(name) { return name === "music" ? musicHost.item : name === "videos" ? videoHost.item : null }
    function goBack() {
        if (playerLoader.active) return false
        const stack = stackFor(section)
        if (stack && stack.depth > 1) { stack.pop(); return true }
        return false
    }
    function openAlbum(id) {
        navigateKeepingStack("music")
        musicHost.item.push(albumPage, { albumId: id })
    }
    function openArtist(id) {
        navigateKeepingStack("music")
        musicHost.item.push(artistPage, { artistId: id })
    }
    function openShow(key) {
        navigateKeepingStack("videos")
        videoHost.item.push(showPage, { showKey: key })
    }
    function navigateKeepingStack(name) { visit(name); section = name }
    function playMusic(params) {
        if (musicState.engineError) { toast(musicState.engineError, "error"); return }
        Bridge.call("music.play", params, (result, error) => {
            if (error) toast(error, "error")
            else musicState.apply(result)
        })
    }
    function playVideo(item, resume) {
        if (musicState.playing) musicState.command("pause")
        playerLoader.active = true
        playerLoader.item.playVideo(item, resume)
    }
    function watchChannel(channelId) {
        if (musicState.playing) musicState.command("pause")
        playerLoader.active = true
        playerLoader.item.watchChannel(channelId)
    }
    function closePlayer() { playerLoader.active = false }
    function toggleFullScreen() {
        if (app.visibility === T.ApplicationWindow.FullScreen) app.showNormal()
        else app.showFullScreen()
    }
    function toast(message, kind) { toastItem.show(message, kind) }
    function editTrack(track) { tagSheet.openTrack(track) }
    function editAlbum(album) { tagSheet.openAlbum(album) }

    Component.onCompleted: {
        if (Platform.startFullScreen) showFullScreen()
        if (Platform.startPage) navigate(Platform.startPage)
    }

    // ---- layout ------------------------------------------------------------------------------------------
    component SectionHost: Loader {
        property string name
        anchors.fill: parent
        active: app.visited[name] === true
        visible: opacity > 0
        opacity: app.section === name ? 1 : 0
        enabled: app.section === name
        Behavior on opacity { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
    }

    Transition { id: pushIn; ParallelAnimation {
        NumberAnimation { property: "opacity"; from: 0; to: 1; duration: Theme.slow; easing.type: Easing.OutCubic }
        NumberAnimation { property: "x"; from: Theme.s(28); to: 0; duration: Theme.slow; easing.type: Easing.OutCubic } } }
    Transition { id: pushOut; NumberAnimation { property: "opacity"; from: 1; to: 0; duration: Theme.normal } }
    Transition { id: popIn; NumberAnimation { property: "opacity"; from: 0; to: 1; duration: Theme.slow; easing.type: Easing.OutCubic } }
    Transition { id: popOut; ParallelAnimation {
        NumberAnimation { property: "opacity"; from: 1; to: 0; duration: Theme.normal }
        NumberAnimation { property: "x"; from: 0; to: Theme.s(28); duration: Theme.normal; easing.type: Easing.OutCubic } } }

    Component { id: albumPage; AlbumPage {} }
    Component { id: artistPage; ArtistPage {} }
    Component { id: showPage; ShowPage {} }

    RowLayout {
        anchors.fill: parent
        spacing: 0

        NavRail {
            Layout.fillHeight: true
            compact: app.compactRail
            current: app.section
            musicActive: musicState.active
            musicPlaying: musicState.playing
            onNavigate: name => app.navigate(name)
        }

        ColumnLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            spacing: 0

            Item {
                Layout.fillWidth: true
                Layout.fillHeight: true
                clip: true

                SectionHost {
                    id: musicHost
                    name: "music"
                    sourceComponent: T.StackView {
                        initialItem: MusicPage {}
                        pushEnter: pushIn; pushExit: pushOut; popEnter: popIn; popExit: popOut
                    }
                }
                SectionHost {
                    id: videoHost
                    name: "videos"
                    sourceComponent: T.StackView {
                        initialItem: VideosPage {}
                        pushEnter: pushIn; pushExit: pushOut; popEnter: popIn; popExit: popOut
                    }
                }
                SectionHost { name: "tv"; sourceComponent: TvGuidePage {} }
                SectionHost { name: "nowplaying"; sourceComponent: NowPlayingPage {} }
                SectionHost { name: "settings"; sourceComponent: SettingsPage {} }
            }

            MiniPlayer {
                Layout.fillWidth: true
                Layout.preferredHeight: musicState.active && app.section !== "nowplaying" ? implicitHeight : 0
                visible: Layout.preferredHeight > 0
                clip: true
                music: musicState
                onOpenNowPlaying: app.navigate("nowplaying")
                onOpenAlbum: id => app.openAlbum(id)
                Behavior on Layout.preferredHeight { NumberAnimation { duration: Theme.normal; easing.type: Easing.OutCubic } }
            }
        }
    }

    // ---- overlays ---------------------------------------------------------------------------------------
    EditTagsSheet { id: tagSheet }

    Loader {
        id: playerLoader
        anchors.fill: parent
        active: false
        z: 50
        sourceComponent: VideoPlayer {
            onClosed: app.closePlayer()
        }
    }

    Toast {
        id: toastItem
        z: 90
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: (musicState.active && !playerLoader.active ? Theme.s(84) : 0) + Theme.s(20)
    }

    // The bridge owns the libraries; if it cannot run, explain why instead of showing empty screens.
    Rectangle {
        anchors.fill: parent
        z: 100
        visible: Bridge.fatalError.length > 0
        color: Theme.bg
        MouseArea { anchors.fill: parent; hoverEnabled: true }
        EmptyState {
            anchors.centerIn: parent
            icon: "warning"
            title: qsTr("Dustan can't reach its library service")
            message: Bridge.fatalError
            actionText: Bridge.fatalError.indexOf("already running") >= 0 ? "" : qsTr("Restart service")
            onAction: Bridge.restart()
        }
    }

    // ---- keyboard ---------------------------------------------------------------------------------------
    Shortcut { sequences: [StandardKey.Back, "Alt+Left", "Backspace"]; enabled: !playerLoader.active; onActivated: app.goBack() }
    Shortcut { sequence: "Escape"; enabled: !playerLoader.active; onActivated: if (!app.goBack() && app.visibility === T.ApplicationWindow.FullScreen) app.showNormal() }
    Shortcut { sequence: "Ctrl+1"; onActivated: app.navigate("music") }
    Shortcut { sequence: "Ctrl+2"; onActivated: app.navigate("videos") }
    Shortcut { sequence: "Ctrl+3"; onActivated: app.navigate("tv") }
    Shortcut { sequence: "Ctrl+4"; onActivated: app.navigate("nowplaying") }
    Shortcut { sequence: "Ctrl+,"; onActivated: app.navigate("settings") }
    Shortcut { sequence: "F11"; enabled: !playerLoader.active; onActivated: app.toggleFullScreen() }
    Shortcut { sequences: ["Media Play", "Media Pause", "Toggle Media Play/Pause"]; enabled: !playerLoader.active && musicState.active; onActivated: musicState.command("toggle") }
    Shortcut { sequence: "Media Next"; enabled: !playerLoader.active; onActivated: musicState.command("next") }
    Shortcut { sequence: "Media Previous"; enabled: !playerLoader.active; onActivated: musicState.command("previous") }
    Shortcut {
        sequence: "Space"
        enabled: !playerLoader.active && musicState.active && !(app.activeFocusItem instanceof TextInput)
        onActivated: musicState.command("toggle")
    }
    MouseArea {
        anchors.fill: parent
        acceptedButtons: Qt.BackButton
        z: -1
        onClicked: app.goBack()
    }

    Loader { active: Platform.tourDirectory.length > 0 && Platform.verifyFiles.length === 0; source: "Tour.qml" }
    Loader { active: Platform.verifyFiles.length > 0; source: "Verify.qml" }
}
