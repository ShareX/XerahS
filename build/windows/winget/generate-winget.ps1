param (
    [string]$Version,
    [string]$InstallerUrl,
    [string]$InstallerSha256,
    [string]$InstallerArm64Url,
    [string]$InstallerArm64Sha256
)

$PackageIdentifier = "ShareX.XerahS"
$Publisher = "ShareX Team"
$PackageName = "XerahS"
$ShortDescription = "ShareX mod with extra features."
$PackageUrl = "https://github.com/ShareX/XerahS"
$License = "GPL-3.0-only"

if ([string]::IsNullOrEmpty($PSScriptRoot)) {
    $ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
} else {
    $ScriptPath = $PSScriptRoot
}



$ManifestDir = Join-Path $ScriptPath "manifests"
if (!(Test-Path $ManifestDir)) {
    New-Item -ItemType Directory -Path $ManifestDir -Force | Out-Null
}

$VersionDir = Join-Path $ManifestDir $Version
if (!(Test-Path $VersionDir)) {
    New-Item -ItemType Directory -Path $VersionDir -Force | Out-Null
}

$InstallersYaml = @"
Installers:
  - Architecture: x64
    InstallerUrl: $InstallerUrl
    InstallerSha256: $InstallerSha256
    InstallerType: nullsoft
"@

if (![string]::IsNullOrEmpty($InstallerArm64Url)) {
    $InstallersYaml += @"

  - Architecture: arm64
    InstallerUrl: $InstallerArm64Url
    InstallerSha256: $InstallerArm64Sha256
    InstallerType: nullsoft
"@
}

$ManifestContent = @"
PackageIdentifier: $PackageIdentifier
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: singleton
ManifestVersion: 1.4.0
Publisher: $Publisher
PackageName: $PackageName
License: $License
ShortDescription: $ShortDescription
PackageUrl: $PackageUrl
$InstallersYaml
"@

$ManifestPath = Join-Path $VersionDir "$PackageIdentifier.yaml"
$ManifestContent | Set-Content -Path $ManifestPath
Write-Host "Generated WinGet manifest at $ManifestPath"
