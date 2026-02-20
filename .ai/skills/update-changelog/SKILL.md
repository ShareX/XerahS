---
name: Changelog Management
description: Rules and workflows for updating CHANGELOG.md, including version grouping, consolidation, and commit handling.
---

## Version Grouping Strategy

### Git Tag-Based Consolidation
- **CRITICAL**: Check `git tag -l` to identify the last released version tag.
- **All commits after the last git tag** must be consolidated into a single heading for the next minor version.
    - **Example**: If the last tag is `v0.15.5`, then ALL commits after that tag (including any changelog sections like v0.15.6, v0.15.7, v0.16.0, v0.16.1) must be **merged into v0.16.0**.
- **Never create multiple version headings** between git tags. Only one version heading should exist between any two tags.

### Minor Version Breakdowns
- **Always** break changes down into minor versions (e.g., `v0.8.0`, `v0.9.0`, `v0.10.0`) even if `Directory.Build.props` or git tags do not explicitly show them.
- Analyze `git log` and `Directory.Build.props` history to identify implicit boundaries where version bumps occurred or were implied.

### Consolidation Rules
- **Patch Versions**: Consolidate patch versions (e.g., `v0.8.x`) into the next significant minor version (e.g., `v0.9.0`) unless the patch version was a major standalone release.
    - **Example**: Items from `v0.8.1`, `v0.8.2`, `v0.8.3` should be merged and listed under **v0.9.0**.
    - Retain original context if needed (e.g., "Feature: ... `(v0.8.1)`").
- **Pre-Release Fixes**: Move post-release fixes from previous versions into the next minor version.
    - **Example**: Fixes made *after* the `v0.7.7` tag but historically listed under the `v0.7.7` header should be moved to **v0.8.0**.

## Commit Handling

### Specific Commit Assignment
- Respect specific user requests to assign certain commits to specific versions.
- **Example**: "List commit `298457a` under **v0.11.0**."
- Always verify the commit hash and subject before assignment.

### Attribution
- **External Contributors**: Attribute Pull Requests from external contributors by including the PR number and their username.
    - **Format**: `(#PR_NUMBER, @username)`
    - **Example**: `(#77, @Hexeption)`
- **Maintainer Merges**: Exclude merge commits from the main maintainer (e.g., `McoreD`) from having explicit attribution unless they contain significant unique work not covered by other commits. The focus is on crediting other users.

### Categorization
Group changes within each version using standard categories:
- **Feature**: New functionality.
- **Fix**: Bug fixes.
- **Refactor**: Code improvements without external behavior change.
- **Build**: Build system, dependencies, and packaging.
- **Core**: Core foundation changes.
- **Infrastructure**: Repo, git, or workflow changes.

### Entry Consolidation to Reduce Line Count
**CRITICAL**: Consolidate related commits into single entries to keep the changelog concise and readable.

#### Guidelines:
- **Group by Component and Purpose**: Combine multiple commits that affect the same component and serve the same purpose.
- **Preserve All Commit Hashes**: When consolidating, include all relevant commit hashes in a single line.
- **Target Reduction**: Aim for 30-50% line reduction by consolidating related work.

#### Examples:

**Before (verbose)**:
```markdown
- **Media Explorer**: Add `IUploaderExplorer` interface `(9deedf9)`
- **Media Explorer**: Implement S3 file browser `(9deedf9)`
- **Media Explorer**: Implement Imgur album browser `(9deedf9)`
- **Media Explorer**: Add navigation, breadcrumbs, search, filter `(9deedf9)`
- **Media Explorer**: Add bandwidth savings banner `(e374160)`
```

**After (consolidated)**:
```markdown
- **Media Explorer**: Implement provider file browsing with S3 and Imgur support, including navigation, search, filtering, and CDN thumbnail optimization `(9deedf9, e374160)`
```

**Before (mobile features)**:
```markdown
- **Mobile**: Add adaptive mobile theming infrastructure `(4b79ddb)`
- **Mobile**: Refactor mobile views for adaptive native styling `(a7cfb22)`
- **Mobile**: Align mobile heads with native theming defaults `(1e5f9eb)`
- **Mobile**: Complete sprint 5 mobile theming polish and docs `(30bbe98)`
- **Mobile**: Add mobile upload queue and picker `(68d97d9)`
- **Mobile**: Add mobile upload history screens `(52d6ad2)`
```

**After (consolidated)**:
```markdown
- **Mobile**: Add adaptive theming infrastructure with native styling polish `(4b79ddb, a7cfb22, 1e5f9eb, 30bbe98)`
- **Mobile**: Add upload queue, picker, and history screens `(68d97d9, 52d6ad2)`
```

**Before (fixes)**:
```markdown
- **Scrolling Capture**: Always auto-scroll to top `(1fa45f2)`
- **Scrolling Capture**: Apply workflow settings and refresh hotkeys `(971219c)`
- **Scrolling Capture**: Use current scroll position for detection `(8ac2c8b)`
```

**After (consolidated)**:
```markdown
- **Scrolling Capture**: Improve auto-scroll behavior and workflow settings integration `(1fa45f2, 971219c, 8ac2c8b)`
```

#### When NOT to Consolidate:
- Commits from different components (e.g., don't merge "Mobile" with "Linux Capture")
- Commits with external contributor attribution (keep separate for visibility)
- Significant standalone features that deserve their own entry

## Format
Follow the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format with Semantic Versioning.

```markdown
## vX.Y.Z

### Features
- **Component**: Description `(short-hash)`
- **Component**: Description `(short-hash, short-hash)`

### Fixes
- Description `(short-hash)`
```

## Workflow

### Step-by-Step Process

1. **Identify Last Git Tag**
   ```bash
   git tag -l | Sort-Object -Descending | Select-Object -First 1
   ```
   This determines the boundary for version consolidation.

2. **Get Commits Since Last Tag**
   ```bash
   git log v0.X.Y..HEAD --oneline --no-decorate
   ```
   Analyze all commits that need to be documented.

3. **Check Current Version in Directory.Build.props**
   Read the `<Version>` property to determine the target version number.

4. **Consolidate Version Headings**
   - Remove ALL version headings between the last git tag and current HEAD
   - Create a SINGLE heading for the next minor version (from Directory.Build.props)
   - Move all commits into this single version heading

5. **Categorize Commits**
   - Group commits into: Features, Fixes, Refactor, Build, Documentation
   - Within each category, group by component (e.g., Mobile, Linux Capture, Editor)

6. **Consolidate Related Entries**
   - Identify commits affecting the same component with similar purpose
   - Merge them into single, comprehensive entries
   - Preserve all commit hashes
   - Aim for 30-50% reduction in line count

7. **Format and Verify**
   - Ensure proper markdown formatting
   - Verify all commit hashes are present
   - Check that external contributor attributions are preserved
   - Confirm adherence to Keep a Changelog format

### Example Command Sequence
```powershell
# Get last tag
$lastTag = git tag -l | Sort-Object -Descending | Select-Object -First 1

# Get commits since tag
git log $lastTag..HEAD --oneline --no-decorate

# Check current version
$version = Select-String -Path "Directory.Build.props" -Pattern '<Version>(.*)</Version>' | ForEach-Object { $_.Matches.Groups[1].Value }

# Update CHANGELOG.md with consolidated entries
```
