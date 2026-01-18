# License Header Fix Script
# Fixes all 562 non-compliant license headers

$authoritative_header = @'
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

'@

Write-Host "=== License Header Fix Script ===" -ForegroundColor Cyan
Write-Host ""

# Priority 1: Fix INCORRECT headers (430 files)
Write-Host "[1/3] Fixing INCORRECT headers (bulk replacements)..." -ForegroundColor Yellow

$incorrectFiles = Get-Content "incorrect_files.txt"
$fixed_incorrect = 0

foreach ($file in $incorrectFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Fix project name variants
        $content = $content -replace 'ShareX - A program that allows you to take screenshots and share any file type', 'XerahS - The Avalonia UI implementation of ShareX'
        $content = $content -replace 'ShareX\.Avalonia - The Avalonia UI implementation of ShareX', 'XerahS - The Avalonia UI implementation of ShareX'
        $content = $content -replace 'ShareX\.Ava - The Avalonia UI implementation of ShareX', 'XerahS - The Avalonia UI implementation of ShareX'

        # Fix copyright year
        $content = $content -replace 'Copyright \(c\) 2007-2025 ShareX Team', 'Copyright (c) 2007-2026 ShareX Team'

        Set-Content $file -Value $content -NoNewline
        $fixed_incorrect++
    }
}

Write-Host "  Fixed $fixed_incorrect files" -ForegroundColor Green

# Priority 2: Add MISSING headers (130 files)
Write-Host "[2/3] Adding MISSING headers..." -ForegroundColor Yellow

$missingFiles = Get-Content "missing_files.txt"
$fixed_missing = 0

foreach ($file in $missingFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        $newContent = $authoritative_header + $content
        Set-Content $file -Value $newContent -NoNewline
        $fixed_missing++
    }
}

Write-Host "  Fixed $fixed_missing files" -ForegroundColor Green

# Priority 3: Fix MISPLACED headers (2 files)
Write-Host "[3/3] Fixing MISPLACED headers..." -ForegroundColor Yellow

$misplacedFiles = @(
    "src\XerahS.Core\Models\HotkeySettings.cs",
    "src\XerahS.Uploaders\PluginSystem\PluginConfigurationVerifier.cs"
)

$fixed_misplaced = 0

foreach ($file in $misplacedFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Remove #nullable disable from the start
        $content = $content -replace '^#nullable disable\s*\r?\n', ''

        # Fix project name and year in existing header
        $content = $content -replace 'ShareX\.Ava - The Avalonia UI implementation of ShareX', 'XerahS - The Avalonia UI implementation of ShareX'
        $content = $content -replace 'ShareX\.Avalonia - The Avalonia UI implementation of ShareX', 'XerahS - The Avalonia UI implementation of ShareX'
        $content = $content -replace 'Copyright \(c\) 2007-2025 ShareX Team', 'Copyright (c) 2007-2026 ShareX Team'

        # Find the end of the license header and insert #nullable disable after it
        if ($content -match '(#endregion License Information \(GPL v3\)\s*\r?\n)') {
            $content = $content -replace '(#endregion License Information \(GPL v3\)\s*\r?\n)', "`$1#nullable disable`r`n"
        }

        Set-Content $file -Value $content -NoNewline
        $fixed_misplaced++
    }
}

Write-Host "  Fixed $fixed_misplaced files" -ForegroundColor Green

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "  INCORRECT headers fixed: $fixed_incorrect / 430" -ForegroundColor Green
Write-Host "  MISSING headers added:   $fixed_missing / 130" -ForegroundColor Green
Write-Host "  MISPLACED headers fixed: $fixed_misplaced / 2" -ForegroundColor Green
Write-Host "  Total fixes:             $($fixed_incorrect + $fixed_missing + $fixed_misplaced) / 562" -ForegroundColor Green
Write-Host ""
Write-Host "Next: Run 'dotnet build' to verify compilation" -ForegroundColor Cyan
