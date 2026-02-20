---
name: ShareX Workflow and Versioning
description: Canonical Git, commit/push, and Directory.Build.props versioning workflow for XerahS. Use for any task that changes version numbers or requires commit/push.
---

## Scope

This file is the single source of truth for Git and versioning rules that involve:
- Commit and push workflow
- Commit message format
- Version bump behavior
- `Directory.Build.props` updates

This supersedes the retired `docs/development/RELEASE_PROCESS.md`.

## Version Source Of Truth

1. Treat `Directory.Build.props` files as the only app version source.
2. Never set version numbers in individual `.csproj` files.
3. When bumping version, update every `Directory.Build.props` in the repository so values match.
4. Read current version from the root `Directory.Build.props` first.

## Version Bump Policy

1. Bug fix: increment patch only (`0.0.z` rule: keep major/minor, increase `z`).
2. New feature: increment minor and reset patch.
3. Breaking change: increment major and reset minor/patch.

## Required Pre-Commit Checks

Before committing and pushing:

```bash
git pull --recurse-submodules
git submodule update --init --recursive
dotnet build XerahS.sln
```

Only continue when build succeeds with 0 errors.

## Commit And Push Procedure

1. Stage changes:
```bash
git add .
```
2. Commit using:
```bash
git commit -m "[vX.Y.Z] [Type] concise description"
```
3. Push:
```bash
git push
```

## Commit Message Rules

1. Prefix every commit with the new version: `[vX.Y.Z]`.
2. Include a type token such as `[Fix]`, `[Feature]`, `[Build]`, `[Docs]`, `[Refactor]`.
3. Keep the description concise and specific.

## Git Hook Expectations

1. Keep `.githooks` active (`git config core.hooksPath .githooks`).
2. Do not bypass hooks with `--no-verify` unless explicitly requested for emergency use.
3. If hooks fail, fix issues and recommit.

## Documentation And Git

1. Commit Markdown documentation changes with related code/config changes.
2. Do not leave generated instruction docs uncommitted when they are part of the requested work.
