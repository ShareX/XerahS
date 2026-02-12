---
name: XIP Writing (XerahS Improvement Proposals)
description: How to write effective XerahS Improvement Proposals (XIPs) for new features, architecture changes, and cross-platform implementations
---

# XIP Writing Skill

This skill guides writing XerahS Improvement Proposals (XIPs) that are actionable, well-structured, and aligned with project architecture.

---

## What is a XIP?

A XIP (XerahS Improvement Proposal) is a design document that:
- Describes a new feature, enhancement, or architectural change
- Provides implementation guidance for developers
- Serves as the single source of truth for the feature scope
- Links to a GitHub issue for tracking

---

## XIP Structure

### Header Template

```markdown
# XIP0001 Short Descriptive Title

**Status**: Draft | In Review | Ready for Implementation | In Progress | Completed  
**Created**: YYYY-MM-DD  
**Updated**: YYYY-MM-DD  
**Area**: Desktop | Mobile | Core | Uploaders | UI | Architecture  
**Goal**: One-sentence description of what this achieves.

---
```

### Naming Convention

**Format**: `XIPXXXX Description`  
- XIP number padded with leading zeros (4 digits)
- Single space between number and description
- **NO** square brackets [ ]
- **NO** colon after XIP number
- **NO** hyphen/dash between number and description

| Format | Status |
|--------|--------|
| `XIP0030 Mobile Share Feature` | ✅ Correct |
| `XIP0030: Mobile Share Feature` | ❌ No colon |
| `[XIP0030] Mobile Share Feature` | ❌ No brackets |
| `XIP0030 - Mobile Share Feature` | ❌ No dash |
| `XIP30 Mobile Share Feature` | ❌ Must be 4 digits |

This format applies to:
- Markdown file names: `XIP0030_Mobile_Share_Feature.md`
- GitHub issue titles: `XIP0030 Mobile Share Feature`
- Headers inside XIP files: `# XIP0030 Mobile Share Feature`

### Required Sections

#### 1. Overview
- What problem does this solve?
- Why is this approach chosen?
- Key principles (reuse, no duplication, platform neutrality, etc.)
- **Keep it concise** - 2-4 paragraphs maximum

#### 2. Prerequisites (if any)
- SDK versions
- Platform requirements
- Dependencies to install

#### 3. Implementation Phases

Break into logical phases. Each phase should:
- Have a clear objective
- Include code examples where helpful
- Reference existing patterns to follow
- List specific files to create/modify

**Example Phase Structure:**
```markdown
### Phase N: Phase Name

Description of what this phase accomplishes.

**Key Files:**
- `src/XerahS.Core/Services/NewService.cs`

**Code Example:**
```csharp
// Include representative code
```

**Rules:**
- Specific constraints for this phase
- What NOT to do
```

#### 4. Non-Negotiable Rules

Explicit constraints that must be followed:
- Do not create duplicate upload pipelines
- Do not call providers directly from platform code
- Do not duplicate TaskSettings cloning logic
- Platform-specific code stays in platform folders
- etc.

#### 5. Deliverables

Numbered list of concrete outputs:
1. New service in XerahS.Core
2. Settings additions
3. Platform implementations
4. UI components
5. Documentation

#### 6. Affected Components

List of projects/files that will change:
- XerahS.Core: Specific classes
- XerahS.App: Platform entry points
- XerahS.Uploaders: Provider changes (if any)
- etc.

#### 7. Architecture Summary (recommended)

ASCII diagram showing data flow:
```
Component A
    ↓
Component B  ←  XerahS.Core
    ↓
Component C
```

---

## Writing Principles

### 1. Reuse Existing Infrastructure

**Before writing new code, study:**
- WatchFolderManager pattern for file handling
- TaskManager.StartFileTask for upload triggering
- TaskSettings.Clone for settings handling
- UploaderProviderBase for provider patterns
- TaskHelpers for file naming/storage

**Golden Rule:** If XerahS already does something similar, reuse that pattern.

### 2. Platform Neutrality

- Core logic goes in `XerahS.Core` or `XerahS.Common`
- Platform-specific code stays in `Platforms/` folders
- Use abstractions (interfaces) to bridge platform and core
- Core services must not know about Android, iOS, or Avalonia views

### 3. No Duplicate Logic

Explicitly forbid:
- Parallel upload pipelines
- Copy-pasted file handling code
- Reimplemented file naming logic
- Duplicated settings cloning
- Direct provider calls from platform code

### 4. Privacy and Security First

- Prefer explicit user action over automatic monitoring
- Request permissions only when necessary
- Use platform secure storage for credentials
- Document privacy implications

### 5. Clear Separation of Concerns

