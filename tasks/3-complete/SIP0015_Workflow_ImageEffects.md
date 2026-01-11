# SIP0015: Workflow Configuration - Image Effects

## Priority
**HIGH** - Feature Parity

## Assignee
**Codex**

## Branch
`feature/workflow-image-effects`

## Status
Complete - Verified on 2026-01-08

## Assessment
100% Complete. `WorkflowsConfig.json` implemented, `ImageEffectPreset` integrated, setting manager updated.

## Objective
Finalize workflow configuration to support Image Effects and rename `HotkeysConfig.json` to `WorkflowsConfig.json` to reflect its broader purpose.

## Scope
1.  **Rename Config**: Rename `HotkeysConfig.json` to `WorkflowsConfig.json`.
2.  **Image Effects**: Add `ImageEffects` support to `TaskSettings`.
3.  **Serialization**: Ensure all workflow components are correctly serialized.

## Deliverables
- ✅ `WorkflowsConfig.json` usage implemented
- ✅ Image Effects supported in workflows
- ✅ Build verification successful
