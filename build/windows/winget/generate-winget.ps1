param (
    [string]$Version,
    [string]$InstallerUrl,
    [string]$InstallerSha256,
    [string]$InstallerArm64Url,
    [string]$InstallerArm64Sha256
)

if ([string]::IsNullOrEmpty($InstallerUrl)) {
    $InstallerUrl = "https://github.com/ShareX/XerahS/releases/download/v$Version/XerahS-$Version-win-x64.exe"
}

if ([string]::IsNullOrEmpty($InstallerArm64Url)) {
    $InstallerArm64Url = "https://github.com/ShareX/XerahS/releases/download/v$Version/XerahS-$Version-win-arm64.exe"
}

$PackageIdentifier = "ShareX.XerahS"
$Publisher = "ShareX Team"
$PackageName = "XerahS"
$ShortDescription = "A cross-platform port of ShareX built with Avalonia UI." # Fallback
$PackageUrl = "https://github.com/ShareX/XerahS"
$License = "GPL-3.0-only"

if ([string]::IsNullOrEmpty($PSScriptRoot)) {
    $ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
} else {
    $ScriptPath = $PSScriptRoot
}

$RepoRoot = Resolve-Path (Join-Path $ScriptPath "..\..\..")
$ReadmePath = Join-Path $RepoRoot "README.md"

if (Test-Path $ReadmePath) {
    $ReadmeContent = Get-Content $ReadmePath
    # Find first non-empty line after title
    $DescriptionLine = $ReadmeContent | Select-Object -Skip 1 | Where-Object { $_ -match "\S" } | Select-Object -First 1
    if ($DescriptionLine) {
        # meaningful description found, remove markdown formatting
        $ShortDescription = $DescriptionLine -replace "\*\*", "" -replace "`r", "" -replace "`n", ""
    }
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
