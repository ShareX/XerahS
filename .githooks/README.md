# Git Hooks for XerahS

This directory contains Git hooks for the XerahS project to enforce code quality and compliance standards.

## Available Hooks

### pre-commit

Validates GPL v3 license headers in all staged C# files before allowing a commit.

**What it checks:**
- Presence of `#region License Information (GPL v3)` tag
- Correct project name: "XerahS - The Avalonia UI implementation of ShareX"
- Current copyright year: "Copyright (c) 2007-2026 ShareX Team"
- GPL v3 license text

**Supported platforms:**
- Linux/macOS: Uses bash script (`pre-commit`)
- Windows: Uses PowerShell script (`pre-commit.ps1`)

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

Once installed, the hooks run automatically on `git commit`.

### Bypassing Hooks (Not Recommended)

If you need to bypass hooks temporarily (e.g., work-in-progress commit):

```bash
git commit --no-verify
```

**Warning:** Bypassing hooks is strongly discouraged as it may introduce non-compliant code.

## Fixing License Header Violations

If the pre-commit hook detects violations:

### Option 1: Automatic Fix

Run the license header fix script:

```powershell
pwsh docs/scripts/fix_license_headers.ps1
```

Then re-stage the fixed files:

```bash
git add <files>
git commit
```

### Option 2: Manual Fix

Update the file headers to match the expected format:

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

## Troubleshooting

### Hook Not Running

1. Verify hooks path configuration:
   ```bash
   git config core.hooksPath
   ```

2. Check file permissions (Linux/macOS):
   ```bash
   ls -la .githooks/pre-commit
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
.githooks/pre-commit       # Linux/macOS
pwsh .githooks/pre-commit.ps1  # Windows

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

**Last Updated:** 2026-01-18
**Maintainer:** XerahS Development Team
