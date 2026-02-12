# XIP Migration Script - Migrates XIP documents to GitHub Issues
# Usage: .\migrate-xips.ps1

$ErrorActionPreference = "Continue"

# Ensure we're in the repo directory
Set-Location "C:\Users\liveu\source\repos\ShareX Team\XerahS"

# Get all XIP files
$xipFiles = Get-ChildItem -Path "tasks" -Recurse -Filter "*.md" | Sort-Object FullName

Write-Host "Found $($xipFiles.Count) XIP documents to migrate" -ForegroundColor Cyan
Write-Host ""

$createdIssues = @()

foreach ($file in $xipFiles) {
    $folder = $file.Directory.Name
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $lines = $content -split "`n"
    
    # Determine status based on folder
    $status = switch ($folder) {
        "1-new" { "New" }
        "2-pending" { "Pending" }
        "3-complete" { "Complete" }
        default { "Unknown" }
    }
    
    # Extract XIP number from filename
    $xipNumber = ""
    if ($file.Name -match "XIP(\d+)") {
        $xipNumber = $Matches[1]
    } elseif ($file.Name -match "XIP-?(\d+)") {
        $xipNumber = $Matches[1]
    }
    
    # Extract title from first heading
    $title = ""
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        # Check for markdown heading
        if ($trimmed -match "^#+\s*(.+)$") {
            $title = $Matches[1].Trim()
            break
        }
        # Check for frontmatter title
        if ($trimmed -match "^title:\s*(.+)$") {
            $title = $Matches[1].Trim()
            break
        }
    }
    
    # Clean up title
    $title = $title -replace "^AG\d+:\s*", ""
    $title = $title -replace "^CX\d+:\s*", ""
    $title = $title -replace "^CP\d+:\s*", ""
    $title = $title -replace "^SIP\d+:\s*", ""
    $title = $title -replace "^XIP\d+:\s*", ""
    $title = $title.Trim()
    
    # Build issue title
    if ($xipNumber) {
        $issueTitle = "[XIP-$xipNumber] $title"
    } else {
        $issueTitle = $title
    }
    
    # Truncate if too long
    if ($issueTitle.Length -gt 120) {
        $issueTitle = $issueTitle.Substring(0, 117) + "..."
    }
    
    # Build issue body
    $body = @"
## XIP Document

**File**: \`$($file.FullName.Replace("C:\Users\liveu\source\repos\ShareX Team\XerahS\", "").Replace("\", "/"))\`

**Status**: $status

**Folder**: $folder

---

$content
"@
    
    Write-Host "Creating issue: $issueTitle" -ForegroundColor Yellow
    Write-Host "  Folder: $folder -> Status: $status" -ForegroundColor Gray
    
    try {
        # Create the issue
        $issueUrl = $body | gh issue create --title $issueTitle --label "xip" --body-file -
        
        if ($issueUrl -match "/issues/(\d+)") {
            $issueNumber = $Matches[1]
            $createdIssues += [PSCustomObject]@{
                Number = $issueNumber
                Title = $issueTitle
                Folder = $folder
                Status = $status
                Url = $issueUrl
            }
            
            Write-Host "  Created: $issueUrl" -ForegroundColor Green
            
            # Close the issue if it's in 3-complete
            if ($folder -eq "3-complete") {
                Write-Host "  Closing issue #$issueNumber (completed)" -ForegroundColor DarkYellow
                gh issue close $issueNumber --reason completed | Out-Null
            }
        } else {
            Write-Host "  WARNING: Could not extract issue number from output" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ERROR: $_" -ForegroundColor Red
    }
    
    Write-Host ""
    
    # Small delay to avoid rate limiting
    Start-Sleep -Milliseconds 500
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Migration Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total files processed: $($xipFiles.Count)" -ForegroundColor White
Write-Host "Issues created: $($createdIssues.Count)" -ForegroundColor White
Write-Host ""

$openIssues = $createdIssues | Where-Object { $_.Folder -ne "3-complete" }
$closedIssues = $createdIssues | Where-Object { $_.Folder -eq "3-complete" }

Write-Host "Open issues (1-new, 2-pending): $($openIssues.Count)" -ForegroundColor Green
foreach ($issue in $openIssues) {
    Write-Host "  #$($issue.Number): $($issue.Title)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Closed issues (3-complete): $($closedIssues.Count)" -ForegroundColor DarkYellow
foreach ($issue in $closedIssues) {
    Write-Host "  #$($issue.Number): $($issue.Title)" -ForegroundColor Gray
}
