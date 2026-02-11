# License Header Compliance Audit Script
# Checks all C# files for correct GPL v3 license header

$expectedHeader = @"
#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
"@

function Test-LicenseHeader {
    param([string]$filePath)

    try {
        $content = Get-Content $filePath -Raw -ErrorAction Stop
        $lines = Get-Content $filePath -ErrorAction Stop

        # Get first 30 lines
        $first30 = ($lines | Select-Object -First 30) -join "`n"

        # Check if license header exists
        if ($content -notmatch '#region License Information \(GPL v3\)') {
            return @{
                Status = "MISSING"
                Issue = "No license header found"
                Preview = $first30
            }
        }

        # Find header start line
        $headerStartLine = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '#region License Information \(GPL v3\)') {
                $headerStartLine = $i
                break
            }
        }

        # Check if there's code before header
        if ($headerStartLine -gt 0) {
            for ($i = 0; $i -lt $headerStartLine; $i++) {
                $line = $lines[$i].Trim()
                if ($line -and -not $line.StartsWith('//')) {
                    return @{
                        Status = "MISPLACED"
                        Issue = "Code found before license header at line $($i+1): $line"
                        Preview = $first30
                    }
                }
            }
        }

        # Find header end line
        $headerEndLine = -1
        for ($i = $headerStartLine + 1; $i -lt [Math]::Min($lines.Count, $headerStartLine + 30); $i++) {
            if ($lines[$i] -match '#endregion License Information \(GPL v3\)') {
                $headerEndLine = $i
                break
            }
        }

        if ($headerEndLine -eq -1) {
            return @{
                Status = "INCORRECT"
                Issue = "License header region not properly closed"
                Preview = $first30
            }
        }

        # Extract actual header
        $actualHeader = ($lines[$headerStartLine..$headerEndLine] -join "`n").TrimEnd()

        # Compare
        if ($actualHeader -eq $expectedHeader) {
            return @{
                Status = "COMPLIANT"
                Issue = ""
                Preview = $first30
            }
        }
        else {
            $issues = @()
            if ($actualHeader -match '2025' -and $actualHeader -notmatch '2026') {
                $issues += "Uses 2025 instead of 2026"
            }
            if ($actualHeader -match 'ShareX\.Avalonia') {
                $issues += "Uses 'ShareX.Avalonia' instead of 'XerahS'"
            }
            if ($actualHeader -match 'ShareX -' -and $actualHeader -notmatch 'XerahS -') {
                $issues += "First line doesn't use 'XerahS' project name"
            }
            if ($issues.Count -eq 0) {
                $issues += "Header text doesn't match exactly"
            }

            return @{
                Status = "INCORRECT"
                Issue = ($issues -join "; ")
                Preview = $first30
            }
        }
    }
    catch {
        return @{
            Status = "ERROR"
            Issue = "Cannot read file: $_"
            Preview = ""
        }
    }
}

# Read file list
$listFile = "cs_files_list.txt"
$files = Get-Content $listFile | Where-Object { $_.Trim() }

# Track statistics
$compliant = @()
$missing = @()
$incorrect = @()
$misplaced = @()
$errors = @()

# Check each file
foreach ($file in $files) {
    $result = Test-LicenseHeader $file

    $fileInfo = @{
        Path = $file
        Issue = $result.Issue
        Preview = $result.Preview
    }

    switch ($result.Status) {
        "COMPLIANT" { $compliant += $fileInfo }
        "MISSING" { $missing += $fileInfo }
        "INCORRECT" { $incorrect += $fileInfo }
        "MISPLACED" { $misplaced += $fileInfo }
        "ERROR" { $errors += $fileInfo }
    }
}

# Output results
Write-Host "=== LICENSE HEADER COMPLIANCE AUDIT ==="
Write-Host ""
Write-Host "Total C# files scanned: $($files.Count)"
Write-Host "Compliant: $($compliant.Count)"
Write-Host "Non-compliant: $($files.Count - $compliant.Count)"
Write-Host "  - Missing: $($missing.Count)"
Write-Host "  - Incorrect: $($incorrect.Count)"
Write-Host "  - Misplaced: $($misplaced.Count)"
Write-Host "  - Errors: $($errors.Count)"

if ($missing.Count -gt 0) {
    Write-Host "`n`n=== MISSING LICENSE HEADER ($($missing.Count) files) ==="
    foreach ($f in $missing) {
        Write-Host "`n$($f.Path)"
        Write-Host "Issue: $($f.Issue)"
        Write-Host "First 30 lines:"
        Write-Host $f.Preview
        Write-Host ("-" * 80)
    }
}

if ($incorrect.Count -gt 0) {
    Write-Host "`n`n=== INCORRECT LICENSE HEADER ($($incorrect.Count) files) ==="
    foreach ($f in $incorrect) {
        Write-Host "`n$($f.Path)"
        Write-Host "Issue: $($f.Issue)"
        Write-Host "First 30 lines:"
        Write-Host $f.Preview
        Write-Host ("-" * 80)
    }
}

if ($misplaced.Count -gt 0) {
    Write-Host "`n`n=== MISPLACED LICENSE HEADER ($($misplaced.Count) files) ==="
    foreach ($f in $misplaced) {
        Write-Host "`n$($f.Path)"
        Write-Host "Issue: $($f.Issue)"
        Write-Host "First 30 lines:"
        Write-Host $f.Preview
        Write-Host ("-" * 80)
    }
}

if ($errors.Count -gt 0) {
    Write-Host "`n`n=== ERRORS ($($errors.Count) files) ==="
    foreach ($f in $errors) {
        Write-Host "`n$($f.Path)"
        Write-Host "Issue: $($f.Issue)"
        Write-Host ("-" * 80)
    }
}
