
$root = "C:\Users\liveu\source\repos\ShareX Team\XerahS"
$docs = "$root\docs\audits"
if (!(Test-Path $docs)) { New-Item -ItemType Directory -Force -Path $docs | Out-Null }
$reportPath = "$docs\unused_enums_audit.md"

$files = Get-ChildItem -Path $root -Recurse -Filter *.cs

# 1. Collect all enums
$enums = @() # Objects { Name, File, Line, Context }

Write-Host "Scanning $($files.Count) files for enums..."

# Pre-read all code for usage check later to save IO
# Use a StringBuilder for memory efficiency if needed, but string implies easy regex
$sb = [System.Text.StringBuilder]::new()

foreach ($file in $files) {
    if ($file.FullName -like "*\obj\*" -or $file.FullName -like "*\bin\*") { continue }
    
    try {
        $content = Get-Content $file.FullName
        $rawContent = $content -join "`n"
        [void]$sb.Append($rawContent + "`n")

        for ($i = 0; $i -lt $content.Count; $i++) {
            $line = $content[$i]
            # Match 'enum Name' or 'public enum Name' etc.
            if ($line -match '\benum\s+(\w+)') {
                $enumName = $matches[1]
                
                # Context attempt (look backwards for class/namespace)
                $context = "Global/Unknown"
                # Scan backwards max 50 lines to find container
                for ($j = $i - 1; $j -ge 0 -and $j -ge ($i - 50); $j--) {
                    $prev = $content[$j].Trim()
                    if ($prev -match '^(public |private |internal |protected |static |partial )*(class|struct|namespace)\s+(\w+)') {
                        $context = $matches[0]
                        break
                    }
                }
                
                $enums += [PSCustomObject]@{
                    Name = $enumName
                    File = $file.FullName
                    Line = $i + 1
                    Context = $context
                }
            }
        }
    }
    catch {
        Write-Warning "Failed to read $($file.FullName)"
    }
}

# 2. Check usage
$unused = @()
$totalEnums = $enums.Count
$processed = 0
$bigText = $sb.ToString()

Write-Host "Found $totalEnums enums. Checking usage against codebase..."

foreach ($enum in $enums) {
    $processed++
    if ($processed % 50 -eq 0) { Write-Progress -Activity "Checking usage" -Status "$processed / $totalEnums" -PercentComplete (($processed / $totalEnums) * 100) }
    
    # Regex for whole word match
    $pattern = "\b" + [Regex]::Escape($enum.Name) + "\b"
    
    # Check matches
    $matchCount = [Regex]::Matches($bigText, $pattern).Count
    
    # Logic: 
    # 1 match = likely just the definition (assuming unique names or no collisions)
    # If name is common (e.g. 'Type'), it might have matches clearly unrelated.
    # But usually < 2 means unused.
    if ($matchCount -le 1) {
        $unused += $enum
        # Write-Host "Potential unused: $($enum.Name)"
    }
}

Write-Progress -Activity "Checking usage" -Completed

# 3. Generate Report
$report = "# Unused Enums Audit Report`n`n"
$report += "Generated on $(Get-Date)`n"
$report += "Total Enums Scanned: $totalEnums`n"
$report += "Potential Unused Enums: $($unused.Count)`n`n"
$report += "| Enum Name | Context (Class/Namespace) | File Location |`n"
$report += "|---|---|---|`n"

foreach ($u in $unused) {
    $relPath = $u.File.Substring($root.Length)
    $report += "| **$($u.Name)** | ``$($u.Context)`` | ``.$($relPath):$($u.Line)`` |`n"
}

Set-Content -Path $reportPath -Value $report
Write-Host "Report saved to $reportPath"
