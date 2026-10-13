# Dustan

Cross-platform NAS music and video player with scheduled TV channels. Streams from the share; keeps a local index. Windows, macOS, Linux.

Audio files stay on the NAS. Dustan only stores a SQLite library index, tags, and small artwork thumbnails in local app data.

Videos also stay on the NAS. The separate video catalog stores playback progress and TV channel schedules locally.

The desktop app is built with Qt Quick. It has screens for Music, Videos, Now Playing, the TV Guide and Settings, and a full-screen player. Its sizing scales from a laptop to a living-room TV (`--size tv`).

## Install

Download the latest archive from [GitHub Releases](https://github.com/AdamMcIntosh/Dunstan/releases). Each archive bundles Qt and Dustan's library service.

| Platform | Asset | Notes |
| --- | --- | --- |
| Windows x64 | `Dustan-<version>-win-x64.zip` | Extract and run `Dustan.exe`. LibVLC is included. |
| Linux x64 | `Dustan-<version>-linux-x64.zip` | Install system LibVLC, then run `./Dustan`. |

On Debian/Ubuntu:

```bash
sudo apt install libmpv2 vlc ffmpeg
```

On Omarchy / Arch, building from source is the most direct route:

```bash
sudo pacman -S --needed cmake ninja qt6-base qt6-declarative qt6-svg mpv ffmpeg dotnet-sdk-8.0 \
    vlc vlc-plugins-base vlc-plugin-ffmpeg
scripts/build-qt.sh --install ~/.local     # then run: dustan
```

## First use

1. Open **Settings**.
2. Under **Music library**, choose the music folder or paste a path:
   - Windows: `\\nas\share\music` (mapped drives are converted to UNC when possible)
   - macOS / Linux: a **mounted** folder such as `/Volumes/music` or `/mnt/music` (`smb://` URLs are not scanned)
3. Click **Save & scan**.
4. Browse albums, artists and songs in **Music** and play.

Use one canonical mount path per library. Scanning the same files from two different mount points creates duplicate index rows.

Only one Dustan can run against a library at a time.

## Videos

**Videos** has its own folder and scan, independent of the music library. Mount the NAS video share, then choose it under **Settings → Video library** and select **Save & scan**. If a scan is cancelled or fails, the previous catalog is kept, and unchanged files reuse their cached durations.

Video scanning uses **FFmpeg's `ffprobe`** in a separate process with a 20-second limit per file, so a native parser failure can't crash Dustan. Files that fail or time out stay browsable with an unknown duration. They are left out of TV scheduling until a later scan reads them.

- **Browsing:** shows are grouped into series and seasons from their folders and `S01E02`-style names; films are listed separately. *Continue watching* lists what you're in the middle of. You can filter to In progress or Unwatched, and search titles and episodes.
- **File types:** MKV, MP4, M4V, AVI, MOV, WMV, WebM, TS, M2TS, MPG and MPEG. Playback uses libmpv, which handles H.264, HEVC, VC-1, MPEG-2, VP9 and AV1 video, and DTS, TrueHD, AC3, AAC and Opus audio.
- **Artwork:** local posters come from `video-name.jpg`, `video-name.png`, `poster.jpg` or `folder.jpg` beside the video. Otherwise Dustan shows a frame from the video, grabbed by `ffmpeg` in a separate process and cached. Titles come from filenames; there is no online metadata lookup.
- **Player:** resume or start over, seek, ±10/30 s skips, volume, speed, audio-track selection, embedded subtitles, external SRT/ASS/SSA/VTT/SUB files, and automatic subtitle files with matching names. It can play the next episode automatically.
- **Keyboard:**
  - Space pauses.
  - ←/→ seek −10/+30 seconds.
  - ↑/↓ change the volume.
  - A cycles audio tracks; S cycles subtitles.
  - F toggles full screen.
  - Escape closes the player.
- **Progress:** saved every ten seconds and on close. Reaching the end marks the video watched. Music pauses when a video starts.

## TV channels and guide

1. Scan your video library so Dustan can read the actual runtimes.
2. Open **TV Guide → New channel**. Give it a name and pick whole shows, individual seasons or films. Choose **In order** or **Shuffled**.
3. The lineup plays back to back, repeating continuously from the moment the channel is created.
4. **Watch live** joins the programme at its scheduled position. The player switches to the next programme at the guide boundary. **Go live** rejoins the current position after a pause, and Page Up/Down changes channel.

The guide shows what's on now on every channel, then a timeline you can scroll up to a week ahead, in your local timezone. Schedules persist across restarts and keep running logically while the app is closed. No background transcoding or network broadcast is needed: this is local scheduled playback, not an IPTV server.

Channel lineups store a snapshot of their paths and durations, so a library rescan can't unexpectedly shift the guide. To change a lineup, remove the channel and create it again. Files with unknown durations are left out. If a scheduled file becomes unavailable, the player reports it and moves on at the next scheduled boundary. Removing a channel doesn't delete any videos.

## Data on disk

Settings, `library.db`, and artwork live in local app data:

- Windows: `%LOCALAPPDATA%\Dustan`
- Linux: `~/.local/share/Dustan`
- macOS: `~/Library/Application Support/Dustan`

`videos.json` in the same directory holds the separate video index, resume positions and channel schedules. Back it up to preserve your channels. The `video-thumbnails` and `artwork-large` folders are caches and are safe to delete. Video scanning never writes to your NAS files. Set `DUSTAN_DATA_DIR` to keep all of this somewhere else.

If you previously ran the unpackaged WinUI build, sidecar files next to the old exe are copied here on first launch.

## Development

The repository has four projects:

- `Dustan.Qt`: the Qt Quick app (C++/QML, CMake). Video plays through libmpv.
- `Dustan.Bridge`: the .NET 8 service process the app starts. It owns the libraries and plays music through LibVLC.
- `Dustan.Core`: the library, scanner, tags, video catalog, TV schedules and playback services.
- `Dustan.Tests`: the checks below.

[docs/QT-APP.md](docs/QT-APP.md) covers the architecture and the per-platform build steps. In short, you need CMake 3.21+, Ninja, a C++20 compiler, Qt 6.5+, libmpv and the .NET 8 SDK.

```bash
scripts/build-qt.sh                       # Release build → Dustan.Qt/build/release
Dustan.Qt/build/release/Dustan
```

Run the schedule, persistence, scan-safety, music, catalog-grouping and bridge checks:

```bash
dotnet run --project Dustan.Tests/Dustan.Tests.csproj -c Release
# Optional native codec smoke test (requires system LibVLC on Linux):
dotnet run --project Dustan.Tests/Dustan.Tests.csproj -c Release -- --media /path/to/sample.mp4
```

### Package an archive locally

```bash
dotnet publish Dustan.Desktop/Dustan.Desktop.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/Dustan
```

`Dustan.slnx` contains `Dustan.Core` (library, scanner, tags) and `Dustan.Desktop` (Avalonia UI + LibVLC).

### Publish a zip locally

```powershell
.\scripts\Publish-Dustan.ps1
.\scripts\Publish-Dustan.ps1 -Runtime linux-x64
```

Output is `artifacts/Dustan-<version>-<runtime>.zip`. Pass `-Version 1.0.1` to override the `<Version>` property in `Dustan.Desktop/Dustan.Desktop.csproj`.

### GitHub Releases

On every push and pull request, CI runs the checks on Windows, macOS and Linux, and builds the Qt app on all three.

Push a version tag to build the archives and create a GitHub Release:

```bash
git tag v2.0.1
git push origin v2.0.1
```

That produces `win-x64` and `linux-x64` assets. You can also run the **Release** workflow from the Actions tab.
