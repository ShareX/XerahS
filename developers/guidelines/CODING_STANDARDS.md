# Coding Standards & Best Practices

## License Headers
All source files must include the **full GPL v3 license text** in the appropriate comment style: **C#** (`#region` + block), **Swift** (line comments), **Kotlin** (block comment). The pre-commit hook validates C#, Swift, and Kotlin.

### C# (`.cs`)
All `.cs` files must include the GPL v3 license header.

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

### Swift (`.swift`)
All `.swift` files (e.g. in `src/mobile/ios`) must include the full GPL v3 text as line comments.

```swift
//
//  FileName.swift
//  XerahS Mobile (Swift)
//
//  XerahS - The Avalonia UI implementation of ShareX
//  Copyright (c) 2007-2026 ShareX Team
//
//  This program is free software; you can redistribute it and/or
//  modify it under the terms of the GNU General Public License
//  as published by the Free Software Foundation; either version 2
//  of the License, or (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
//
//  Optionally you can also view the license at <http://www.gnu.org/licenses/>.
//
```

### Kotlin (`.kt`)
All `.kt` files (e.g. in `src/mobile/android`) must include the full GPL v3 text as a block comment at the top of the file, before the `package` declaration.

```kotlin
/*
 * XerahS - The Avalonia UI implementation of ShareX
 * Copyright (c) 2007-2026 ShareX Team
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 *
 * Optionally you can also view the license at <http://www.gnu.org/licenses/>.
 */

package com.getsharex.xerahs.mobile...
```

## Nullability Best Practices
This project uses strict nullable reference types (`<Nullable>enable</Nullable>`). **All new code MUST be null-safe.**

- **Always handle nullable returns**: Use null-conditional (`?.`), null-coalescing (`??`), or explicit null checks.
- **Nullable dereference errors (CS8602)**: Never dereference a possibly-null reference without a guard.
- **Null argument errors (CS8604)**: Validate arguments before passing to non-nullable parameters.
- **Null assignment errors (CS8601)**: Use default values or `null!` only when the value is guaranteed to be non-null.
- **Collection null properties**: Some Avalonia/framework properties (e.g., `PathFigure.Segments`) can be `null`. Use `??=` before accessing.
- **Late-initialized fields**: Use `null!` assertion only for fields guaranteed to be initialized before use (e.g., in constructor).

**Example patterns:**
```csharp
// Safe access with null-conditional + null-coalescing
var result = settings?.CaptureSettings?.UseModernCapture ?? false;

// Null-coalescing assignment for collection properties
outerFigure.Segments ??= new PathSegments();
outerFigure.Segments.Add(...);

// Non-nullable parameter guard
if (string.IsNullOrEmpty(workflowId)) return;
```

## General Guidelines
- **Patterns**: Follow existing patterns in each project area.
- **Comments**: Add small comments only when necessary to explain non-obvious logic.
- **Minimalism**: Keep changes minimal and targeted.
- **Encoding**: Preserve intended Unicode characters in user-facing strings; prefer C# Unicode escape sequences (e.g., `\u26A0\uFE0F`, `\u2713`) to avoid mojibake, and keep files UTF-8.
