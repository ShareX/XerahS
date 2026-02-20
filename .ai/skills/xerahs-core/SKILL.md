---
name: ShareX Core Standards
description: License headers, build configuration rules, SkiaSharp version constraints, and general coding standards for XerahS
---

## License Header Requirement

When creating or editing C# files (`.cs`) in this repository, include the following license header at the top of the file (tailored for the Avalonia implementation):

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

## Build Configuration Rules

### Windows Target Framework Moniker (TFM)

**CRITICAL**: When configuring projects to target Windows, use the **explicit TFM** format that includes the platform version:
`net10.0-windows10.0.19041.0`

**Do NOT use**:
`<TargetFramework>net10.0-windows</TargetFramework>` with a separate `<TargetPlatformVersion>...` property.
This is required to avoid "Windows Metadata not provided" errors during full solution builds with CsWinRT.

### SkiaSharp Version Constraint

**IMPORTANT (Until Avalonia 12 is released):** All `.csproj` files in this repository MUST use **SkiaSharp version 2.88.9**. Do NOT upgrade to SkiaSharp 3.x as it is incompatible with Avalonia 11.x.

- When adding SkiaSharp to a new project: `<PackageReference Include="SkiaSharp" Version="2.88.9" />`
- If you encounter version conflicts, always downgrade to 2.88.9.
- This constraint applies to all projects including `XerahS.*` and `ShareX.Editor`.

## General Coding Rules

### Code and Config Changes

- Follow existing patterns in each project area.
- Keep changes minimal and targeted.
- Add small comments only when necessary to explain non-obvious logic.
- **CRITICAL:** After modifying code, always run `dotnet build` and fix all build errors before finishing.
- For Git workflow, commit format, and version bump behavior, follow `.github/skills/xerahs-workflow/SKILL.md`.
- **Ensure you can compile, and if not, fix the issues.** This is a mandatory check before finishing any coding task.

### Change Safety

- Do not remove or rewrite unrelated content.
- Apply version changes only via the workflow defined in `.github/skills/xerahs-workflow/SKILL.md`.
- Flag assumptions clearly when requirements are ambiguous.

### Testing

- If you modify executable code, suggest relevant tests.
- If tests are added, align them with current test conventions.

### Documentation

- Update or add docs when behavior or usage changes.
- Keep filenames and headings descriptive and stable.
- **Technical Documentation Location**: Automatically save all technical `.md` files that do not properly belong to other specific `docs/` subfolders in `docs/technical`. Do not save them in the root folder.
- Commit expectations for documentation follow `.github/skills/xerahs-workflow/SKILL.md`.

### Security

- Do not include secrets or tokens.
- Avoid logging sensitive data in examples.

### Output Format

- For changes, summarize what changed and where.
- Provide next steps only when they are natural and actionable.
