## License Header Requirement

When creating or editing C# files (`.cs`) in this repository, include the following license header at the top of the file (tailored for the Avalonia implementation):

```csharp
#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

# Agent Rules

- Always summarize code changes in the final response, and use that summary when performing `git push` after each code update.

## Semantic Versioning

- Use semantic versioning for all versioned artifacts: MAJOR.MINOR.PATCH.
- MAJOR: breaking changes to public APIs, data contracts, or user-visible behavior.
- MINOR: backward-compatible features or enhancements.
- PATCH: backward-compatible bug fixes or small corrections.
- Pre-release and build metadata follow SemVer 2.0.0 conventions.

## Purpose

- This document provides clear operating instructions for LLM-assisted work in this repository.

## Scope

- Applies to documentation, code, tests, configs, and release notes.
- If a request conflicts with repository guidelines, ask for clarification.

## Communication

- Be concise and factual.
- Prefer short paragraphs and bullet lists.
- Use consistent terminology from existing docs.

## Repository Awareness

- Read existing docs before adding new guidance.
- Avoid duplicating information unless it is a deliberate summary.
- Keep instructions in ASCII unless the target file already uses Unicode.

## Change Safety

- Do not remove or rewrite unrelated content.
- Do not change version numbers unless explicitly requested.
- Flag assumptions clearly when requirements are ambiguous.

## Code and Config Changes

- Follow existing patterns in each project area.
- Keep changes minimal and targeted.
- Add small comments only when necessary to explain non-obvious logic.

## Testing

- If you modify executable code, suggest relevant tests.
- If tests are added, align them with current test conventions.

## Documentation

- Update or add docs when behavior or usage changes.
- Keep filenames and headings descriptive and stable.

## Security and Privacy

- Do not include secrets or tokens.
- Avoid logging sensitive data in examples.

## Output Format

- For changes, summarize what changed and where.
- Provide next steps only when they are natural and actionable.

## Future TODO - Uploaders Plug-in Architecture

- Extract common abstractions (e.g., `GenericUploader`, `UploaderService<T>`) into a core library referenced by the app and plug-ins.
- Split each uploader into its own class library (one per `FileDestination` entry) so `ShareX.UploadersLib.<Uploader>.dll` can be built individually.
- Replace in-assembly reflection with a dynamic plug-in loader that scans `C:\Users\<your-username>\AppData\Local\ShareX.Avalonia\Plugins` for uploader assemblies.
- Remove hard-coded uploader enums and update configuration UI to list plug-ins discovered at runtime.
- Update build/deployment scripts to package plug-in DLLs separately and load them on demand.
