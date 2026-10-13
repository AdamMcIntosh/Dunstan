# Dustan

Cross-platform NAS music and video player with scheduled TV channels. Streams from the share; keeps a local index. Windows, macOS, Linux.

Audio files stay on the NAS. Dustan only stores a SQLite library index, tags, and small artwork thumbnails in local app data.

Videos also stay on the NAS. The separate video catalog stores playback progress and TV channel schedules locally.

## Install

Download the latest zip from [GitHub Releases](https://github.com/AdamMcIntosh/Dunstan/releases).

| Platform | Asset | Notes |
| --- | --- | --- |
| Windows x64 | `Dustan-<version>-win-x64.zip` | Extract and run `Dustan.exe`. LibVLC is included. |
| macOS Apple Silicon | `Dustan-<version>-osx-arm64.zip` | Extract and run `Dustan`. LibVLC is included. |
| macOS Intel | `Dustan-<version>-osx-x64.zip` | Extract and run `Dustan`. LibVLC is included. |
| Linux x64 | `Dustan-<version>-linux-x64.zip` | Install system LibVLC, then run `./Dustan`. |

On Debian/Ubuntu:

```bash
sudo apt install libvlc-dev vlc
chmod +x Dustan
./Dustan
```

On Omarchy / Arch, video playback also needs the decoder and subtitle plugins:

```bash
sudo pacman -S --needed ffmpeg vlc vlc-plugins-base vlc-plugin-ffmpeg vlc-plugin-freetype vlc-plugin-ass
```

The Avalonia desktop runs through X11/XWayland on Linux. Omarchy includes XWayland.

## First use

1. Open **Library**.
2. Choose the music folder, or paste a path:
   - Windows: `\\nas\share\music` (mapped drives are converted to UNC when possible)
   - macOS / Linux: a **mounted** folder such as `/Volumes/music` or `/mnt/music` (`smb://` URLs are not scanned)
3. Click **Save & Scan**.
4. Browse Albums / Artists / Tracks and play.

Use one canonical mount path per library. Scanning the same files from two different mount points creates duplicate index rows.

## Videos

Music remains available through Albums, Artists, Tracks and Library. **Videos** has its own folder and scan, independent of the music library. Mount the NAS video share on Linux, then choose it in Videos and select **Save & scan**. Scan cancellation or errors retain the previous catalog; unchanged files reuse cached durations.

Video scanning requires **FFmpeg's `ffprobe`** on PATH (or beside the Dustan executable). On Debian/Ubuntu install `ffmpeg`; on Windows/macOS install FFmpeg and make `ffprobe` available on PATH. Metadata is read in a separate process with a 20-second per-file timeout, so a native parser failure cannot crash Dustan. Failed or timed-out files remain browsable with an unknown duration and are excluded from TV scheduling until a successful rescan.

- Browse/search video cards and filter by Continue watching, Unwatched or Watched.
- Supported file extensions: MKV, MP4, M4V, AVI, MOV, WMV, WebM, TS, M2TS, MPG and MPEG. Actual codec support depends on your installed LibVLC plugins.
- Local posters are read from `video-name.jpg`, `video-name.png`, `poster.jpg` or `folder.jpg` beside the video. Titles come from filenames; online metadata/artwork lookup is not included.
- Play or resume in a dedicated video window. Music pauses when video opens; resume music through its existing transport after closing the video.
- Controls: play/pause, seek, volume, speed, audio-track selection, embedded subtitles and external SRT/ASS/SSA/VTT/SUB files.
- Keyboard: Space pauses, left/right seek −10/+30 seconds, F toggles fullscreen, Escape leaves fullscreen or closes the player.
- Progress saves every ten seconds and on close. Playback completion marks the video watched.

## TV channels and guide

1. Scan your Videos library so Dustan can read the actual runtimes.
2. Open **TV guide → Create channel**. Give it a name and select videos using Ctrl/Shift or Select all.
3. The selected videos play in the displayed path order, repeating continuously from the channel's creation time.
4. **Watch live** joins the program at its scheduled position. Playback switches to the next program at the guide boundary. **Go live** rejoins the current position after pausing.

The guide shows three hours at a time, with navigation up to a week ahead and times in your local timezone. Schedules persist across app restarts and continue logically while the app is closed; no background transcoding or network broadcast is needed. This is local scheduled playback, not an IPTV server.

Channel lineups snapshot their paths and durations so library rescans cannot unexpectedly shift the guide. To change a lineup, remove and recreate the channel. Files with unknown durations are excluded from channel creation. If a scheduled file becomes unavailable, the player reports it and moves on at the next scheduled boundary. Removing a channel does not delete videos.

## Data on disk

Settings, `library.db`, and artwork live in local app data:

- Windows: `%LOCALAPPDATA%\Dustan`
- Linux: `~/.local/share/Dustan`
- macOS: `~/Library/Application Support/Dustan`

`videos.json` in the same directory holds the separate video index, resume positions and channel schedules. Back it up to preserve your channels. Video scanning never writes to your NAS files.

If you previously ran the unpackaged WinUI build, sidecar files next to the old exe are copied here on first launch.

## Development

Requirements: .NET 8 SDK on Windows 10 19041+ / Windows 11, macOS, or Linux. Linux also needs LibVLC installed.

```powershell
dotnet build Dustan.Desktop/Dustan.Desktop.csproj -c Release
dotnet run --project Dustan.Desktop/Dustan.Desktop.csproj -c Release --no-build
```

Build the project directly with .NET 8; opening the `.slnx` solution requires a newer SDK supporting that format.

Run schedule, persistence, scan-safety and music regression checks:

```bash
dotnet run --project Dustan.Tests/Dustan.Tests.csproj -c Release
# Optional native codec smoke test (requires system LibVLC on Linux):
dotnet run --project Dustan.Tests/Dustan.Tests.csproj -c Release -- --media /path/to/sample.mp4
```

Publish a standalone Linux Release build:

```bash
dotnet publish Dustan.Desktop/Dustan.Desktop.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/Dustan
```

`Dustan.slnx` contains `Dustan.Core` (library, scanner, tags) and `Dustan.Desktop` (Avalonia UI + LibVLC).

### Publish a zip locally

```powershell
.\scripts\Publish-Dustan.ps1
.\scripts\Publish-Dustan.ps1 -Runtime osx-arm64
.\scripts\Publish-Dustan.ps1 -Runtime linux-x64
```

Output is `artifacts/Dustan-<version>-<runtime>.zip`. Pass `-Version 1.0.1` to override the `<Version>` property in `Dustan.Desktop/Dustan.Desktop.csproj`.

### GitHub Releases

CI builds Release on Windows, macOS, and Linux for every push and pull request.

Push a version tag to publish self-contained zips and create a GitHub Release:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

That produces `win-x64`, `osx-arm64`, `osx-x64`, and `linux-x64` assets. You can also run the **Release** workflow from the Actions tab.
