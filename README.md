# Dustan

Unpackaged WinUI 3 music player for a local NAS library (FLAC/MP3). Streams audio from the share; keeps only a SQLite index, tags, and small artwork thumbnails beside the exe.

## Install

1. Download `Dustan-<version>-win-x64.zip` from GitHub Releases.
2. Extract the zip to a folder of your choice.
3. Run `Dustan.exe`.

No installer and no separate .NET or Windows App SDK runtime install — the Release build is self-contained.

## First use

1. Enter a UNC path such as `\\nas\share\music` (mapped drives are accepted and converted to UNC when possible).
2. Click **Save & Scan**.
3. Browse Albums / Artists / Tracks and play. Audio files are never copied off the NAS.

## Development

Requirements: Windows 10 19041+ / Windows 11, .NET 8 SDK.

```powershell
dotnet build -c Debug -p:Platform=x64
dotnet run -c Debug -p:Platform=x64 --no-build
```

Or open `Dustan.csproj` in Visual Studio and run the **Dustan** profile (unpackaged).

### Publish a Release zip

```powershell
.\scripts\Publish-Dustan.ps1
```

Produces `artifacts\Dustan-<version>-win-x64.zip`, where `<version>` comes from the `<Version>` property in `Dustan.csproj`.
