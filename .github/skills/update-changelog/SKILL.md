---
name: Changelog Management
description: Rules and workflows for updating CHANGELOG.md, including version grouping, consolidation, and commit handling.
---

## Version Grouping Strategy

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
