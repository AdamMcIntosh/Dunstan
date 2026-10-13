pragma Singleton
import QtQuick

// Design tokens for Dustan. Every size goes through s() so the whole interface scales
// for TV viewing distances without separate layouts.
QtObject {
    id: theme

    // ---- scale -------------------------------------------------------------
    property real scale: 1.0
    function s(value) { return Math.round(value * scale) }

    // ---- colour ------------------------------------------------------------
    readonly property color bg: "#0B0C0F"
    readonly property color rail: "#0F1014"
    readonly property color surface: "#14161B"
    readonly property color surface2: "#1A1C22"
    readonly property color surface3: "#22252D"
    readonly property color hover: Qt.rgba(1, 1, 1, 0.045)
    readonly property color pressed: Qt.rgba(1, 1, 1, 0.08)
    readonly property color line: Qt.rgba(1, 1, 1, 0.07)
    readonly property color lineStrong: Qt.rgba(1, 1, 1, 0.13)
    readonly property color text: "#EDEEF0"
    readonly property color text2: "#A3A7B0"
    readonly property color text3: "#6B707A"
    readonly property color accent: "#E3B96B"
    readonly property color accentHover: "#EDC983"
    readonly property color accentText: "#17130A"
    readonly property color accentSoft: Qt.rgba(0.89, 0.725, 0.42, 0.14)
    readonly property color live: "#EF5B4F"
    readonly property color positive: "#79C99A"
    readonly property color focus: "#F2D7A0"
    readonly property color scrim: Qt.rgba(0.02, 0.02, 0.03, 0.72)

    // ---- shape & motion ----------------------------------------------------
    readonly property int radiusS: s(6)
    readonly property int radiusM: s(10)
    readonly property int radiusL: s(16)
    readonly property int fast: 120
    readonly property int normal: 200
    readonly property int slow: 340

    // ---- type --------------------------------------------------------------
    readonly property string family: "Inter"
    readonly property int display: s(40)
    readonly property int title: s(28)
    readonly property int heading: s(20)
    readonly property int subheading: s(16)
    readonly property int body: s(14)
    readonly property int label: s(13)
    readonly property int caption: s(12)
    readonly property int overline: s(11)

    // ---- formatting --------------------------------------------------------
    function clock(seconds) {
        if (!isFinite(seconds) || seconds < 0) seconds = 0
        seconds = Math.floor(seconds)
        const h = Math.floor(seconds / 3600), m = Math.floor(seconds % 3600 / 60), sec = seconds % 60
        const pad = n => (n < 10 ? "0" : "") + n
        return h > 0 ? h + ":" + pad(m) + ":" + pad(sec) : m + ":" + pad(sec)
    }
    function duration(seconds) {
        if (!isFinite(seconds) || seconds <= 0) return ""
        const minutes = Math.round(seconds / 60)
        if (minutes < 60) return minutes + " min"
        const h = Math.floor(minutes / 60), m = minutes % 60
        if (h >= 48) return Math.floor(h / 24) + " days " + (h % 24) + " hr"
        return m > 0 ? h + " hr " + m + " min" : h + " hr"
    }
    function plural(n, one, many) { return n + " " + (n === 1 ? one : (many || one + "s")) }
    function timeOfDay(ms) { return Qt.formatTime(new Date(ms), Qt.locale().timeFormat(Locale.ShortFormat)) }
    function initials(name) {
        const parts = (name || "").split(/[\s\-_\/&,.:;()\[\]'"]+/).filter(w => w.length)
        const letters = parts.filter(w => /[A-Za-z\u00C0-\uFFFF]/.test(w[0]))
        const words = letters.length ? letters : parts
        if (words.length === 0) return "♪"
        return (words[0][0] + (words.length > 1 ? words[words.length - 1][0] : "")).toUpperCase()
    }
    // A stable, muted hue per name for generated artwork.
    function tint(name, lightness) {
        let h = 0
        const str = name || ""
        for (let i = 0; i < str.length; ++i) h = (h * 31 + str.charCodeAt(i)) % 360
        return Qt.hsla(h / 360, 0.28, lightness === undefined ? 0.26 : lightness, 1)
    }

    // ---- icons -------------------------------------------------------------
    // Original 24×24 line icons: s = stroked path, f = filled path.
    readonly property var icons: ({
        play: { f: "M8 5.6v12.8a1 1 0 0 0 1.53.85l10.1-6.4a1 1 0 0 0 0-1.7L9.53 4.75A1 1 0 0 0 8 5.6z" },
        pause: { f: "M7.5 5h2.2a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H7.5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1zM14.3 5h2.2a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1h-2.2a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z" },
        next: { f: "M5.5 6.3v11.4a.8.8 0 0 0 1.25.66l8.5-5.7a.8.8 0 0 0 0-1.32l-8.5-5.7a.8.8 0 0 0-1.25.66zM17.2 5.5h1.6a.7.7 0 0 1 .7.7v11.6a.7.7 0 0 1-.7.7h-1.6a.7.7 0 0 1-.7-.7V6.2a.7.7 0 0 1 .7-.7z" },
        previous: { f: "M18.5 6.3v11.4a.8.8 0 0 1-1.25.66l-8.5-5.7a.8.8 0 0 1 0-1.32l8.5-5.7a.8.8 0 0 1 1.25.66zM6.8 5.5H5.2a.7.7 0 0 0-.7.7v11.6a.7.7 0 0 0 .7.7h1.6a.7.7 0 0 0 .7-.7V6.2a.7.7 0 0 0-.7-.7z" },
        shuffle: { s: "M3 7h3.2c1.9 0 3.1.9 4 2.4l3.6 6.2c.9 1.5 2.1 2.4 4 2.4H21M18 15l3 3-3 3M3 17h3.2c1.2 0 2.1-.4 2.8-1.1M14.9 8.1c.7-.7 1.6-1.1 2.9-1.1H21M18 4l3 3-3 3" },
        repeat: { s: "M4 11V9.5A3.5 3.5 0 0 1 7.5 6H19M16 3l3 3-3 3M20 13v1.5a3.5 3.5 0 0 1-3.5 3.5H5M8 21l-3-3 3-3" },
        repeatOne: { s: "M4 11V9.5A3.5 3.5 0 0 1 7.5 6H19M16 3l3 3-3 3M20 13v1.5a3.5 3.5 0 0 1-3.5 3.5H5M8 21l-3-3 3-3M11 10.6l1.4-1v5" },
        volume: { f: "M4 9.4h2.9l4.2-3.5a.6.6 0 0 1 1 .46v11.28a.6.6 0 0 1-1 .46L6.9 14.6H4a1 1 0 0 1-1-1V10.4a1 1 0 0 1 1-1z", s: "M15.5 9.2a4 4 0 0 1 0 5.6M18.2 6.6a7.6 7.6 0 0 1 0 10.8" },
        volumeLow: { f: "M4 9.4h2.9l4.2-3.5a.6.6 0 0 1 1 .46v11.28a.6.6 0 0 1-1 .46L6.9 14.6H4a1 1 0 0 1-1-1V10.4a1 1 0 0 1 1-1z", s: "M15.5 9.2a4 4 0 0 1 0 5.6" },
        mute: { f: "M4 9.4h2.9l4.2-3.5a.6.6 0 0 1 1 .46v11.28a.6.6 0 0 1-1 .46L6.9 14.6H4a1 1 0 0 1-1-1V10.4a1 1 0 0 1 1-1z", s: "M16 10l4 4M20 10l-4 4" },
        search: { s: "M10.5 4a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13zM15.3 15.3L20 20" },
        music: { s: "M9 17.5V6.6l10-2.4v10.9M9 17.5a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0zM19 15.1a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0z" },
        videos: { s: "M4.5 5.5h15A1.5 1.5 0 0 1 21 7v10a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17V7a1.5 1.5 0 0 1 1.5-1.5z", f: "M10 9.2v5.6a.4.4 0 0 0 .6.34l4.6-2.8a.4.4 0 0 0 0-.68l-4.6-2.8a.4.4 0 0 0-.6.34z" },
        tv: { s: "M3.5 7.5h17a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1h-17a1 1 0 0 1-1-1v-10a1 1 0 0 1 1-1zM8.5 3.5L12 7l3.5-3.5" },
        settings: { s: "M4 6h9M17 6h3M4 12h3M11 12h9M4 18h11M19 18h1M15 4v4M9 10v4M17 16v4" },
        nowPlaying: { s: "M4 10v4M8 7v10M12 4v16M16 8v8M20 10v4" },
        back: { s: "M15 5l-7 7 7 7" },
        chevronRight: { s: "M9 5l7 7-7 7" },
        chevronLeft: { s: "M15 5l-7 7 7 7" },
        chevronDown: { s: "M5 9l7 7 7-7" },
        chevronUp: { s: "M5 15l7-7 7 7" },
        plus: { s: "M12 5v14M5 12h14" },
        close: { s: "M6 6l12 12M18 6L6 18" },
        check: { s: "M5 12.5l4.5 4.5L19 7.5" },
        edit: { s: "M15.2 4.8l4 4L9 19H5v-4L15.2 4.8zM13 7l4 4" },
        more: { f: "M6 10.4a1.6 1.6 0 1 1 0 3.2 1.6 1.6 0 0 1 0-3.2zM12 10.4a1.6 1.6 0 1 1 0 3.2 1.6 1.6 0 0 1 0-3.2zM18 10.4a1.6 1.6 0 1 1 0 3.2 1.6 1.6 0 0 1 0-3.2z" },
        subtitles: { s: "M4 5.5h16a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-11a1 1 0 0 1 1-1zM6.5 14h4.5M13.5 14h4M6.5 10.5h2M11 10.5h6.5" },
        audio: { s: "M4 15.5v-3.5a8 8 0 0 1 16 0v3.5M4 15.5a2 2 0 0 1 2-2h1.5v6H6a2 2 0 0 1-2-2v-2zM20 15.5a2 2 0 0 0-2-2h-1.5v6H18a2 2 0 0 0 2-2v-2z" },
        fullscreen: { s: "M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" },
        exitFullscreen: { s: "M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5" },
        rewind: { s: "M4.5 12a7.5 7.5 0 1 0 2.2-5.3M4.5 3.5v3.7h3.7" },
        forward: { s: "M19.5 12a7.5 7.5 0 1 1-2.2-5.3M19.5 3.5v3.7h-3.7" },
        folder: { s: "M3.5 7A1.5 1.5 0 0 1 5 5.5h4l2 2.5h8A1.5 1.5 0 0 1 20.5 9.5v8A1.5 1.5 0 0 1 19 19H5a1.5 1.5 0 0 1-1.5-1.5z" },
        trash: { s: "M5 7h14M10 7V5h4v2M7 7l1 12h8l1-12M10.5 10.5V16M13.5 10.5V16" },
        queue: { s: "M4 6h16M4 11h16M4 16h9", f: "M16 13.6v5.8a.4.4 0 0 0 .6.34l4.4-2.9a.4.4 0 0 0 0-.68l-4.4-2.9a.4.4 0 0 0-.6.34z" },
        album: { s: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z" },
        artist: { s: "M12 4a3.6 3.6 0 1 0 0 7.2A3.6 3.6 0 0 0 12 4zM5 20a7 7 0 0 1 14 0" },
        clock: { s: "M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17zM12 7.5V12l3 2" },
        refresh: { s: "M19.5 12A7.5 7.5 0 1 1 17 6.4M19.5 3.6v4h-4" },
        warning: { s: "M12 4.5l8.5 15h-17L12 4.5zM12 10v4M12 16.8v.01" },
        info: { s: "M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17zM12 11v5M12 7.9v.01" },
        dot: { f: "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z" },
        eye: { s: "M2.5 12s3.5-6.5 9.5-6.5 9.5 6.5 9.5 6.5-3.5 6.5-9.5 6.5S2.5 12 2.5 12zM12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z" },
        speed: { s: "M4.5 16.5a7.5 7.5 0 1 1 15 0M12 16.5l4-5" },
        list: { s: "M8 6h12M8 12h12M8 18h12M4 6v.01M4 12v.01M4 18v.01" },
        grid: { s: "M4.5 4.5h6v6h-6zM13.5 4.5h6v6h-6zM4.5 13.5h6v6h-6zM13.5 13.5h6v6h-6z" },
        live: { s: "M7.8 7.8a6 6 0 0 0 0 8.4M16.2 7.8a6 6 0 0 1 0 8.4M5 5a10 10 0 0 0 0 14M19 5a10 10 0 0 1 0 14", f: "M12 10.2a1.8 1.8 0 1 0 0 3.6 1.8 1.8 0 0 0 0-3.6z" },
        image: { s: "M5 4.5h14A1.5 1.5 0 0 1 20.5 6v12a1.5 1.5 0 0 1-1.5 1.5H5A1.5 1.5 0 0 1 3.5 18V6A1.5 1.5 0 0 1 5 4.5zM3.5 16l5-5 4 4 2.5-2.5 5 5M15.5 8.5v.01" },
    })
}