| Layer | Responsibility |
|-------|---------------|
| Platform Heads | Receive platform data, convert to local files |
| Core Services | Process files, invoke TaskManager |
| TaskManager | Coordinate upload jobs |
| Providers | Execute actual uploads |
| UI | Display results, user interaction |

---

## XIP Evolution Process

XIPs often evolve during review. Document significant changes:

```markdown
## Evolution History

| Date | Change | Rationale |
|------|--------|-----------|
| 2026-02-12 | Switched from auto-monitoring to share-based | Battery/privacy concerns |
```

When rewriting a XIP:
1. Preserve the XIP number
2. Update the "Updated" date
3. Document the rationale for changes
4. Update linked GitHub issue

---

## GitHub Issue Integration

Every XIP must have a corresponding GitHub issue:

### Issue Title Format

```
XIP0001 Brief Description
```

**Must follow XIP naming convention:**
- 4-digit zero-padded number
- Single space separator
- No brackets, no colon, no dash

Example: `XIP0030 Implement Share to XerahS on Mobile`

### Issue Body Should Include
- Link to XIP file
- Status
- Area/Labels
- Brief summary
- Key deliverables checklist

### Keep in Sync
When XIP changes, update the GitHub issue:
```bash
gh issue edit <number> --title "New Title" --body "Updated body"
```

---

## Review Checklist

Before finalizing a XIP:

- [ ] Header information complete (Status, Dates, Area, Goal)
- [ ] Overview explains the "why"
- [ ] Implementation phases are logical and actionable
- [ ] Code examples are syntactically correct
- [ ] References to existing patterns are accurate
- [ ] Non-negotiable rules explicitly stated
- [ ] Deliverables are concrete and verifiable
- [ ] Affected components listed
- [ ] Architecture diagram included (for complex features)
- [ ] No duplicate logic implied
- [ ] Platform neutrality maintained
- [ ] GitHub issue created/updated

---

## Common Patterns

### Pattern: Share-to-App (Mobile)

When implementing "share" functionality:

1. Platform receives shared content (Intent/Share Extension)
2. Platform copies to local app storage
3. Platform signals/passes paths to Core service
4. Core service processes via TaskManager
5. UI displays results

### Pattern: New Upload Provider

When adding an uploader:

1. Extend UploaderProviderBase
2. Add config model
3. Add settings UI (reuse existing patterns)
4. Implement upload logic
5. Register in provider factory
6. No changes to upload pipeline

### Pattern: Cross-Platform Service

When creating a service that works everywhere:

1. Define interface in Abstractions
2. Implement platform-specific versions in Platform folders
3. Register in PlatformServices or DI
4. Use from Core via interface only

---

## Anti-Patterns to Avoid

### Don't: Auto-Monitoring on Mobile
```markdown
Monitor screenshots folder automatically  ← BAD
Let user share files explicitly           ← GOOD
```

### Don't: Platform Code Doing Uploads
```markdown
Android MainActivity calls S3 directly    ← BAD
Android → ShareImportService → TaskManager ← GOOD
```

### Don't: Duplicate TaskSettings Logic
```markdown
Create new settings cloning for mobile    ← BAD
Reuse existing TaskSettings.Clone()       ← GOOD
```

### Don't: UI in Core
```markdown
Show MessageBox from ShareImportService   ← BAD
Return results, let caller display        ← GOOD
```

---

## File Locations

- **Active XIPs**: `tasks/1-new/XIPXXXX_Descriptive_Name.md`
- **In Progress**: Move to `tasks/2-in-progress/`
- **Completed**: Move to `tasks/3-done/`
- **GitHub Issue**: Link to `https://github.com/ShareX/XerahS/issues/XXX`

---

## Quick Reference: XIP Template

```markdown
# XIP0001 Title

**Status**: Draft  
**Created**: YYYY-MM-DD  
**Area**: Area  
**Goal**: One sentence.

---

## Overview

Problem and approach.

## Prerequisites

- List

## Implementation Phases

### Phase 1: Name

Description.

**Key Files:**
- Path

**Code:**
```csharp
// Example
```

**Rules:**
- Do X
- Don't Y

## Non-Negotiable Rules

1. Rule one
2. Rule two

## Deliverables

1. Item
2. Item

## Affected Components

- Project: Changes

## Architecture Summary

```
Diagram
```
```

---

## Key Takeaways

1. **Study first** - Understand existing patterns before writing
2. **Reuse always** - No duplicate upload pipelines, no duplicate settings logic
3. **Separate concerns** - Platform code handles platform things, Core handles logic
4. **Be explicit** - Non-negotiable rules, clear deliverables, concrete file paths
5. **Privacy first** - Prefer explicit user action over automatic monitoring
6. **Stay in sync** - XIP and GitHub issue must match

---

**Remember**: A good XIP is precise, reusable, and respects existing architecture.
