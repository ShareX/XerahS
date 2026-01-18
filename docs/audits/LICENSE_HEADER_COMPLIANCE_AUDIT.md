# License Header Compliance Audit Report

**Date:** 2026-01-18
**Auditor:** Claude (Automated Scan)
**Scope:** All C# files in `src/` and `tests/` directories
**Branch:** feature/SIP0016-modern-capture

---

## Executive Summary

**CRITICAL COMPLIANCE ISSUE:** Zero files are currently compliant with the required GPL v3 license header format.

### Statistics

- **Total C# Files Scanned:** 562
- **Compliant:** 0 (0%)
- **Non-Compliant:** 562 (100%)
  - Missing Header: 130 files (23.1%)
  - Incorrect Header: 430 files (76.5%)
  - Misplaced Header: 2 files (0.4%)
  - Read Errors: 0 files (0%)

---

## Authoritative License Header

All C# files MUST include this exact header at the top of the file (before any code or using statements):

```csharp
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
```

**Source:** AGENTS.md (project instructions)

---

## Violation Categories

### 1. MISSING License Header (130 files - 23.1%)

These files have no license header at all.

#### Breakdown by Project:

| Project | Missing Files |
|---------|---------------|
| XerahS.UI | 48 |
| XerahS.Common | 16 |
| XerahS.Platform.Linux | 10 |
| XerahS.Platform.Windows | 9 |
| XerahS.Core | 9 |
| XerahS.Platform.Abstractions | 8 |
| XerahS.Platform.MacOS | 7 |
| ShareX.Imgur.Plugin | 2 |
| XerahS.CLI | 1 |
| XerahS.Uploaders | 1 |
| ShareX.AmazonS3.Plugin | 1 |

#### Sample Files:

**Plugin Files:**
- `src/Plugins/ShareX.AmazonS3.Plugin/Views/AmazonS3ConfigView.axaml.cs`
- `src/Plugins/ShareX.Imgur.Plugin/Views/ImgurConfigView.axaml.cs`
- `src/Plugins/ShareX.Imgur.Plugin/ViewModels/ImgurConfigViewModel.cs`

**CLI Files:**
- `src/XerahS.CLI/Commands/BackupSettingsCommand.cs`

**Bootstrap Files:**
- `src/XerahS.Bootstrap/BootstrapContext.cs`
- `src/XerahS.Bootstrap/PlatformBootstrapper.cs`
- `src/XerahS.Bootstrap/PlatformConfiguration.cs`

**Common Files:**
- `src/XerahS.Common/ColorBgra.cs`
- `src/XerahS.Common/Colors/ColorEventHandler.cs`
- `src/XerahS.Common/Colors/MyColor.cs`
- `src/XerahS.Common/DesktopIconManager.cs`

**Core Files:**
- `src/XerahS.Core/Backend/Capture/BackendCapture.cs`
- `src/XerahS.Core/Backend/Capture/BackendScreenshot.cs`
- `src/XerahS.Core/Backend/Capture/DwmCapture.cs`
- `src/XerahS.Core/Capture/AnnotationManager.cs`

**UI Files:**
- `src/XerahS.UI/Controls/CaptureRegionControl.axaml.cs`
- `src/XerahS.UI/Controls/CrosshairControl.cs`
- `src/XerahS.UI/Controls/InfoOverlayControl.cs`
- `src/XerahS.UI/Controls/MagnifierControl.cs`

**Platform Abstraction Files:**
- `src/XerahS.Platform.Abstractions/Capture/BackendCapabilities.cs`
- `src/XerahS.Platform.Abstractions/Capture/CapturedBitmap.cs`
- `src/XerahS.Platform.Abstractions/Capture/IRegionCaptureBackend.cs`
- `src/XerahS.Platform.Abstractions/IScreenCaptureService.cs`

**Platform Implementation Files:**
- `src/XerahS.Platform.Linux/Capture/LinuxRegionCaptureBackend.cs`
- `src/XerahS.Platform.MacOS/Capture/MacOSRegionCaptureBackend.cs`
- `src/XerahS.Platform.Windows/Capture/WindowsRegionCaptureBackend.cs`

---

### 2. INCORRECT License Header (430 files - 76.5%)

These files have a license header, but it doesn't match the required format.

#### Breakdown by Project:

| Project | Incorrect Files |
|---------|-----------------|
| XerahS.Uploaders | 132 |
| XerahS.Common | 124 |
| XerahS.Platform.Abstractions | 20 |
| XerahS.UI | 19 |
| XerahS.Platform.Windows | 18 |
| XerahS.Core | 17 |
| XerahS.Media | 13 |
| XerahS.Platform.Linux | 13 |
| XerahS.Platform.MacOS | 13 |
| XerahS.CLI | 10 |
| XerahS.History | 10 |
| XerahS.Indexer | 9 |
| ShareX.Imgur.Plugin | 5 |
| ShareX.AmazonS3.Plugin | 4 |
| XerahS.Bootstrap | 3 |
| XerahS.Services | 3 |
| XerahS.ViewModels | 1 |
| XerahS.PluginExporter | 1 |

