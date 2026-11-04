#!/usr/bin/env pwsh
# Link host skill folders to the single source in .ai/skills so Claude Code
# (.claude/skills) and Codex (.agents/skills) discover skills natively.
# Windows uses directory junctions (no admin or Developer Mode needed).
# Idempotent. Never replaces a real directory or file. Links are gitignored.
# Usage: scripts/link-agent-skills.ps1 [-Check] [-Quiet]
param(
    [switch]$Check,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$target = Join-Path $repoRoot '.ai/skills'
$missing = $false

function Say([string]$Message) { if (-not $Quiet) { Write-Host $Message } }

foreach ($hostDir in @('.claude', '.agents')) {
    $link = Join-Path $repoRoot "$hostDir/skills"
    $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue

    if ($item) {
        $isLink = $item.Attributes -band [IO.FileAttributes]::ReparsePoint
        $points = $isLink -and ((Resolve-Path -LiteralPath $link).Path -eq (Resolve-Path -LiteralPath $target).Path)
        if ($points) { Say "OK: $hostDir/skills"; continue }
        Write-Warning "SKIP: $hostDir/skills exists and is not a link to .ai/skills; leaving it unchanged."
        continue
    }

    $missing = $true
    if ($Check) { Say "MISSING: $hostDir/skills"; continue }

    New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot $hostDir) | Out-Null
    if ($IsLinux -or $IsMacOS) {
        New-Item -ItemType SymbolicLink -Path $link -Target '../.ai/skills' | Out-Null
    } else {
        New-Item -ItemType Junction -Path $link -Target $target | Out-Null
    }
    Say "LINKED: $hostDir/skills -> .ai/skills"
}

if ($Check -and $missing) { exit 1 }
exit 0
