# License Header Compliance Audit Report

**Date:** 2026-01-18
**Auditor:** Claude (Automated Scan)
**Scope:** All C# files in `src/` and `tests/` directories

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

---

## Violation Categories

### 1. MISSING License Header (130 files)

These files have no license header at all. Examples include:

**Plugin Files:**
- `src/Plugins/ShareX.AmazonS3.Plugin/Views/AmazonS3ConfigView.axaml.cs`
- `src/Plugins/ShareX.AmazonS3.Plugin/ViewModels/AmazonS3ConfigViewModel.cs`
- `src/Plugins/ShareX.Imgur.Plugin/Views/ImgurConfigView.axaml.cs`
- `src/Plugins/ShareX.Imgur.Plugin/ViewModels/ImgurConfigViewModel.cs`

**CLI Files:**
- `src/XerahS.CLI/Commands/BackupSettingsCommand.cs`
- `src/XerahS.CLI/Commands/CaptureCommand.cs`
- `src/XerahS.CLI/Commands/HistoryCommand.cs`
- `src/XerahS.CLI/Commands/PluginCommand.cs`
- `src/XerahS.CLI/Commands/UploadCommand.cs`
- `src/XerahS.CLI/Commands/WorkflowCommand.cs`
- `src/XerahS.CLI/Program.cs`

**Bootstrap Files:**
- `src/XerahS.Bootstrap/BootstrapContext.cs`
- `src/XerahS.Bootstrap/PlatformBootstrapper.cs`
- `src/XerahS.Bootstrap/PlatformConfiguration.cs`

**Core Files:**
- `src/XerahS.Core/Backend/Capture/BackendCapture.cs`
- `src/XerahS.Core/Backend/Capture/BackendScreenshot.cs`
- `src/XerahS.Core/Backend/Capture/DwmCapture.cs`
- `src/XerahS.Core/Backend/Capture/ScreenshotOptions.cs`
- `src/XerahS.Core/Backend/Capture/Enums/ScreenshotMethod.cs`
- `src/XerahS.Core/Capture/AnnotationManager.cs`
- `src/XerahS.Core/Capture/CaptureHelpers.cs`

**UI Files:**
- `src/XerahS.UI/Controls/CaptureRegionControl.axaml.cs`
- `src/XerahS.UI/Controls/CrosshairControl.cs`
- `src/XerahS.UI/Controls/InfoOverlayControl.cs`
- `src/XerahS.UI/Controls/MagnifierControl.cs`

And approximately 100+ more files across various projects.

---

### 2. INCORRECT License Header (430 files)

These files have a license header, but it doesn't match the required format. Common issues:

**Most Common Issues:**
1. **Wrong Year:** Uses "2007-2025" instead of "2007-2026"
2. **Wrong Project Name:** Uses "ShareX - A program..." or "XerahS" instead of "XerahS - The Avalonia UI implementation of ShareX"

**Examples:**

**Files with "ShareX - A program that allows you to take screenshots...":**
- `src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Provider.cs`
- `src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Uploader.cs`
- `src/Plugins/ShareX.AmazonS3.Plugin/S3ConfigModel.cs`

**Files with "XerahS - The Avalonia UI implementation...":**
- Most files in `XerahS.Common`
- Most files in `XerahS.Core`
- Most files in `XerahS.Uploaders`
- Most files in `XerahS.ViewModels`
- Most files in `XerahS.UI`
- Most files in `XerahS.Services`

**Files with "ShareX.Ava - The Avalonia UI implementation...":**
- `src/XerahS.Core/Models/HotkeySettings.cs`

This category represents the largest compliance issue - 76.5% of all files.

---

### 3. MISPLACED License Header (2 files)

These files have the license header, but code appears before it:

1. **`src/XerahS.Core/Models/HotkeySettings.cs`**
   - Issue: `#nullable disable` appears before the license header
   - Current header uses "ShareX.Ava" and "2025"

2. **`src/XerahS.Uploaders/PluginSystem/PluginConfigurationVerifier.cs`**
   - Issue: `#nullable disable` appears before the license header
   - Current header uses "XerahS" and "2025"

**Note:** The `#nullable disable` directive should be moved AFTER the license header.

---

## Detailed File Lists

### Missing Header Files by Project

#### XerahS.CLI (7 files)
- Commands/BackupSettingsCommand.cs
- Commands/CaptureCommand.cs
- Commands/HistoryCommand.cs
- Commands/PluginCommand.cs
- Commands/UploadCommand.cs
- Commands/WorkflowCommand.cs
- Program.cs

#### XerahS.Bootstrap (3 files)
- BootstrapContext.cs
- PlatformBootstrapper.cs
- PlatformConfiguration.cs

#### Plugins (4 files)
- ShareX.AmazonS3.Plugin/ViewModels/AmazonS3ConfigViewModel.cs
- ShareX.AmazonS3.Plugin/Views/AmazonS3ConfigView.axaml.cs
- ShareX.Imgur.Plugin/ViewModels/ImgurConfigViewModel.cs
- ShareX.Imgur.Plugin/Views/ImgurConfigView.axaml.cs