#### Common Issues:

**Issue Type 1: Wrong Year + Wrong Project Name (Most Common)**
- Uses "2007-2025" instead of "2007-2026"
- Uses "ShareX - A program that allows you to take screenshots..." instead of "XerahS - The Avalonia UI implementation of ShareX"

**Example (from ShareX.AmazonS3.Plugin/AmazonS3Provider.cs):**
```csharp
#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type  // WRONG
    Copyright (c) 2007-2025 ShareX Team                                             // WRONG: 2025 → 2026

    [rest of GPL v3 text...]
*/

#endregion License Information (GPL v3)
```

**Issue Type 2: Wrong Project Name Variant**
- Uses "ShareX.Avalonia - The Avalonia UI implementation of ShareX" instead of "XerahS - The Avalonia UI implementation of ShareX"
- Uses "2007-2025" instead of "2007-2026"

**Example (from XerahS.Common/AppResources.cs):**
```csharp
#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX     // WRONG: ShareX.Ava → XerahS
    Copyright (c) 2007-2025 ShareX Team                       // WRONG: 2025 → 2026

    [rest of GPL v3 text...]
*/

#endregion License Information (GPL v3)
```

**Sample Affected Files:**
- `src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Provider.cs` (ShareX + 2025)
- `src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Uploader.cs` (ShareX + 2025)
- `src/Plugins/ShareX.Imgur.Plugin/ImgurAlbumData.cs` (ShareX + 2025)
- `src/XerahS.Common/AppResources.cs` (ShareX.Ava + 2025)
- `src/XerahS.Common/CLI/CLIManager.cs` (ShareX.Avalonia + 2025)
- `src/XerahS.Uploaders/*` (all files - ShareX.Avalonia + 2025)
- `src/XerahS.Core/*` (most files - ShareX.Avalonia + 2025)

**Projects with Systematic Issues:**
- **XerahS.Uploaders**: All 132 files use "ShareX.Avalonia" + 2025
- **XerahS.Common**: 124 files use various wrong project names + 2025
- **XerahS.Platform.Abstractions**: All 20 files use wrong format

---

### 3. MISPLACED License Header (2 files - 0.4%)

These files have the license header, but code appears before it.

**Issue:** `#nullable disable` directive appears BEFORE the license header

**Affected Files:**

1. **`src/XerahS.Core/Models/HotkeySettings.cs`**
   - Current header uses "ShareX.Ava" and "2025"
   - `#nullable disable` must be moved AFTER the license header

2. **`src/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`**
   - Current header uses "ShareX.Avalonia" and "2025"
   - `#nullable disable` must be moved AFTER the license header

**Current Structure (WRONG):**
```csharp
#nullable disable                    // WRONG: This should be AFTER the header
#region License Information (GPL v3)
/*
    ShareX.Ava - ...                 // Also needs correction
    Copyright (c) 2007-2025...       // Also needs correction
*/
#endregion License Information (GPL v3)
```

**Correct Structure:**
```csharp
#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    ...
*/
#endregion License Information (GPL v3)

#nullable disable                    // CORRECT: After the header
```

---

## Recommendations

### Immediate Actions Required

#### 1. Fix INCORRECT Headers (430 files - Highest Priority)

This is the largest category and can be fixed systematically.

**Find/Replace Operations:**

**Operation 1: Fix Project Name Variants**
```
Find:     ShareX.Avalonia - The Avalonia UI implementation of ShareX
Replace:  XerahS - The Avalonia UI implementation of ShareX

Find:     ShareX.Ava - The Avalonia UI implementation of ShareX
Replace:  XerahS - The Avalonia UI implementation of ShareX

Find:     ShareX - A program that allows you to take screenshots and share any file type
Replace:  XerahS - The Avalonia UI implementation of ShareX
```

**Operation 2: Fix Year**
```
Find:     Copyright (c) 2007-2025 ShareX Team
Replace:  Copyright (c) 2007-2026 ShareX Team
```

**Estimated Time:** 15-30 minutes with bulk find/replace

---

#### 2. Add MISSING Headers (130 files)

Each file needs the complete header inserted at the top.

**Recommended Approach:**
- Use automated script (PowerShell/Python)
- Or use IDE snippet/template for batch insertion

**Sample Script Logic:**
```powershell
foreach ($file in $missingFiles) {
    $content = Get-Content $file -Raw
    $headerContent = $authoritative_header + "`n" + $content
    Set-Content $file -Value $headerContent
}
```

**Estimated Time:** 1-2 hours (script development + testing + execution)

---

#### 3. Fix MISPLACED Headers (2 files)

**Manual fix required:**

For each file:
1. Remove `#nullable disable` from line 1
2. Ensure license header is at line 1
3. Insert blank line after header
4. Add `#nullable disable` after the blank line
5. Fix project name and year in header

**Estimated Time:** 5-10 minutes

---

### Implementation Approach

**Recommended Strategy: Automated Script**

Create a comprehensive fix script:

