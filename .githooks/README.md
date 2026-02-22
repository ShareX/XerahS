# Git Hooks for XerahS

This directory contains Git hooks for the XerahS project to enforce code quality and compliance standards.

## Available Hooks

### pre-commit

Validates **GPL v3** license headers in all staged **C#**, **Swift**, and **Kotlin** source files. All require the **full GPL v3 license text** (same as C#), not just a short copyright line.

**C# (`.cs`):**
- Presence of `#region License Information (GPL v3)` tag
- Correct project name: "XerahS - The Avalonia UI implementation of ShareX"
- Current copyright year: "Copyright (c) 2007-YYYY ShareX Team"
- Full GPL v3 license text

**Swift (`.swift`), e.g. `src/mobile/ios`:**
- Line with "XerahS Mobile (Swift)"
- Current copyright and **full GPL v3 license text** (as `//` line comments)

**Kotlin (`.kt`), e.g. `src/mobile/android`:**
- Block comment at top with project name, copyright, and **full GPL v3 license text**
- Must appear before the `package` declaration

See `developers/guidelines/CODING_STANDARDS.md` for exact header formats.

**Supported platforms:**
- Linux/macOS: Uses bash script (`pre-commit.bash`) via launcher (`pre-commit`)
- Windows: Uses PowerShell script (`pre-commit.ps1`) via launcher (`pre-commit`)

### pre-push

Ensures `ImageEditor` is attached to a branch and auto-pushes branch commits before pushing the superproject.

**What it does:**
- Runs the same detached `HEAD` recovery logic used by `post-checkout`/`post-merge`
- If `ImageEditor` is ahead of its upstream, pushes those commits automatically
- Sets upstream to `origin/<branch>` when possible

### post-checkout

Ensures `ImageEditor` is not left detached after checkout operations.

**What it does:**
- Detects detached `HEAD` in `ImageEditor`
- Resolves default branch from `origin/HEAD` (fallback: `develop`, `main`, `master`)
- Checks out the default branch locally
- Attempts fast-forward to upstream when available
- Optional auto-push support when `xerahs.hooks.imageeditorautopush=true`

**Cross-platform execution:**
- Launcher: `.githooks/sync-imageeditor-head`
- Linux/macOS: delegates to `.githooks/sync-imageeditor-head.bash`
- Windows: delegates to `.githooks/sync-imageeditor-head.ps1` (with bash fallback)

### post-merge

Runs the same detached-HEAD recovery logic as `post-checkout` after merges.

## Installation

### Option 1: Configure Git to Use .githooks Directory (Recommended)

This makes the hooks available to all contributors:

```bash
git config core.hooksPath .githooks
```

To verify it's configured:
```bash
git config core.hooksPath
# Should output: .githooks
```

### Option 2: Manual Symlink (Per User)

**Linux/macOS:**
```bash
ln -s ../../.githooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

**Windows (PowerShell as Administrator):**
```powershell
New-Item -ItemType SymbolicLink -Path ".git\hooks\pre-commit" -Target "..\..\. githooks\pre-commit.ps1"
```

## Usage

Once installed, hooks run automatically on:
- `git commit` (`pre-commit`)
- `git push` (`pre-push`)
- `git checkout`/branch switches (`post-checkout`)
- `git merge` (`post-merge`)

Optional auto-push on checkout/merge as well:

```bash
git config xerahs.hooks.imageeditorautopush true
```

### Bypassing Hooks (Not Recommended)

If you need to bypass hooks temporarily (e.g., work-in-progress commit):

```bash
git commit --no-verify
```

**Warning:** Bypassing hooks is strongly discouraged as it may introduce non-compliant code.

## Fixing License Header Violations

If the pre-commit hook detects violations:

### C# files

**Option 1 – Automatic:** Run `pwsh docs/scripts/fix_license_headers.ps1`, then re-stage and commit.

**Option 2 – Manual:** Update the file headers to match the expected format:

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

### Swift / Kotlin files

Add the **full GPL v3 license text** at the top of each `.swift` or `.kt` file (see `developers/guidelines/CODING_STANDARDS.md` for the exact block). Swift uses `//` line comments; Kotlin uses a `/* ... */` block comment before the `package` line. Then re-stage and commit: `git add <files>` and `git commit`.

## Troubleshooting

### Hook Not Running

1. Verify hooks path configuration:
   ```bash
   git config core.hooksPath
   ```

2. Check file permissions (Linux/macOS):
   ```bash
   ls -la .githooks/pre-commit
   ls -la .githooks/pre-push .githooks/post-checkout .githooks/post-merge .githooks/sync-imageeditor-head.bash
   chmod +x .githooks/pre-commit  # If not executable
   ```

3. Verify PowerShell execution policy (Windows):
   ```powershell
   Get-ExecutionPolicy
   # If Restricted, run:
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

### False Positives

If the hook incorrectly flags a file:

1. Verify the file actually has the correct header
2. Check for invisible characters or encoding issues
3. Ensure the file uses UTF-8 encoding
4. Report the issue with the specific file path

### Platform-Specific Issues

**Windows:**
- Ensure PowerShell 5.1+ is installed
- Check that `.ps1` files are associated with PowerShell
- Run `pwsh --version` to verify

**Linux/macOS:**
- Ensure bash is available: `which bash`
- Verify Git version: `git --version` (2.9+ recommended)

## Development

### Adding New Hooks

1. Create the hook script in `.githooks/`
2. Make it executable (Linux/macOS): `chmod +x .githooks/<hook-name>`
3. Add documentation to this README
4. Test thoroughly before committing

### Testing Hooks

To test the pre-commit hook without actually committing:

```bash
# Stage some C# files
git add src/SomeFile.cs

# Run the hook directly
.githooks/pre-commit          # All platforms (launcher)
pwsh .githooks/pre-commit.ps1 # Windows (PowerShell)
.githooks/pre-commit.bash     # Linux/macOS (bash)

# Unstage files
git reset HEAD
```

## CI/CD Integration

The same validation runs in CI/CD pipelines. See `.github/workflows/` for integration details.

## References

- [Git Hooks Documentation](https://git-scm.com/book/en/v2/Customizing-Git-Git-Hooks)
- [GPL v3 License](https://www.gnu.org/licenses/gpl-3.0.en.html)
- [XerahS License Compliance Guide](../LICENSE_COMPLIANCE_README.md)

---

**Last Updated:** 2026-02-18
**Maintainer:** XerahS Development Team
