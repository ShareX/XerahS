#
# Pre-commit hook to validate GPL v3 license headers in C# files (PowerShell version)
# This hook checks all staged .cs files for proper license headers
#
# To install: git config core.hooksPath .githooks
# To bypass: git commit --no-verify
#

$ErrorActionPreference = "Stop"

# Expected header components
$EXPECTED_PROJECT = "XerahS - The Avalonia UI implementation of ShareX"
$EXPECTED_COPYRIGHT = "Copyright (c) 2007-2026 ShareX Team"
$EXPECTED_GPL_START = "This program is free software"

# Get list of staged C# files
$STAGED_CS_FILES = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.cs$' }

if (-not $STAGED_CS_FILES) {
    Write-Host "✓ No C# files to check" -ForegroundColor Green
    exit 0
}

Write-Host "Checking license headers in staged C# files..."

$VIOLATIONS = 0
$VIOLATION_FILES = @()

foreach ($FILE in $STAGED_CS_FILES) {
    if (-not (Test-Path $FILE)) {
        continue
    }

    # Read first 30 lines (header should be within this)
    $HEADER = Get-Content $FILE -TotalCount 30 -Raw

    # Check for required components
    $MISSING = @()

    if ($HEADER -notmatch [regex]::Escape($EXPECTED_PROJECT)) {
        $MISSING += "project name"
    }

    if ($HEADER -notmatch [regex]::Escape($EXPECTED_COPYRIGHT)) {
        $MISSING += "copyright year 2026"
    }

    if ($HEADER -notmatch [regex]::Escape($EXPECTED_GPL_START)) {
        $MISSING += "GPL v3 license text"
    }

    # Check if header is before any code (should be after #region License Information)
    if ($HEADER -notmatch "#region License Information") {
        $MISSING += "#region License Information tag"
    }

    if ($MISSING.Count -gt 0) {
        $VIOLATIONS++
        $VIOLATION_FILES += $FILE
        Write-Host "✗ $FILE" -ForegroundColor Red
        Write-Host "  Missing: $($MISSING -join ', ')" -ForegroundColor Yellow
    }
}

if ($VIOLATIONS -gt 0) {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host "  LICENSE HEADER VALIDATION FAILED" -ForegroundColor Red
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host ""
    Write-Host "$VIOLATIONS file(s) have incorrect or missing license headers:" -ForegroundColor Yellow
    foreach ($FILE in $VIOLATION_FILES) {
        Write-Host "  → $FILE" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Expected header format:" -ForegroundColor Yellow
    Write-Host "  #region License Information (GPL v3)"
    Write-Host "  /*"
    Write-Host "      $EXPECTED_PROJECT"
    Write-Host "      $EXPECTED_COPYRIGHT"
    Write-Host ""
    Write-Host "      This program is free software; you can redistribute it and/or"
    Write-Host "      modify it under the terms of the GNU General Public License"
    Write-Host "      ..."
    Write-Host "  */"
    Write-Host "  #endregion License Information (GPL v3)"
    Write-Host ""
    Write-Host "To fix:" -ForegroundColor Yellow
    Write-Host "  1. Update headers manually, or"
    Write-Host "  2. Run: pwsh docs/scripts/fix_license_headers.ps1"
    Write-Host "  3. Re-stage files: git add <files>"
    Write-Host ""
    Write-Host "To bypass this check (NOT RECOMMENDED):" -ForegroundColor Yellow
    Write-Host "  git commit --no-verify"
    Write-Host ""
    exit 1
}

$fileCount = ($STAGED_CS_FILES | Measure-Object).Count
Write-Host "✓ All staged C# files have valid license headers ($fileCount files checked)" -ForegroundColor Green
exit 0
