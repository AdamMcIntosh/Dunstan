#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes Dustan as a self-contained desktop build and zips it.

.DESCRIPTION
  Output zip: artifacts/Dustan-<version>-<runtime>.zip
  Version comes from -Version, otherwise <Version> in Dustan.Desktop.csproj.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$csproj = Join-Path (Join-Path $root "Dustan.Desktop") "Dustan.Desktop.csproj"
if (-not (Test-Path $csproj)) {
    throw "Dustan.Desktop.csproj not found at $csproj"
}

Set-Location $root

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$proj = Get-Content -Raw $csproj
    $versionNode = Select-Xml -Xml $proj -XPath "//*[local-name()='Version']" | Select-Object -First 1
    if (-not $versionNode) {
        throw "Could not read <Version> from Dustan.Desktop.csproj"
    }
    $Version = $versionNode.Node.InnerText.Trim()
}

$Version = $Version.Trim().TrimStart('v', 'V')
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version is empty"
}

$publishDir = Join-Path (Join-Path (Join-Path $root "artifacts") "publish") $Runtime
$artifactsDir = Join-Path $root "artifacts"
$zipPath = Join-Path $artifactsDir "Dustan-$Version-$Runtime.zip"
$useR2R = $Runtime -eq "win-x64"

Write-Host "Publishing Dustan $Version ($Configuration, $Runtime, self-contained)..."

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

dotnet publish $csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishReadyToRun=$useR2R `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$unixExe = Join-Path $publishDir "Dustan"
$winExe = Join-Path $publishDir "Dustan.exe"
if (Test-Path $winExe) {
    $exe = $winExe
}
elseif (Test-Path $unixExe) {
    $exe = $unixExe
    if (Get-Command chmod -ErrorAction SilentlyContinue) {
        & chmod +x $exe
    }
}
else {
    throw "Expected Dustan executable was not produced in $publishDir"
}

if ($Runtime -like "win-*") {
    $libvlc = Get-ChildItem -Path $publishDir -Recurse -Filter "libvlc.dll" | Select-Object -First 1
    if (-not $libvlc) {
        throw "libvlc.dll was not copied into the Windows publish output"
    }
}

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-Host "Zipping to $zipPath..."
if (Get-Command zip -ErrorAction SilentlyContinue) {
    Push-Location $publishDir
    try {
        & zip -r -y $zipPath .
        if ($LASTEXITCODE -ne 0) {
            throw "zip failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}
else {
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
}

Write-Host "Done."
Write-Host "  Publish folder: $publishDir"
Write-Host "  Executable:     $exe"
Write-Host "  Zip:            $zipPath"
