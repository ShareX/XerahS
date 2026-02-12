---
id: XIP0028
title: Refactor WorkerTask and Consolidate Tool Workflows
status: Complete
created: 2026-02-06
author: Claude (AI Assistant)
---

## Overview

Refactor the XerahS codebase to:

1. Split the monolithic `WorkerTask.cs` (1,187 lines) into focused partial classes
2. Move ColorPicker and QRCode workflow handling from `App.axaml.cs` into `WorkerTask`
3. Unify all workflow execution through the TaskManager → WorkerTask pipeline

## Current State (Completed)

- ✅ `WorkerTask.cs` is now 936 lines (was 1,187) - reduced by ~20%
- ✅ WorkerTask is now `partial class` with domain-specific files
- ✅ ColorPicker and QRCode workflows route through `HandleToolWorkflowAsync` in WorkerTaskTools.cs
- ✅ All workflows go through TaskManager → WorkerTask pipeline
- ✅ Consistent "TOOL_WORKFLOW" logging category for tool operations

## Target State (Achieved)

- ✅ All workflows route through TaskManager → WorkerTask
- ✅ Tool workflows handled in partial class `WorkerTaskTools.cs`
- ✅ `App.axaml.cs` uses `HandleToolWorkflowCallback` - no special-case handling
- ✅ WorkerTask split into focused partial classes by domain

---

## Implementation Steps

### Step 1: Make WorkerTask Partial

**File:** `src/XerahS.Core/Tasks/WorkerTask.cs`

Change the class declaration from `public class WorkerTask` to `public partial class WorkerTask`

---

### Step 2: Create Split Files

Create in `src/XerahS.Core/Tasks/`:

**WorkerTaskCapture.cs** - Screen capture logic
**WorkerTaskRecording.cs** - Screen recording logic  
**WorkerTaskUpload.cs** - Upload logic
**WorkerTaskTools.cs** - Tool workflows (NEW)

---

### Step 3: Add Tool Workflow Cases

Add to WorkerTask.cs switch statement after `AbortScreenRecording`:
- ColorPicker, ScreenColorPicker, QRCode, QRCodeDecodeFromScreen, QRCodeScanRegion
- Route to `await HandleToolWorkflowAsync(token);`

---

### Step 4: Remove Special Handling from App.axaml.cs

Remove the `isColorPickerJob` and `isQrJob` special handling blocks from `HotkeyManager_HotkeyTriggered`

---

## Success Criteria

- [x] WorkerTask.cs is 936 lines (was 1,187) - core orchestration only
- [x] ColorPicker/QRCode use "TOOL_WORKFLOW" log category (via WorkerTaskTools.cs)
- [x] App.axaml.cs has no ColorPicker/QRCode special cases - uses HandleToolWorkflowCallback
- [x] All workflows execute through TaskManager → WorkerTask

## File Structure After Refactor

```
src/XerahS.Core/Tasks/
├── WorkerTask.cs              (core orchestration)
├── WorkerTaskCapture.cs       (screen capture workflows)
├── WorkerTaskRecording.cs     (screen recording workflows)
├── WorkerTaskUpload.cs        (upload workflows)
└── WorkerTaskTools.cs         (tool workflows - NEW)
```

## Related Files

- `src/XerahS.Core/Tasks/WorkerTask.cs`
- `src/XerahS.Core/Tasks/WorkerTaskCapture.cs` (to create)
- `src/XerahS.Core/Tasks/WorkerTaskRecording.cs` (to create)
- `src/XerahS.Core/Tasks/WorkerTaskUpload.cs` (to create)
- `src/XerahS.Core/Tasks/WorkerTaskTools.cs` (to create)
- `src/XerahS.UI/App.axaml.cs` (remove special handling)
- `src/XerahS.UI/Services/ColorPickerToolService.cs` (keep, call from WorkerTask)
- `src/XerahS.UI/Services/QrCodeToolService.cs` (keep, call from WorkerTask)

## Dependencies

- ColorPickerToolService
- QrCodeToolService
- TroubleshootingHelper (for logging)
- PlatformServices.Toast (for not-implemented notifications)

## Notes

- Keep ColorPickerToolService and QrCodeToolService in UI layer
- WorkerTaskTools.cs calls these services via Dispatcher.UIThread.InvokeAsync
- This maintains separation: Core (WorkerTask) orchestrates, UI (Services) implements UI
