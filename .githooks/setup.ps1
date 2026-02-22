#!/usr/bin/env pwsh
#
# Setup script for XerahS Git hooks
# This script configures Git to use the hooks in the .githooks directory
#

param(
    [switch]$Force,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

function Write-Success {
    param([string]$Message)
    Write-Host "OK: $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "INFO: $Message" -ForegroundColor Cyan
}

function Write-Warning {
    param([string]$Message)
    Write-Host "WARN: $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "FAIL: $Message" -ForegroundColor Red
}

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Error "This script must be run from the root of the XerahS repository"
    exit 1
}

Write-Info "XerahS Git Hooks Setup"
Write-Host ""

if ($Uninstall) {
    Write-Info "Uninstalling Git hooks..."

    # Remove hooks path configuration
    $currentHooksPath = git config --get core.hooksPath
    if ($currentHooksPath -eq ".githooks") {
        git config --unset core.hooksPath
        Write-Success "Removed core.hooksPath configuration"
    } else {
        Write-Info "Hooks path not set to .githooks, skipping"
    }

    Write-Success "Git hooks uninstalled"
    Write-Info "Standard .git/hooks directory will be used"
    exit 0
}

# Check current configuration
$currentHooksPath = git config --get core.hooksPath

if ($currentHooksPath) {
    if ($currentHooksPath -eq ".githooks") {
        if (-not $Force) {
            Write-Success "Git hooks are already configured correctly"
            Write-Info "Use -Force to reconfigure"
            exit 0
        } else {
            Write-Info "Reconfiguring hooks (force mode)"
        }
    } else {
        Write-Warning "core.hooksPath is currently set to: $currentHooksPath"
        if (-not $Force) {
            Write-Error "Use -Force to override existing configuration"
            exit 1
        }
        Write-Info "Overriding existing configuration"
    }
}

# Configure Git to use .githooks directory
try {
    git config core.hooksPath .githooks
    Write-Success "Configured Git to use .githooks directory"
} catch {
    Write-Error "Failed to configure Git hooks: $_"
    exit 1
}

# Verify hooks exist
$hookFiles = @(
    ".githooks/pre-commit",
    ".githooks/pre-commit.bash",
    ".githooks/pre-commit.ps1",
    ".githooks/pre-push",
    ".githooks/post-checkout",
    ".githooks/post-merge",
    ".githooks/sync-imageeditor-head",
    ".githooks/sync-imageeditor-head.bash",
    ".githooks/sync-imageeditor-head.ps1"
)

$missingHooks = @()
foreach ($hook in $hookFiles) {
    if (-not (Test-Path $hook)) {
        $missingHooks += $hook
    }
}

if ($missingHooks.Count -gt 0) {
    Write-Warning "Some hook files are missing:"
    foreach ($hook in $missingHooks) {
        Write-Host "  -> $hook" -ForegroundColor Yellow
    }
}

# Make bash script executable on Unix-like systems
if ($IsLinux -or $IsMacOS) {
    try {
        chmod +x .githooks/pre-commit
        chmod +x .githooks/pre-commit.bash
        chmod +x .githooks/pre-push
        chmod +x .githooks/post-checkout
        chmod +x .githooks/post-merge
        chmod +x .githooks/sync-imageeditor-head
        chmod +x .githooks/sync-imageeditor-head.bash
        Write-Success "Made git hook scripts executable"
    } catch {
        Write-Warning "Could not set execute permission on one or more hook scripts"
    }
}

# Test if PowerShell hooks can be executed on Windows
if ($IsWindows) {
    $executionPolicy = Get-ExecutionPolicy
    if ($executionPolicy -eq "Restricted") {
        Write-Warning "PowerShell execution policy is Restricted"
        Write-Info "Hooks may not run. To fix, run as Administrator:"
        Write-Host "  Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Success "Git hooks setup complete!"
Write-Host ""
Write-Info "Active hooks:"
Write-Host "  * pre-commit - Validates GPL v3 license headers in C#, Swift, and Kotlin files"
Write-Host "  * pre-push - Auto-syncs and auto-pushes ImageEditor branch commits"
Write-Host "  * post-checkout - Ensures ImageEditor is on default branch when detached"
Write-Host "  * post-merge - Ensures ImageEditor is on default branch when detached"
Write-Host ""
Write-Info "To test the hooks:"
Write-Host "  1. Stage a C# file: git add src/SomeFile.cs"
if ($IsWindows) {
    Write-Host "  2. Run hook manually: pwsh .githooks/pre-commit.ps1"
} else {
    Write-Host "  2. Run hook manually: .githooks/pre-commit"
}
Write-Host "  3. Commit as normal: git commit -m 'Test commit'"
Write-Host ""
Write-Info "To bypass hooks (not recommended):"
Write-Host "  git commit --no-verify"
Write-Host ""
Write-Info "For more information, see .githooks/README.md"
