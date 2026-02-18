#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$imageEditorPath = Join-Path $repoRoot "ImageEditor"

if (-not (Test-Path $imageEditorPath)) {
    exit 0
}

try {
    git -C $imageEditorPath rev-parse --is-inside-work-tree *> $null
} catch {
    exit 0
}

try {
    git -C $imageEditorPath symbolic-ref -q HEAD *> $null
    if ($LASTEXITCODE -eq 0) {
        exit 0
    }
} catch {
}

$porcelain = git -C $imageEditorPath status --porcelain 2>$null
if ($porcelain) {
    Write-Host "WARN: ImageEditor is detached but has local changes; skipping auto-checkout." -ForegroundColor Yellow
    exit 0
}

$defaultRef = (git -C $imageEditorPath symbolic-ref --quiet refs/remotes/origin/HEAD 2>$null)
$defaultBranch = $null
if ($defaultRef -and $defaultRef.StartsWith("refs/remotes/origin/")) {
    $defaultBranch = $defaultRef.Substring("refs/remotes/origin/".Length)
}

if (-not $defaultBranch) {
    foreach ($candidate in @("develop", "main", "master")) {
        git -C $imageEditorPath show-ref --verify --quiet "refs/remotes/origin/$candidate"
        if ($LASTEXITCODE -eq 0) {
            $defaultBranch = $candidate
            break
        }
        git -C $imageEditorPath show-ref --verify --quiet "refs/heads/$candidate"
        if ($LASTEXITCODE -eq 0) {
            $defaultBranch = $candidate
            break
        }
    }
}

if (-not $defaultBranch) {
    Write-Host "WARN: ImageEditor is detached and default branch could not be detected." -ForegroundColor Yellow
    exit 0
}

git -C $imageEditorPath show-ref --verify --quiet "refs/heads/$defaultBranch"
if ($LASTEXITCODE -eq 0) {
    git -C $imageEditorPath checkout $defaultBranch *> $null
} else {
    git -C $imageEditorPath show-ref --verify --quiet "refs/remotes/origin/$defaultBranch"
    if ($LASTEXITCODE -eq 0) {
        git -C $imageEditorPath checkout -B $defaultBranch "origin/$defaultBranch" *> $null
    } else {
        Write-Host "WARN: ImageEditor default branch '$defaultBranch' not available locally." -ForegroundColor Yellow
        exit 0
    }
}

$upstreamRef = (git -C $imageEditorPath rev-parse --abbrev-ref --symbolic-full-name "$defaultBranch@{upstream}" 2>$null)
if ($upstreamRef) {
    git -C $imageEditorPath merge --ff-only $upstreamRef *> $null
}

Write-Host "INFO: ImageEditor detached HEAD fixed -> $defaultBranch"
