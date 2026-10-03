# Dustan

Cross-platform music player for a local NAS library (FLAC/MP3). Streams audio from the share; keeps only a SQLite index, tags, and small artwork thumbnails in local app data.

Runs on Windows, macOS, and Linux (Avalonia + LibVLC).

## Install

### Windows

1. Download `Dustan-<version>-win-x64.zip` from GitHub Releases.
2. Extract the zip and run `Dustan.exe`.

LibVLC is bundled in the Windows zip.

### macOS

1. Download `Dustan-<version>-osx-arm64.zip` or `osx-x64`.
2. Extract and run `Dustan`.

LibVLC is bundled for macOS builds produced on a Mac.

### Linux

1. Download `Dustan-<version>-linux-x64.zip`.
2. Install system LibVLC (for example `sudo apt install libvlc-dev vlc` on Debian/Ubuntu).
3. Run `./Dustan`.

## First use

1. Open **Library**.
2. Browse to your music folder, or paste a path:
   - Windows: `\\nas\share\music` (mapped drives are converted to UNC when possible)
   - macOS / Linux: a **mounted** folder such as `/Volumes/music` or `/mnt/music` (`smb://` URLs are not scanned directly)
3. Click **Save & Scan**.
4. Browse Albums / Artists / Tracks and play. Audio files are never copied off the NAS.

Index, settings, and thumbnails live in local app data (`%LOCALAPPDATA%\Dustan` on Windows, `~/.local/share/Dustan` on Linux, `~/Library/Application Support/Dustan` on macOS). If you previously ran the unpackaged WinUI build, those sidecar files next to the old exe are copied here on first launch.

Use one canonical mount path per library. Scanning the same files from two different mount points will create duplicate index rows.

## Development

Requirements: .NET 8 SDK. Windows 10 19041+ / Windows 11, macOS, or Linux.

```powershell
dotnet build Dustan.Desktop/Dustan.Desktop.csproj -c Debug
dotnet run --project Dustan.Desktop/Dustan.Desktop.csproj -c Debug --no-build
```

Linux also needs LibVLC installed on the machine.

Solution file: `Dustan.slnx` (`Dustan.Core` + `Dustan.Desktop`).

### Publish a Release zip

```powershell
.\scripts\Publish-Dustan.ps1
.\scripts\Publish-Dustan.ps1 -Runtime osx-arm64
.\scripts\Publish-Dustan.ps1 -Runtime linux-x64
```

Produces `artifacts/Dustan-<version>-<runtime>.zip`. Pass `-Version 1.0.1` to override the `<Version>` property in `Dustan.Desktop/Dustan.Desktop.csproj`.

### GitHub Releases

CI builds Release on Windows, macOS, and Linux for every push/PR.

Pushing a version tag publishes self-contained zips and creates a GitHub Release:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

That produces `Dustan-1.0.1-win-x64.zip`, `osx-arm64`, `osx-x64`, and `linux-x64`. You can also run the **Release** workflow from the Actions tab (`workflow_dispatch`) to build a draft or a tagged set of assets without pushing locally.