#### XerahS.Core (40+ files)
- Backend/Capture/*.cs (multiple files)
- Capture/*.cs
- Hotkeys/*.cs
- Models/*.cs
- Settings/*.cs
- UI/*.cs
- Utilities/*.cs
- Workflows/*.cs

#### XerahS.UI (30+ files)
- Controls/*.cs
- Converters/*.cs
- Views/*.cs

#### XerahS.Services (10+ files)
- Various service implementation files

#### Other Projects
- Additional files across Platform.Windows, Platform.MacOS, Platform.Linux, Media, History, etc.

---

## Incorrect Header Files by Issue Type

### Issue: "Uses 2025 instead of 2026; First line doesn't use 'XerahS' project name"
This is the most common pattern, affecting the majority of the 430 incorrect files:

**All Projects Affected:**
- XerahS.Common (majority of files)
- XerahS.Core (majority of files)
- XerahS.Uploaders (all files)
- XerahS.ViewModels (all files)
- XerahS.UI (majority of files)
- XerahS.Services (all files)
- XerahS.Platform.Windows (all files)
- XerahS.Platform.MacOS (all files)
- XerahS.Platform.Linux (all files)
- XerahS.Platform.Abstractions (all files)
- XerahS.Media (all files)
- XerahS.History (all files)
- XerahS.Indexer (all files)
- XerahS.PluginExporter (all files)
- Plugins (most non-MISSING files)

**Typical Header Format Found:**
```csharp
#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX  // WRONG: Should be "XerahS -"
    Copyright (c) 2007-2025 ShareX Team                         // WRONG: Should be 2026

    [rest of GPL v3 text...]
*/

#endregion License Information (GPL v3)
```

---

## Recommendations

### Immediate Actions Required

1. **Bulk Update for Incorrect Headers (430 files)**
   - Replace "XerahS - The Avalonia UI implementation of ShareX" with "XerahS - The Avalonia UI implementation of ShareX"
   - Replace "ShareX - A program that allows you to take screenshots and share any file type" with "XerahS - The Avalonia UI implementation of ShareX"
   - Replace "ShareX.Ava - The Avalonia UI implementation of ShareX" with "XerahS - The Avalonia UI implementation of ShareX"
   - Replace "2007-2025" with "2007-2026"

2. **Add Headers for Missing Files (130 files)**
   - Insert the complete authoritative header at the top of each file
   - Ensure header appears BEFORE any code, using statements, or directives

3. **Fix Misplaced Headers (2 files)**
   - Move `#nullable disable` directive to AFTER the license header
   - Update the header text to match authoritative version

### Suggested Implementation Approach

**Option 1: Automated Script**
- Create a PowerShell/Python script to:
  - Add missing headers
  - Replace incorrect headers with correct version
  - Fix misplaced headers

**Option 2: IDE Find/Replace**
- Use multi-file find/replace for common patterns:
  - Find: `Copyright (c) 2007-2025 ShareX Team`
  - Replace: `Copyright (c) 2007-2026 ShareX Team`

  - Find: `XerahS - The Avalonia UI implementation of ShareX`
  - Replace: `XerahS - The Avalonia UI implementation of ShareX`

  - Find: `ShareX - A program that allows you to take screenshots and share any file type`
  - Replace: `XerahS - The Avalonia UI implementation of ShareX`

**Option 3: Manual Review**
- For critical/complex files, review each change manually

### Post-Update Verification

After making corrections:
1. Re-run this audit script to verify 100% compliance
2. Add pre-commit hook to enforce license headers on new files
3. Update CI/CD pipeline to check license compliance

---

## Testing Coverage

**Test Files Audited:** 1 file
- `tests/XerahS.Tests/Services/CoordinateTransformTests.cs`

**Status:** This file requires audit inclusion in the detailed report (not shown in preview sections above).

---

## Appendix: Full Non-Compliant File List

The complete list of 562 non-compliant files is available in the detailed audit report:
- File: `license_audit_report.txt`
- Location: Repository root directory
- Size: ~697 KB

To view specific violations:
```bash
# View MISSING files
grep -A 5 "=== MISSING LICENSE HEADER" license_audit_report.txt | head -100

# View INCORRECT files
grep -A 5 "=== INCORRECT LICENSE HEADER" license_audit_report.txt | head -100

# View MISPLACED files
grep -A 5 "=== MISPLACED LICENSE HEADER" license_audit_report.txt
```

---

## Notes

- This audit was performed on all `.cs` files in `src/` and `tests/` directories
- Generated/compiler-created files in `obj/` and `bin/` were excluded
- The audit compares against the authoritative header defined in `AGENTS.md`
- Exact text matching is required - even minor whitespace differences will trigger INCORRECT status

---

**Report Generated:** 2026-01-18
**Tool:** PowerShell Audit Script (`audit_license.ps1`)
**Command:** `powershell -ExecutionPolicy Bypass -File audit_license.ps1`
