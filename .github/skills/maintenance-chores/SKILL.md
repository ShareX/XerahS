# Maintenance Chores Skill

**Objective**: Automate periodic maintenance tasks for XerahS including version bumps, changelog updates, and repository synchronization.

**Scope**: This skill manages routine operations across all XerahS repositories (main, ImageEditor, website) to keep versions and documentation in sync.

---

## 📋 Chore Workflow

### Phase 1: Repository Synchronization

**Purpose**: Ensure all local repositories are up-to-date with remote branches.

#### 1.1 Pull Main Repository
```powershell
git pull origin develop
```
- Pulls the latest changes from the `develop` branch of the main XerahS repository
- Verify exit code is 0 (success)

#### 1.2 Pull ImageEditor Repository
```powershell
git -C ImageEditor pull origin main
```
- Pulls the latest changes from ImageEditor submodule/nested repository
- This typically targets `main` or `develop` depending on ImageEditor's branch strategy
- Verify exit code is 0 (success)

#### 1.3 Pull Website Repository (Optional)
```powershell
git -C ../xerahs.github.io pull origin main
```
- Pulls the latest changes from the website repository for coordinated updates
- Optional if not performing website updates in this chore cycle

---

### Phase 2: Version Bumping

**Purpose**: Update version numbers across all relevant project files.

#### 2.1 Identify Current Version
```powershell
$version = (Select-String -Path Directory.Build.props -Pattern '<Version>(.+?)</Version>').Matches[0].Groups[1].Value
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

**Purpose**: Record changes and synchronize with remote repositories. **CRITICAL**: Commit and push **everything** in **every repository** including **all submodules**.

#### 4.0 Check All Repository Status
**Before committing, verify what needs to be committed across ALL repos:**

```powershell
# Check main repository
git status --short

# Check ImageEditor submodule
git -C ImageEditor status --short

# Check website repository
git -C ../xerahs.github.io status --short

# Check ShareX repository (if applicable)
git -C ../ShareX status --short
```

**Important**: Always check ALL submodules, not just known ones. If detached HEAD is detected in any submodule, checkout the appropriate branch first.

#### 4.1 Stage Changes in ALL Repositories
**Stage changes in every repository and submodule:**

```powershell
# Main repository
git add .

# ImageEditor submodule (always check, even if no direct edits)
git -C ImageEditor add .

# Website repository
git -C ../xerahs.github.io add .

# ShareX repository (if applicable)
git -C ../ShareX add .
```

#### 4.2 Commit with Proper Format in ALL Repositories
**Commit Message Format**: `[vX.Y.Z] [Chore] Update version and changelog`

**CRITICAL**: Commit changes in ALL repositories, even if they seem unrelated to version bump:

```powershell
# ImageEditor submodule (commit FIRST - submodules before parent)
git -C ImageEditor commit -m "[v0.15.6] [Chore] Update ImageEditor version and changes"

# Main repository (commit AFTER submodules to capture updated references)
git commit -m "[v0.15.6] [Chore] Update version and changelog for release"

# Website repository (if updated)
git -C ../xerahs.github.io commit -m "[v0.15.6] [Chore] Update website content"

# ShareX repository (if applicable)
git -C ../ShareX commit -m "[v0.15.6] [Chore] Update ShareX changes"
```

**Commit Message Components**:
- `[vX.Y.Z]`: Version tag matching Directory.Build.props version
- `[Chore]`: Classification per AGENTS.md 
- Concise description of what was updated

**Submodule Commit Order**:
1. **First**: Commit all submodules (deepest first if nested)
2. **Last**: Commit parent repository to capture updated submodule references

#### 4.3 Push to Remote in ALL Repositories
**Push ALL repositories including submodules:**

```powershell
# ImageEditor submodule (push FIRST - before parent)
git -C ImageEditor push origin develop

# Main repository (push AFTER submodules)
git push origin develop

# Website repository
git -C ../xerahs.github.io push origin main

# ShareX repository (if applicable)
git -C ../ShareX push origin develop
```

**Push Order**: Submodules first, parent repository last.

#### 4.4 Verification
- Confirm **ALL** pushes succeeded (exit code 0) for **every repository**
- Visit GitHub UI to verify commits appear on remote branches for **all repos**
- Verify submodule references are updated in parent repository
- Check that no merge conflicts need resolution in **any repository**
- Confirm working tree is clean in **all repositories** after push

---

## 🔧 Implementation Checklist

- ✅ Pull all repositories (main XerahS, ImageEditor submodule, website, ShareX)
- ✅ Determine appropriate version bump (patch/minor/major)
- ✅ Update `Directory.Build.props` in both main and ImageEditor
- ✅ **Run `.github/skills/update-changelog/SKILL.md` to consolidate and format changelog** ← PRIMARY STEP
- ✅ Update version references in README/docs as needed
- ✅ Run `dotnet build` to validate changes
- ✅ **Check status of ALL repositories and submodules (`git status --short` in each)**
- ✅ **Stage ALL modified files in ALL repositories including submodules**
- ✅ **Commit ALL repositories (submodules first, parent last) with `[vX.Y.Z] [Chore]` format**
- ✅ **Push ALL repositories to remote (submodules first, parent last)**
- ✅ **Verify ALL pushes succeeded and working tree is clean in ALL repos**
- ✅ Confirm changes visible on GitHub for all repositories

---

## 📝 Prerequisites

- Local repositories cloned as siblings under `ShareX Team/` directory:
  - `XerahS/` (main repository, current working directory)
  - `XerahS/ImageEditor/` (submodule)
  - `xerahs.github.io/` (sibling repository, optional)
  - `ShareX/` (sibling repository, optional)
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

**Check status of ALL repositories**:
```powershell
git status --short
git -C ImageEditor status --short
git -C ../xerahs.github.io status --short
git -C ../ShareX status --short
```

**Pull all repositories**:
```powershell
git pull origin develop
git -C ImageEditor pull origin develop
git -C ../xerahs.github.io pull origin main
git -C ../ShareX pull origin develop
```

**Stage, commit, and push ALL repositories (including submodules)**:
```powershell
# Submodules first, parent last
git -C ImageEditor add . ; git -C ImageEditor commit -m "[vX.Y.Z] [Chore] Update ImageEditor" ; git -C ImageEditor push origin develop

# Then parent repository
git add . ; git commit -m "[vX.Y.Z] [Chore] Update version and changelog" ; git push origin develop

# Website and other repos
git -C ../xerahs.github.io add . ; git -C ../xerahs.github.io commit -m "[vX.Y.Z] [Chore] Update website" ; git -C ../xerahs.github.io push origin main
```

---

## 🔗 Related Documentation

- [Update Changelog Skill](.github/skills/update-changelog/SKILL.md) - **Dedicated changelog management, versioning, and consolidation rules** (Primary step in Phase 3)
- [AGENTS.md](../../AGENTS.md) - General git workflow and commit format standards
- [Development Standards](../development/CODING_STANDARDS.md)
- [Release & Versioning](.github/skills/xerahs-workflow/SKILL.md)

