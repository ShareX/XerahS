#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$imageEditorPath = Join-Path $repoRoot "ImageEditor"

function Test-Truthy {
    param([string]$Value)

    if (-not $Value) {
        return $false
    }

    switch ($Value.Trim().ToLowerInvariant()) {
        "1" { return $true }
        "true" { return $true }
        "yes" { return $true }
        "on" { return $true }
        default { return $false }
    }
}

$autoPushSetting = $env:XERAHS_IMAGEEDITOR_AUTO_PUSH
if (-not $autoPushSetting) {
    $autoPushSetting = (git -C $repoRoot config --bool --get xerahs.hooks.imageeditorautopush 2>$null)
}

$autoPushEnabled = Test-Truthy $autoPushSetting

if (-not (Test-Path $imageEditorPath)) {
    exit 0
}

try {
    git -C $imageEditorPath rev-parse --is-inside-work-tree *> $null
} catch {
    exit 0
}

$branchName = (git -C $imageEditorPath branch --show-current 2>$null)
$wasDetached = $false

if (-not $branchName) {
    $wasDetached = $true

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

    $branchName = $defaultBranch
    Write-Host "INFO: ImageEditor detached HEAD fixed -> $defaultBranch"
}

if (-not $autoPushEnabled) {
    exit 0
}

if (-not $branchName) {
    Write-Host "WARN: ImageEditor auto-push skipped because no active branch is checked out." -ForegroundColor Yellow
    exit 0
}

$upstreamRef = (git -C $imageEditorPath rev-parse --abbrev-ref --symbolic-full-name "$branchName@{upstream}" 2>$null)
if (-not $upstreamRef) {
    git -C $imageEditorPath show-ref --verify --quiet "refs/remotes/origin/$branchName"
    if ($LASTEXITCODE -eq 0) {
        git -C $imageEditorPath branch --set-upstream-to "origin/$branchName" $branchName *> $null
        $upstreamRef = (git -C $imageEditorPath rev-parse --abbrev-ref --symbolic-full-name "$branchName@{upstream}" 2>$null)
    }
}

if (-not $upstreamRef) {
    git -C $imageEditorPath push -u origin $branchName *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "INFO: ImageEditor branch '$branchName' pushed and upstream set."
    } else {
        Write-Host "WARN: ImageEditor auto-push failed for '$branchName' (set upstream)." -ForegroundColor Yellow
    }
    exit 0
}

$aheadCountRaw = (git -C $imageEditorPath rev-list --count "$upstreamRef..HEAD" 2>$null)
[int]$aheadCount = 0
if ($aheadCountRaw) {
    [int]::TryParse($aheadCountRaw, [ref]$aheadCount) *> $null
}

if ($aheadCount -eq 0) {
    if ($wasDetached) {
        Write-Host "INFO: ImageEditor auto-push skipped; '$branchName' is already up to date."
    }
    exit 0
}

git -C $imageEditorPath push *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Host "INFO: ImageEditor auto-pushed $aheadCount commit(s) from '$branchName'."
} else {
    Write-Host "WARN: ImageEditor auto-push failed for '$branchName'." -ForegroundColor Yellow
}
