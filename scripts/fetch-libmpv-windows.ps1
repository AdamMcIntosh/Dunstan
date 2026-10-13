<#
.SYNOPSIS
  Downloads the latest libmpv development build for Windows x64 (shinchiro/mpv-winbuild-cmake)
  and creates the MSVC import library mpv.lib next to libmpv-2.dll.

.EXAMPLE
  ./scripts/fetch-libmpv-windows.ps1 -Destination deps/mpv
  cmake ... -DMPV_DIR=deps/mpv -DMPV_RUNTIME=deps/mpv/libmpv-2.dll

  Run from an MSVC developer prompt (needs lib.exe and dumpbin.exe), with the GitHub CLI signed in
  or GH_TOKEN set.
#>
param([string]$Destination = "deps/mpv")
$ErrorActionPreference = "Stop"

$release = gh api repos/shinchiro/mpv-winbuild-cmake/releases/latest | ConvertFrom-Json
# The plain x86_64 build runs on any x64 CPU; the "-v3" variant needs AVX2.
$asset = $release.assets | Where-Object { $_.name -match '^mpv-dev-x86_64-\d+-git-[0-9a-f]+\.7z$' } | Select-Object -First 1
if (-not $asset) { throw "No mpv-dev-x86_64 archive in the latest shinchiro release." }

New-Item -ItemType Directory -Force $Destination | Out-Null
$archive = Join-Path $Destination $asset.name
Write-Host "Downloading $($asset.name)"
Invoke-WebRequest $asset.browser_download_url -OutFile $archive
7z x $archive "-o$Destination" -y | Out-Null

$dll = Join-Path $Destination "libmpv-2.dll"
if (-not (Test-Path $dll)) { throw "libmpv-2.dll not found in $($asset.name)" }
$def = Join-Path $Destination "mpv.def"
if (-not (Test-Path $def)) {
    $exports = dumpbin /nologo /exports $dll |
        Select-String '^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]{8}\s+(mpv_\S+)' |
        ForEach-Object { $_.Matches[0].Groups[1].Value }
    if (-not $exports) { throw "Could not read exports from libmpv-2.dll" }
    @("LIBRARY libmpv-2", "EXPORTS") + $exports | Set-Content $def
}
lib /nologo "/def:$def" /machine:x64 "/out:$(Join-Path $Destination 'mpv.lib')"
Write-Host "libmpv ready in $Destination"
