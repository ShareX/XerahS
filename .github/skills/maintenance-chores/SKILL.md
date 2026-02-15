# Maintenance Chores Skill

**Objective**: Automate periodic maintenance tasks for XerahS including version bumps, changelog updates, and repository synchronization.

**Scope**: This skill manages routine operations across all XerahS repositories (main, ImageEditor, website) to keep versions and documentation in sync.

---

## 📋 Chore Workflow

### Phase 1: Repository Synchronization

**Purpose**: Ensure all local repositories are up-to-date with remote branches.

#### 1.1 Pull Main Repository
```powershell
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" pull origin develop
```
- Pulls the latest changes from the `develop` branch of the main XerahS repository
- Verify exit code is 0 (success)

#### 1.2 Pull ImageEditor Repository
```powershell
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS\ImageEditor" pull origin main
```
- Pulls the latest changes from ImageEditor submodule/nested repository
- This typically targets `main` or `develop` depending on ImageEditor's branch strategy
- Verify exit code is 0 (success)

#### 1.3 Pull Website Repository (Optional)
```powershell
git -C "c:\Users\liveu\source\repos\ShareX Team\xerahs.github.io" pull origin main
```
- Pulls the latest changes from the website repository for coordinated updates
- Optional if not performing website updates in this chore cycle

---

### Phase 2: Version Bumping

**Purpose**: Update version numbers across all relevant project files.

#### 2.1 Identify Current Version
```powershell
$version = (Select-String -Path "c:\Users\liveu\source\repos\ShareX Team\XerahS\Directory.Build.props" -Pattern '<Version>(.+?)</Version>').Matches[0].Groups[1].Value
Write-Host "Current Version: $version"
```
- Extract the current semantic version from `Directory.Build.props`
- Format: `major.minor.patch` (e.g., `0.15.5`)

#### 2.2 Bump Version
**Versioning Strategy**:
- **Patch bump**: `0.15.5` → `0.15.6` (bug fixes, minor updates)
- **Minor bump**: `0.15.5` → `0.16.0` (new features)
- **Major bump**: `0.15.5` → `1.0.0` (breaking changes)

**Update Files**:
1. **Main Directory.Build.props**
   - File: `Directory.Build.props`
   - Update `<Version>X.Y.Z</Version>`

2. **ImageEditor Directory.Build.props**
   - File: `ImageEditor/Directory.Build.props`
   - Update `<Version>X.Y.Z</Version>`
   - Note: May maintain different version from main project

#### 2.3 Validation
- Verify both `Directory.Build.props` files contain the updated version
- Run `dotnet build` to confirm no version-related compilation errors
- Check that version displays correctly in built assemblies

---

### Phase 3: Changelog Updates

**Purpose**: Document all changes since the last release in a maintainable format.

#### 3.0 Execute Changelog Update Skill (Primary Method)

**Location**: `.github/skills/update-changelog/SKILL.md`

Run the dedicated changelog management skill which handles:
- Version grouping strategy (minor version breakdowns)
- Consolidation rules (patch versions, pre-release fixes)
- Specific commit assignment
- Attribution formatting (external contributors with PR/username)
- Categorization and formatting per Keep a Changelog standard

**When to Use**:
- This is the **primary** approach for comprehensive changelog updates
- Ensures consistency with the established changelog management rules
- Handles complex version histories and rollups automatically

#### 3.1 Identify Change Categories
Review since last tag/release:
- 🔧 **Features**: New capabilties, enhancements
- 🐛 **Fixes**: Bug fixes, corrections
- 📚 **Documentation**: README, guides, API docs
- ⚡ **Performance**: Optimizations, speed improvements
- 🔒 **Security**: Security patches
- 🏗️ **Refactor**: Code restructuring (non-user-impacting)
- 🧪 **Testing**: Test additions and improvements
- ⬆️ **Dependencies**: Dependency updates (SkiaSharp version notes)

#### 3.2 Update CHANGELOG.md (Manual/Supplemental)
**Location**: `docs/CHANGELOG.md`

Entry Format:
```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
- Bullet point for each new feature

### Fixed
- Bullet point for each bug fix

### Changed
- Bullet point for each improvement

### Removed
- Bullet point for each removal (if any)

### Security
- Bullet point for security updates (if any)
```

