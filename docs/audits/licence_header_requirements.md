# License Header Requirements

**Date**: 2026-01-18
**Source**: AGENTS.md (lines 55-83)
**License**: GNU General Public License v3
**Scope**: All `.cs` files in the XerahS solution

---

## Authoritative License Header

Per AGENTS.md, all C# source files MUST include the following GPL v3 license header:

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

## Placement Rules

### Position
- **MUST** be the **first content** in the file
- Placed **before** any `using` directives
- Placed **before** any `namespace` declarations
- Placed **before** any other `#region` blocks

### Exceptions
The following MAY precede the license header if required:
- Compiler directives like `#nullable enable`, `#define`, or `#pragma warning disable`
- BOM (Byte Order Mark) for UTF-8 files (if present)

### Whitespace
- Blank lines within the header comment block MUST be preserved
- Exactly **one blank line** after `#endregion` is recommended but not strictly required

---

## Compliance Criteria

A file is **COMPLIANT** if:
1. ✅ License header is present
2. ✅ Header text matches the authoritative version **exactly** (character-for-character)
3. ✅ Header is positioned correctly (first non-directive content)
4. ✅ File is encoded as UTF-8 (or ASCII-compatible)

A file is **NON-COMPLIANT** if:
1. ❌ License header is missing entirely
2. ❌ Header text differs from authoritative version (wrong year, wrong project name, etc.)
3. ❌ Header is present but positioned incorrectly (after `using` directives, etc.)
4. ❌ Header uses wrong region name (e.g., `#region License` instead of `#region License Information (GPL v3)`)

---

## Audit Methodology

### Scan Scope
- All `.cs` files in `src/` directory
- All `.cs` files in `tests/` directory
- Exclude: Generated files (if any), third-party code (if explicitly marked)

### Validation Steps
1. Enumerate all `.cs` files in solution
2. For each file:
   a. Read file content
   b. Detect and skip initial compiler directives (if any)
   c. Extract first 23 lines (approximate header length)
   d. Compare extracted text against authoritative header
   e. Record compliance status

### Reporting
Generate a compliance report listing:
- Total files scanned
- Compliant files count
- Non-compliant files count
- Breakdown by violation type (missing, incorrect, misplaced)
- Full list of non-compliant files with specific issues

---

## Fix Strategy

### For Missing Headers
- **Action**: Insert authoritative header at the top of file
- **Preservation**: Keep existing compiler directives above header if present

### For Incorrect Headers
- **Action**: Replace existing header with authoritative version
- **Cases**:
  - Old copyright year (e.g., 2025 → 2026)
  - Wrong project name (e.g., "ShareX.Avalonia" → "XerahS")
  - Truncated or modified text

### For Misplaced Headers
- **Action**: Move header to correct position (before `using` directives)

---

## Version History

### Current Version (2026)
- Project name: **XerahS**
- Description: "The Avalonia UI implementation of ShareX"
- Copyright years: **2007-2026**

### Previous Versions (Historical)
- Project name: "ShareX.Avalonia" (superseded)
- Copyright years: 2007-2025 (superseded)

**Note**: All files MUST use the current 2026 version regardless of original file date.

---

## Enforcement

### Build-Time
- **Current**: No automated build-time enforcement
- **Future**: Consider adding analyzer or pre-commit hook

### Review-Time
- **Manual Review**: This audit represents the first comprehensive review
- **Ongoing**: Future PRs should validate license headers

---

**Last Updated**: 2026-01-18
**Review Phase**: 4 - License Header Compliance Audit
**Status**: ✅ REQUIREMENTS DOCUMENTED

---

*This document is the authoritative reference for license header compliance.*
*All C# files must conform to these requirements.*
