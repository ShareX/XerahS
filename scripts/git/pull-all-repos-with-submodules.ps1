param(
    [string]$RootPath = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DefaultBranchName {
    param(
        [Parameter(Mandatory = $true)][string]$RepoPath
    )

    $defaultRef = git -C $RepoPath symbolic-ref --short refs/remotes/origin/HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $defaultRef) {
        return ($defaultRef -replace '^origin/', '')
    }

    $remoteInfo = git -C $RepoPath remote show origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $remoteInfo) {
        $headLine = $remoteInfo | Select-String 'HEAD branch:' | Select-Object -First 1
        if ($headLine) {
            return (($headLine.ToString().Split(':')[-1]).Trim())
        }
    }

    $mainExists = git -C $RepoPath rev-parse --verify origin/main 2>$null
    if ($LASTEXITCODE -eq 0 -and $mainExists) {
        return 'main'
    }

    $masterExists = git -C $RepoPath rev-parse --verify origin/master 2>$null
    if ($LASTEXITCODE -eq 0 -and $masterExists) {
        return 'master'
    }

    throw "Could not determine default branch for '$RepoPath'."
}

function Update-SubmoduleToLatest {
    param(
        [Parameter(Mandatory = $true)][string]$ParentRepoPath,
        [Parameter(Mandatory = $true)][string]$SubmodulePath
    )

    $submoduleRepoPath = Join-Path $ParentRepoPath $SubmodulePath

    if (-not (Test-Path $submoduleRepoPath)) {
        throw "Submodule path does not exist: $submoduleRepoPath"
    }

    git -C $submoduleRepoPath fetch origin --prune
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch submodule '$SubmodulePath'."
    }

    $defaultBranch = Get-DefaultBranchName -RepoPath $submoduleRepoPath
    $branchExists = git -C $submoduleRepoPath branch --list $defaultBranch

    if ($branchExists) {
        git -C $submoduleRepoPath checkout $defaultBranch
    }
    else {
        git -C $submoduleRepoPath checkout -b $defaultBranch --track "origin/$defaultBranch"
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to checkout branch '$defaultBranch' in submodule '$SubmodulePath'."
    }

    git -C $submoduleRepoPath pull --ff-only origin $defaultBranch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pull latest '$defaultBranch' in submodule '$SubmodulePath'."
    }

    $status = git -C $submoduleRepoPath status --short --branch
    Write-Output "    [$SubmodulePath] $status"
}

function Update-Repository {
    param(
        [Parameter(Mandatory = $true)][string]$RepoPath
    )

    Write-Output ""
    Write-Output ">>> Updating repository: $RepoPath"

    git -C $RepoPath pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        throw "git pull failed in '$RepoPath'."
    }

    $gitmodulesPath = Join-Path $RepoPath '.gitmodules'
    if (-not (Test-Path $gitmodulesPath)) {
        Write-Output "    No submodules found."
        return
    }

    git -C $RepoPath submodule sync --recursive
    if ($LASTEXITCODE -ne 0) {
        throw "git submodule sync failed in '$RepoPath'."
    }

    git -C $RepoPath submodule update --init --recursive
    if ($LASTEXITCODE -ne 0) {
        throw "git submodule update failed in '$RepoPath'."
    }

    $submodulePaths = @(git -C $RepoPath config --file .gitmodules --get-regexp path 2>$null |
        ForEach-Object {
            $parts = $_ -split '\s+', 2
            if ($parts.Count -eq 2) { $parts[1].Trim() }
        } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    if ($submodulePaths.Count -eq 0) {
        Write-Output "    Submodule config exists, but no paths found."
        return
    }

    Write-Output "    Attaching and updating submodules to latest default branch:"
    foreach ($submodulePath in $submodulePaths) {
        Update-SubmoduleToLatest -ParentRepoPath $RepoPath -SubmodulePath $submodulePath
    }

    $parentStatus = git -C $RepoPath status --short
    if ([string]::IsNullOrWhiteSpace($parentStatus)) {
        Write-Output "    Parent repository working tree is clean."
    }
    else {
        Write-Output "    Parent repository has changes after submodule updates:"
        Write-Output $parentStatus
    }
}

$resolvedRoot = Resolve-Path -Path $RootPath
$gitDirs = @(Get-ChildItem -Path $resolvedRoot -Recurse -Directory -Force -Filter '.git')

if (-not $gitDirs) {
    Write-Output "No Git repositories found under '$resolvedRoot'."
    exit 0
}

$repoRoots = @($gitDirs | ForEach-Object { $_.Parent.FullName } | Sort-Object -Unique)

Write-Output "Found $($repoRoots.Count) Git repository(ies) under '$resolvedRoot'."
foreach ($repoRoot in $repoRoots) {
    Update-Repository -RepoPath $repoRoot
}

Write-Output ""
Write-Output 'All repositories processed successfully.'