```powershell
# 1. Fix INCORRECT headers (bulk find/replace)
# 2. Add MISSING headers (prepend authoritative header)
# 3. Fix MISPLACED headers (special handling)
# 4. Verify all files after changes
```

**Execution Steps:**
1. Create backup branch
2. Run automated fix script
3. Build solution (`dotnet build`) to verify no syntax errors
4. Re-run audit to verify 100% compliance
5. Review sample of changes manually
6. Commit with message: `[Compliance] Fix GPL v3 license headers across all C# files`

---

### Post-Fix Verification

After fixing all violations:

1. **Re-run Audit:**
   ```bash
   powershell -ExecutionPolicy Bypass -File audit_license.ps1
   ```
   Expected result: 562 compliant, 0 non-compliant

2. **Build Verification:**
   ```bash
   dotnet build
   ```
   Expected: 0 errors, 0 warnings

3. **Spot Check:**
   Manually verify 10-20 random files across different projects

---

### Prevention Measures

To prevent future violations:

1. **Pre-commit Hook:**
   - Add git pre-commit hook to check license headers
   - Reject commits with missing/incorrect headers

2. **CI/CD Pipeline:**
   - Add license compliance check to CI/CD pipeline
   - Fail builds on non-compliant files

3. **IDE Templates:**
   - Configure IDE file templates to include correct header
   - Visual Studio: File Header template
   - VS Code: File header extension/snippet

4. **Documentation:**
   - Keep authoritative header in AGENTS.md
   - Reference in contributing guidelines

---

## Detailed File Lists

Complete file lists are available in the repository root:

- `all_noncompliant_files.txt` - All 562 non-compliant files
- `missing_files.txt` - 130 files without headers
- `incorrect_files.txt` - 430 files with incorrect headers
- `misplaced_files.txt` - 2 files with misplaced headers
- `license_audit_report.txt` - Full detailed report (697 KB)

---

## Audit Methodology

**Tools Used:**
- PowerShell script: `audit_license.ps1`
- String comparison: Exact match against authoritative header
- Exclusions: `obj/`, `bin/` directories (auto-generated files)

**Detection Logic:**
1. **MISSING:** No `#region License Information (GPL v3)` found
2. **INCORRECT:** Header found but text doesn't match exactly
3. **MISPLACED:** Code exists before header (e.g., `#nullable disable`)
4. **COMPLIANT:** Header matches exactly and is positioned correctly

**Verification:**
- Manual spot-checks confirmed accuracy
- Sample files inspected across all violation categories

---

## Testing Coverage

**Test Files Audited:** 1 file
- `tests/ShareX.Avalonia.Tests/Services/CoordinateTransformTests.cs`

**Status:** Included in incorrect files count (needs fixing)

---

## Appendix A: Project Statistics Summary

### Projects with Highest Non-Compliance

| Project | Missing | Incorrect | Misplaced | Total Non-Compliant |
|---------|---------|-----------|-----------|---------------------|
| XerahS.Uploaders | 1 | 132 | 1 | 134 |
| XerahS.Common | 16 | 124 | 0 | 140 |
| XerahS.UI | 48 | 19 | 0 | 67 |
| XerahS.Platform.Abstractions | 8 | 20 | 0 | 28 |
| XerahS.Core | 9 | 17 | 1 | 27 |
| XerahS.Platform.Linux | 10 | 13 | 0 | 23 |
| XerahS.Platform.MacOS | 7 | 13 | 0 | 20 |
| XerahS.Platform.Windows | 9 | 18 | 0 | 27 |

### Projects Requiring Most Attention

1. **XerahS.Uploaders (134 files)**: 99% incorrect headers, systematic issue
2. **XerahS.Common (140 files)**: Mixed violations, needs comprehensive fix
3. **XerahS.UI (67 files)**: 71% missing headers, mostly new files

---

## Appendix B: Sample Corrections

### Example 1: Fix INCORRECT Header

**Before (Plugins/ShareX.AmazonS3.Plugin/AmazonS3Provider.cs):**
```csharp
#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2025 ShareX Team
    ...
*/

#endregion License Information (GPL v3)
```

**After:**
```csharp
#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    ...
*/

#endregion License Information (GPL v3)
```

---

### Example 2: Add MISSING Header

**Before (Plugins/ShareX.Imgur.Plugin/Views/ImgurConfigView.axaml.cs):**
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Imgur.Plugin.Views;
```

**After:**
```csharp
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

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Imgur.Plugin.Views;
```

---

### Example 3: Fix MISPLACED Header

**Before (XerahS.Core/Models/HotkeySettings.cs):**
```csharp
#nullable disable
#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team
    ...
*/

#endregion License Information (GPL v3)
```

**After:**
```csharp
#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    ...
*/

#endregion License Information (GPL v3)

#nullable disable
```

---

## Report Metadata

- **Generated:** 2026-01-18
- **Tool:** PowerShell Audit Script (`audit_license.ps1`)
- **Runtime:** ~30 seconds
- **Files Scanned:** 562
- **Excluded Paths:** `obj/`, `bin/`
- **Report Size:** 697 KB (full report)

---

**End of Report**
