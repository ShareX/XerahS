---
name: ShareX Workflow and Versioning
description: Semantic versioning, commit message standards, multi-agent coordination, and workflow guidelines
---

## Semantic Versioning Automation

**Product Name**: ShareX Ava (Project files/UI should reflect this, code namespace remains `ShareX.Avalonia`)

**Current Version**: 0.1.0 (Managed centrally in `Directory.Build.props`)

**Rules for Agents**:

1. **Automated Version Bumping**:
   - **PATCH (x.x.X)**: Bump for bug fixes, refactors, or minor tasks (Complexity ≤ 3).
   - **MINOR (x.X.x)**: Bump for new features, significant UI changes, or new workflows (Complexity 4-7).
   - **MAJOR (X.x.x)**: Bump for breaking changes or major releases (Complexity ≥ 8).

2. **How to Bump**:
   - Check `Directory.Build.props` for the current version.
   - Increment accordingly based on the highest complexity of changes in your session.
   - Update `<Version>` tag in `Directory.Build.props`.
   - **IMPORTANT**: `Directory.Build.props` is the **single source of truth** for the application version. When updating the version, you MUST update this file.
   - **Do not** update individual `.csproj` versions; they inherit from `Directory.Build.props`.

3. **Commit Messages**:
   - Prefix commits with `[vX.Y.Z]` relative to the new version.
   - Example: `[v0.1.1] [Fix] Captured images now display in Editor`

## Semantic Versioning Standards

- Uses standard SemVer 2.0.0 (MAJOR.MINOR.PATCH).
- Pre-release tags allowed (e.g., `0.1.0-alpha.1`) for unstable features.

## Multi-Agent Coordination

This project uses multiple AI developer agents working in parallel. See [MULTI_AGENT_COORDINATION.md](../../../docs/agents/MULTI_AGENT_COORDINATION.md) for:
- Agent roles (Antigravity, Codex, Copilot)
- Task distribution rules
- Git workflow and branch naming
- Conflict avoidance protocols
- Communication requirements

**Lead Agent**: Antigravity (architecture, integration, merge decisions)

- Always summarize code changes in the final response, and use that summary when performing `git push` after each code update.

## Scope

- This document provides clear operating instructions for LLM-assisted work in this repository.
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
