# License Header Violations - Quick Reference

**Date:** 2026-01-18
**Total Non-Compliant:** 562 files (100%)

---

## Summary by Violation Type

| Violation | Count | % of Total |
|-----------|-------|------------|
| INCORRECT | 430 | 76.5% |
| MISSING | 130 | 23.1% |
| MISPLACED | 2 | 0.4% |
| **TOTAL** | **562** | **100%** |

---

## Fix Priority Order

### Priority 1: INCORRECT Headers (430 files)
**Why First:** Can be fixed with bulk find/replace operations.

**Common Patterns:**
- `ShareX - A program that...` → `XerahS - The Avalonia UI implementation...`
- `XerahS - The Avalonia...` → `XerahS - The Avalonia...`
- `ShareX.Ava - The Avalonia...` → `XerahS - The Avalonia...`
- `2007-2025` → `2007-2026`

**Top 5 Projects:**
1. XerahS.Uploaders: 132 files
2. XerahS.Common: 124 files
3. XerahS.Platform.Abstractions: 20 files
4. XerahS.UI: 19 files
5. XerahS.Platform.Windows: 18 files

---

### Priority 2: MISSING Headers (130 files)
**Why Second:** Requires inserting full header, needs script or manual work.

**Top 5 Projects:**
1. XerahS.UI: 48 files
2. XerahS.Common: 16 files
3. XerahS.Platform.Linux: 10 files
4. XerahS.Platform.Windows: 9 files
5. XerahS.Core: 9 files

---

### Priority 3: MISPLACED Headers (2 files)
**Why Last:** Only 2 files, requires manual fix.

**Files:**
1. `src/XerahS.Core/Models/HotkeySettings.cs`
2. `src/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`

**Fix:** Move `#nullable disable` from before header to after header.

---

## Quick Fix Commands

### Fix INCORRECT Headers (Find/Replace)

**Visual Studio / VS Code Multi-File Search:**

**Step 1 - Fix Project Name:**
```
Find:    ShareX - A program that allows you to take screenshots and share any file type
Replace: XerahS - The Avalonia UI implementation of ShareX
Scope:   Entire Solution
```

```
Find:    XerahS - The Avalonia UI implementation of ShareX
Replace: XerahS - The Avalonia UI implementation of ShareX
Scope:   Entire Solution
```

```
Find:    ShareX.Ava - The Avalonia UI implementation of ShareX
Replace: XerahS - The Avalonia UI implementation of ShareX
Scope:   Entire Solution
```

**Step 2 - Fix Year:**
```
Find:    Copyright (c) 2007-2025 ShareX Team
Replace: Copyright (c) 2007-2026 ShareX Team
Scope:   Entire Solution
```

---

### Add MISSING Headers (PowerShell Script)

Create and run: `fix_missing_headers.ps1`

```powershell
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

$missingFiles = Get-Content "missing_files.txt"
foreach ($file in $missingFiles) {
    $content = Get-Content $file -Raw
    $newContent = $authoritative_header + $content
    Set-Content $file -Value $newContent -NoNewline
    Write-Host "Fixed: $file"
}
```

---

### Fix MISPLACED Headers (Manual)

**File 1:** `src/XerahS.Core/Models/HotkeySettings.cs`
**File 2:** `src/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`

**Steps:**
1. Open file
2. Cut `#nullable disable` from line 1
3. Fix header (update project name to "XerahS", year to "2026")
4. Paste `#nullable disable` after header (after `#endregion`)
5. Save

---

## Verification After Fixes

**Step 1: Re-run Audit**
```bash
cd "c:\Users\liveu\source\repos\ShareX Team\XerahS"
powershell -ExecutionPolicy Bypass -File audit_license.ps1
```

**Expected Output:**
```
Total C# files scanned: 562
Compliant: 562
Non-compliant: 0
```

**Step 2: Build Verification**
```bash
dotnet build
```

**Expected:** 0 errors, 0 warnings

**Step 3: Spot Check**
Manually open and verify 5-10 random files.

---

## File Lists

**Complete lists available in repo root:**

- `missing_files.txt` - 130 files without headers
- `incorrect_files.txt` - 430 files with incorrect headers
- `misplaced_files.txt` - 2 files with misplaced headers
- `all_noncompliant_files.txt` - All 562 non-compliant files
- `license_audit_report.txt` - Full detailed report with previews

---

## Common Mistakes to Avoid

1. **Don't skip blank line:** Header should be followed by a blank line before `using` statements
2. **Don't modify GPL text:** Only the first two lines change (project name, copyright year)
3. **Don't place after using statements:** Header MUST be first thing in file
4. **Don't forget #nullable disable fix:** Must move AFTER header, not delete
5. **Don't forget region tags:** Both `#region` and `#endregion` are required

---

## Project-Specific Notes

### XerahS.Uploaders (134 violations)
- All files use "XerahS" → Systematic bulk fix needed
- All files use "2025" → Systematic bulk fix needed
- 1 file has misplaced header (PluginConfigurationVerifier.cs)

### XerahS.Common (140 violations)
- Mix of "ShareX.Ava" and "XerahS"
- 16 files completely missing headers
- Focus area: Colors/, Native/, UITypeEditors/ subdirectories

### XerahS.UI (67 violations)
- Mostly missing headers (48 files)
- Likely newer files that were created without template
- Focus area: Controls/, Converters/, Views/ subdirectories

### Plugins (12 violations)
- ShareX.Imgur.Plugin: 7 files (2 missing, 5 incorrect)
- ShareX.AmazonS3.Plugin: 5 files (1 missing, 4 incorrect)
- All incorrect files use "ShareX - A program that..." header

---

**Last Updated:** 2026-01-18
**Next Action:** Execute Priority 1 fixes (bulk find/replace for INCORRECT headers)
