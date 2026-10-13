# Dustan desktop app (Qt Quick)

`Dustan.Qt` is Dustan's desktop interface: a Qt Quick/QML application with Music, Videos, Now Playing, TV Guide and Settings screens, a full-window video player, and sizing that scales from a laptop to a living-room TV.

## How it fits together

```
┌──────────────── Dustan (Qt Quick, C++/QML) ────────────────┐      ┌────────── Dustan.Bridge (.NET 8) ──────────┐
│ Music · Videos · Now Playing · TV Guide · Settings         │ JSON │ Dustan.Core, unchanged in behaviour:       │
│ Video player: libmpv rendered in the scene graph (OpenGL)  │ lines│  LibraryDb (SQLite music index)            │
│ Thumbnails: image://videothumb → ffmpeg child process      │◄────►│  LibraryScanner · TagEditor                │
│                                                            │stdio │  PlaybackService + LibVLC (music audio)    │
└────────────────────────────────────────────────────────────┘      │  VideoLibrary (videos.json) · TvSchedule   │
                                                                    │  VideoProbe → ffprobe child process        │
                                                                    └────────────────────────────────────────────┘
```

- **The bridge owns the data.** It uses the Core services, so the music index, video catalog, resume points and channel schedules stay in the same files as earlier releases. A single-instance lock stops two copies of Dustan from running against one library.
- **One request, one line.** The UI sends `{"id":1,"method":"music.albums","params":{…}}` and receives `{"id":1,"result":…}`, plus pushed events such as `player` and `videoScan`. The protocol is in `Dustan.Bridge/BridgeHost.cs`, and the method list in `Dustan.Bridge/DustanApi.cs`. If the UI exits or crashes, the bridge sees its input close and shuts down cleanly.
- **Native parsers stay out of process.** Video runtimes still come from `ffprobe` in a separate process with a 20-second limit. Preview frames for the Videos grid come from `ffmpeg`, also in a separate process (25-second limit, killed if the card scrolls away). A damaged file can't take the app down.
- **Libraries stay separate.** Music and videos have their own folders, scans and screens. The Videos screen groups the flat catalog into series, seasons and films at display time, and nothing about that grouping is stored.

### Data on disk

The libraries are in the same places as before (`~/.local/share/Dustan`, `%LOCALAPPDATA%\Dustan`, `~/Library/Application Support/Dustan`):

| File | Change |
| --- | --- |
| `settings.json`, `library.db`, `artwork/` | Unchanged. |
| `videos.json` | Videos gain an optional `LastPlayedUtc` (drives *Continue watching*). Files from earlier releases load as-is. |
| `video-thumbnails/`, `artwork-large/` | New caches for preview frames and 1200 px album covers. Safe to delete. |

Interface preferences (interface size, full-screen video, languages) are stored by Qt separately: `~/.config/Dustan/Dustan.conf` on Linux, the registry on Windows, and a plist on macOS. Set `DUSTAN_DATA_DIR` to run against another data folder, for example a portable install or a test copy.

## Linux (Release)

Packages on Arch / Omarchy (this is the configuration that was built and tested):

```bash
sudo pacman -S --needed cmake ninja gcc qt6-base qt6-declarative qt6-svg mpv ffmpeg dotnet-sdk-8.0 \
    vlc vlc-plugins-base vlc-plugin-ffmpeg intel-media-driver   # intel-media-driver: VA-API on Intel GPUs
```

Debian/Ubuntu need **Qt 6.5 or newer** (Ubuntu 24.04 ships 6.4; use 25.04+, Debian 13, or the Qt online installer). The equivalents are `cmake ninja-build g++ qt6-base-dev qt6-declarative-dev qt6-svg-dev libmpv-dev ffmpeg vlc dotnet-sdk-8.0`, plus the QML runtime modules (`qml6-module-qtquick-*`, `qml6-module-qtcore`). These Debian package names have not been tested.

Build and launch:

```bash
scripts/build-qt.sh                      # Release build → Dustan.Qt/build/release
Dustan.Qt/build/release/Dustan           # launch from the build tree

scripts/build-qt.sh --install ~/.local   # optional: ~/.local/bin/dustan + desktop entry
dustan
```

`build-qt.sh` is a thin wrapper around these commands:

```bash
cmake -S Dustan.Qt -B Dustan.Qt/build/release -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build Dustan.Qt/build/release        # also publishes Dustan.Bridge (self-contained) into build/release/bridge
```

Useful options: `--page videos|tv|nowplaying|settings`, `--fullscreen`, `--size compact|standard|large|tv`. For a TV box, use `dustan --fullscreen --size tv`.

Local builds use the system Qt. Release archives bundle Qt instead, via `scripts/package-qt.sh <version>` (`-DDUSTAN_DEPLOY_QT=ON`). Build those with the Qt online installer's Qt, as CI does: with a distribution's Qt in `/usr/lib`, Qt's deployment can't tell Qt apart from system libraries.

The app runs natively on Wayland and on X11. Hardware video decoding uses VA-API. On Wayland it is zero-copy (`vaapi`); on X11 it copies frames back (`vaapi-copy`). The player shows which one is active in its top-right corner.

## Windows build path

CI builds this on every push, but it hasn't been run interactively yet.