#### 3.3 Update Version References
- **README.md**: Update any version badges/download links if applicable
- **docs/PROJECT_STATUS.md**: Update current release version if applicable
- **FAQ.md**: Update version references in examples/download links if any

---

### Phase 4: Commit and Push

**Purpose**: Record changes and synchronize with remote repositories.

#### 4.1 Stage Changes
```powershell
# Main repository
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" add .

# ImageEditor repository (if modified)
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS\ImageEditor" add .

# Website repository (if updated)
git -C "c:\Users\liveu\source\repos\ShareX Team\xerahs.github.io" add .
```

#### 4.2 Commit with Proper Format
**Commit Message Format**: `[vX.Y.Z] [Chore] Update version and changelog`

```powershell
# Main repository
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" commit -m "[v0.15.6] [Chore] Update version and changelog for release"

# ImageEditor repository (if needed)
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS\ImageEditor" commit -m "[v0.15.6] [Chore] Update ImageEditor version"
```

**Commit Message Components**:
- `[vX.Y.Z]`: Version tag matching Directory.Build.props version
- `[Chore]`: Classification per AGENTS.md 
- Concise description of what was updated

#### 4.3 Push to Remote
```powershell
# Main repository
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" push origin develop

# ImageEditor repository
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS\ImageEditor" push origin main

# Website repository (if applicable)
git -C "c:\Users\liveu\source\repos\ShareX Team\xerahs.github.io" push origin main
```

#### 4.4 Verification
- Confirm all pushes succeeded (exit code 0)
- Visit GitHub UI to verify commits appear on remote branches
- Check that no merge conflicts need resolution

---

## 🔧 Implementation Checklist

- ✅ Pull all three repositories (main XerahS, ImageEditor, website)
- ✅ Determine appropriate version bump (patch/minor/major)
- ✅ Update `Directory.Build.props` in both main and ImageEditor
- ✅ **Run `.github/skills/update-changelog/SKILL.md` to consolidate and format changelog** ← PRIMARY STEP
- ✅ Update version references in README/docs as needed
- ✅ Run `dotnet build` to validate changes
- ✅ Stage all modified files across repositories
- ✅ Create commits with `[vX.Y.Z] [Chore]` format
- ✅ Push to remote branches (develop for main, main for ImageEditor)
- ✅ Verify all pushes succeeded
- ✅ Confirm changes visible on GitHub

---

## 📝 Prerequisites

- Local repositories cloned at expected paths:
  - `c:\Users\liveu\source\repos\ShareX Team\XerahS`
  - `c:\Users\liveu\source\repos\ShareX Team\XerahS\ImageEditor`
  - `c:\Users\liveu\source\repos\ShareX Team\xerahs.github.io` (optional)
- Git configured with user identity
- Push access to all target repositories
- Working directory clean (no uncommitted changes in unrelated files)

---

## ⚠️ Important Notes

1. **SkiaSharp Version Lock**:
   - NEVER bump SkiaSharp beyond 2.88.9
   - Update CHANGELOG.md to reflect this constraint if dependencies change

2. **Semantic Versioning**:
   - Follow semver strictly (major.minor.patch)
   - Communicate breaking changes clearly in changelog

3. **Branch Strategy**:
   - Main XerahS: commit to `develop` branch
   - ImageEditor: commits to `main` branch (or per ImageEditor's strategy)
   - Website: commits to `main` branch

4. **Atomic Operations**:
   - All three repositories should be updated together for consistency
   - If one fails, consider rolling back others or understanding the reason

---

## 🚀 Quick Command Reference

**Pull all repositories**:
```powershell
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" pull origin develop
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS\ImageEditor" pull origin main
```

**Stage, commit, and push all**:
```powershell
git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" add . ; git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" commit -m "[vX.Y.Z] [Chore] Update version and changelog" ; git -C "c:\Users\liveu\source\repos\ShareX Team\XerahS" push origin develop
```

---

## 🔗 Related Documentation

- [Update Changelog Skill](.github/skills/update-changelog/SKILL.md) - **Dedicated changelog management, versioning, and consolidation rules** (Primary step in Phase 3)
- [AGENTS.md](../../AGENTS.md) - General git workflow and commit format standards
- [Development Standards](../development/CODING_STANDARDS.md)
- [Release & Versioning](.github/skills/xerahs-workflow/SKILL.md)

