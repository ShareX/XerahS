# SIP0014: Editor Decoupling

## Priority
**HIGH** - Architecture Refactoring

## Assignee
**Codex**

## Branch
`feature/editor-decoupling` (merged)

## Status
Complete - Verified on 2026-01-08

## Assessment
100% Complete. `ShareX.Editor` DLL is created and decoupled. This satisfies the requirement for a reusable editor component.

## Objective
Decouple the Editor functionality into a standalone `ShareX.Editor` DLL that can be consumed by both XerahS and potentially other projects.

## Scope
1.  **Extract Editor Logic**: Move all editor-related code (annotations, tools, canvas) to a new project `ShareX.Editor`.
2.  **Remove Dependencies**: Ensure `ShareX.Editor` does not depend on `XerahS` specific infrastructure where possible.
3.  **Refactor References**: Update `XerahS` to reference the new DLL.

## Deliverables
- ✅ `ShareX.Editor` project created
- ✅ Code moved and refactored
- ✅ `XerahS` builds successfully with new reference