1. Install **Visual Studio 2022** (Desktop development with C++), the **.NET 8 SDK**, and **Qt 6.8 LTS → MSVC 2022 64-bit** with the Qt Online Installer. Qt Quick Controls, Effects, Shapes and Dialogs are part of the default Qt Declarative install.
2. Get **libmpv**. From an *x64 Native Tools* PowerShell, run `./scripts/fetch-libmpv-windows.ps1 -Destination C:\deps\mpv`. It downloads the latest shinchiro `mpv-dev-x86_64` build and creates the MSVC import library `mpv.lib`. It needs the GitHub CLI, signed in.
3. Get **FFmpeg** (`ffmpeg.exe` and `ffprobe.exe`, for example the gyan.dev essentials build).
4. Build, deploy and run from the *x64 Native Tools* prompt:

```bat
cmake -S Dustan.Qt -B build\win -G Ninja -DCMAKE_BUILD_TYPE=Release ^
      -DCMAKE_PREFIX_PATH=C:\Qt\6.8.3\msvc2022_64 ^
      -DMPV_DIR=C:\deps\mpv -DMPV_RUNTIME=C:\deps\mpv\libmpv-2.dll
cmake --build build\win
cmake --install build\win --prefix dist\Dustan      :: runs windeployqt, copies libmpv-2.dll and the bridge into bin\
copy C:\deps\ffmpeg\bin\ffmpeg.exe dist\Dustan\bin\
copy C:\deps\ffmpeg\bin\ffprobe.exe dist\Dustan\bin\
dist\Dustan\bin\Dustan.exe
```

LibVLC for music playback comes with the bridge (the `VideoLAN.LibVLC.Windows` NuGet package). Dustan runs Qt Quick on OpenGL because libmpv renders through it, so it needs a GPU driver with desktop OpenGL 3.3 or later. On Windows, `auto-safe` hardware decoding is expected to choose D3D11VA (copy-back).

## macOS build path

CI builds this on every push, but it hasn't been run interactively yet.

```bash
xcode-select --install
brew install cmake ninja qt mpv ffmpeg dotnet@8
cmake -S Dustan.Qt -B build/mac -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_PREFIX_PATH="$(brew --prefix qt)"
cmake --build build/mac
cmake --install build/mac --prefix dist          # dist/Dustan.app; macdeployqt copies Qt, libmpv and its dylibs
cp "$(brew --prefix ffmpeg)"/bin/{ffmpeg,ffprobe} dist/Dustan.app/Contents/MacOS/
open dist/Dustan.app
```

- Apps opened from Finder don't inherit your shell `PATH`, so bundle `ffmpeg` and `ffprobe` as shown above.
- The bridge is published for the build machine's architecture (`osx-arm64` on Apple Silicon). Pass `-DDUSTAN_RID=osx-x64` to cross-target Intel.
- LibVLC for music comes from the `VideoLAN.LibVLC.Mac` NuGet package.
- Apple deprecates OpenGL but Qt still supports it.
- To distribute, sign with the hardened runtime. The self-contained .NET bridge needs the `com.apple.security.cs.allow-jit` entitlement.

## Tests and verification tools

```bash
dotnet run --project Dustan.Tests/Dustan.Tests.csproj -c Release            # 78 checks: schedules, scans, catalog grouping, bridge protocol and process
dotnet run --project Dustan.Tests/Dustan.Tests.csproj -c Release -- --database-copy /path/to/copy/library.db
dotnet run --project Dustan.Tests/Dustan.Tests.csproj -c Release -- --media /path/to/sample.mkv

# Development aids in the app itself (use a copy of the data: DUSTAN_DATA_DIR=/tmp/dustan-copy)
Dustan --tour /tmp/shots                                    # visits every screen with your libraries, saves screenshots, quits
Dustan --tour /tmp/shots --verify-playback files.txt        # plays each listed file: start, seek, audio switch, subtitles; logs JSON
```

## Playback notes and limitations

- **Codecs depend on the FFmpeg inside libmpv.** On the test NAS, these all played through the app, including seeking, audio-track switching and subtitles:
  - Video: H.264, HEVC, VC-1, MPEG-2 and VP9.
  - Audio: AC3, AAC, DTS 5.1/6.1, TrueHD 7.1, PCM 5.1, MP3 and Opus.
  - Subtitles: PGS, DVD and SRT.
  - A file with a broken MP4 header (`moov atom not found`) shows a clear error rather than hanging.
- **No audio passthrough.** Surround formats are decoded and mixed for the system output; bitstreaming to an AV receiver isn't enabled. No HDR content was available to test. mpv tone-maps HDR to SDR, but there is no HDR output.
- **Two playback engines.** Music plays through LibVLC in the bridge, and video through mpv in the UI. Starting a video pauses music; music doesn't resume automatically.
- **Live TV can't be seeked.** Channels join each programme at its scheduled position. *Go live* re-syncs after a pause. If a file ends early, the channel waits for the next scheduled boundary. Missing files show a message, and the channel moves on at the boundary.
- **Preview frames are generated on first view**, two at a time. Over SMB the first visit to Videos takes about a minute to fill in every series and film; after that they come from the cache.
- **Videos take 2–6 seconds to start** over SMB, depending on the container. A spinner appears after half a second.
- **Hardware decoding was verified with VA-API on Linux only.** MPEG-2 (the DVD rips) decodes in software because mpv's default hardware-decoding list excludes it; at 480p that costs little.
- **Remote controls:** keyboard navigation works throughout (arrows, Enter, Escape/Backspace, Page Up/Down for channels). There is no HDMI-CEC or gamepad mapping.
