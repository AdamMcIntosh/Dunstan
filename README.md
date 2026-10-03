# Dustan

Cross-platform NAS music player for FLAC/MP3. Streams from the share; keeps a local index. Windows, macOS, Linux.

Audio files stay on the NAS. Dustan only stores a SQLite library index, tags, and small artwork thumbnails in local app data.

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

## First use

1. Open **Library**.
2. Choose the music folder, or paste a path:
   - Windows: `\\nas\share\music` (mapped drives are converted to UNC when possible)
   - macOS / Linux: a **mounted** folder such as `/Volumes/music` or `/mnt/music` (`smb://` URLs are not scanned)
3. Click **Save & Scan**.
4. Browse Albums / Artists / Tracks and play.

Use one canonical mount path per library. Scanning the same files from two different mount points creates duplicate index rows.

## Data on disk

Settings, `library.db`, and artwork live in local app data:

- Windows: `%LOCALAPPDATA%\Dustan`
- Linux: `~/.local/share/Dustan`
- macOS: `~/Library/Application Support/Dustan`

If you previously ran the unpackaged WinUI build, sidecar files next to the old exe are copied here on first launch.

## Development

Requirements: .NET 8 SDK on Windows 10 19041+ / Windows 11, macOS, or Linux. Linux also needs LibVLC installed.

```powershell
dotnet build Dustan.slnx -c Debug
dotnet run --project Dustan.Desktop/Dustan.Desktop.csproj -c Debug --no-build
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
