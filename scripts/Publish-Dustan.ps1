#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes Dustan as an unpackaged, self-contained win-x64 build and zips it.

.DESCRIPTION
  Output zip: artifacts/Dustan-<version>-win-x64.zip
  Version is read from Dustan.csproj <Version>.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$csproj = Join-Path $root "Dustan.csproj"
if (-not (Test-Path $csproj)) {
    throw "Dustan.csproj not found at $csproj"
}

Set-Location $root

[xml]$proj = Get-Content -Raw $csproj
$versionNode = Select-Xml -Xml $proj -XPath "//*[local-name()='Version']" | Select-Object -First 1
if (-not $versionNode) {
    throw "Could not read <Version> from Dustan.csproj"
}
$version = $versionNode.Node.InnerText.Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read <Version> from Dustan.csproj"
}

$publishDir = Join-Path $root "artifacts\publish\$Runtime"
$artifactsDir = Join-Path $root "artifacts"
$zipPath = Join-Path $artifactsDir "Dustan-$version-$Runtime.zip"

Write-Host "Publishing Dustan $version ($Configuration, $Runtime, self-contained unpackaged)..."

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

dotnet publish $csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $publishDir "Dustan.exe"
if (-not (Test-Path $exe)) {
    throw "Expected Dustan.exe was not produced at $exe"
}

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-Host "Zipping to $zipPath..."
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Done."
Write-Host "  Publish folder: $publishDir"
Write-Host "  Zip:            $zipPath"
