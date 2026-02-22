#requires -Version 5.1
param(
    [switch]$WhatIf,
    [switch]$Force
)
# Cleanup script: remove legacy (pre-XIP0036) folders under src/.
# After XIP0036, the solution uses src/desktop/, src/platform/, src/mobile-experimental/, src/mobile/ only.
# Copyright (c) 2007-2026 ShareX Team.

$ErrorActionPreference = "Stop"

$Root = if ($PSScriptRoot) {
    Resolve-Path (Join-Path $PSScriptRoot "..")
} else {
    Resolve-Path "."
}
$Src = Join-Path $Root "src"
if (-not (Test-Path $Src)) {
    Write-Error "src/ not found at $Src"
    exit 1
}

# XIP0036 canonical top-level folders under src/ (do not remove)
$KeepFolders = @("desktop", "platform", "mobile", "mobile-experimental")

# Legacy folders to remove (flat projects and old Plugins; per tasks/complete/XIP0036_src_restructure.md)
$LegacyFolders = @(
    "Plugins",
    "XerahS.App",
    "XerahS.Audits.Tool",
    "XerahS.Bootstrap",
    "XerahS.CLI",
    "XerahS.Common",
    "XerahS.Core",
    "XerahS.History",
    "XerahS.Indexer",
    "XerahS.Media",
    "XerahS.Mobile.Android",
    "XerahS.Mobile.Ava",
    "XerahS.Mobile.Core",
    "XerahS.Mobile.iOS",
    "XerahS.Mobile.iOS.ShareExtension",
    "XerahS.Mobile.Maui",
    "XerahS.Mobile.UI",
    "XerahS.Platform.Abstractions",
    "XerahS.Platform.Linux",
    "XerahS.Platform.MacOS",
    "XerahS.Platform.Mobile",
    "XerahS.Platform.Windows",
    "XerahS.PluginExporter",
    "XerahS.RegionCapture",
    "XerahS.Services",
    "XerahS.Services.Abstractions",
    "XerahS.UI",
    "XerahS.Uploaders",
    "XerahS.ViewModels",
    "XerahS.WatchFolder.Daemon"
)

$toRemove = @()
foreach ($name in $LegacyFolders) {
    $path = Join-Path $Src $name
    if (Test-Path $path -PathType Container) {
        $toRemove += $path
    }
}

if ($toRemove.Count -eq 0) {
    Write-Host "No legacy src folders found. src/ is already clean (XIP0036)."
    exit 0
}

Write-Host "Legacy src folders to remove (XIP0036 cleanup):"
foreach ($p in $toRemove) {
    Write-Host "  - $p"
}
Write-Host ""

if (-not $Force -and -not $WhatIf) {
    $confirm = Read-Host "Remove these $($toRemove.Count) folder(s)? [y/N]"
    if ($confirm -notmatch '^[yY]') {
        Write-Host "Aborted."
        exit 0
    }
}

$removed = 0
foreach ($path in $toRemove) {
    try {
        if ($WhatIf) {
            Write-Host "[WhatIf] Would remove: $path"
        } else {
            Remove-Item -Path $path -Recurse -Force
            Write-Host "Removed: $path"
        }
        $removed++
    } catch {
        Write-Error "Failed to remove $path : $_"
        exit 1
    }
}

if ($WhatIf) {
    Write-Host "[WhatIf] Would remove $removed folder(s). Run without -WhatIf to apply."
} else {
    Write-Host "Done. Removed $removed legacy folder(s)."
}
