# WorkflowType Implementation Audit

**Date:** February 6, 2026  
**Version:** 0.8.2  
**Auditor:** Claude (AI Assistant)

---

## Executive Summary

| Metric | Count |
|--------|-------|
| **Total WorkflowType Definitions** | 73 |
| **Implemented in WorkerTask.cs** | 27 |
| **Implemented via Tool Services in App.axaml.cs** | 7 (ColorPicker x2, QRCode x3, ScrollingCapture x1, OCR x1) |
| **Implemented in TrayIconHelper.cs** | 1 (`OpenMainWindow`) |
| **NOT WIRED (Stub/Placeholder)** | **37** |

---

## Implementation Patterns

### Pattern 1: WorkerTask.cs (Primary)
The main execution engine handling capture, upload, and recording workflows.

**File:** `src/XerahS.Core/Tasks/WorkerTask.cs`

### Pattern 2: Tool Services via App.axaml.cs
Standalone tools that bypass WorkerTask and are handled directly in the application layer.

**File:** `src/XerahS.UI/App.axaml.cs`

- `ColorPickerToolService.HandleWorkflowAsync()` - Color Picker, Screen Color Picker
- `QrCodeToolService.HandleWorkflowAsync()` - QR Code, Scan from Screen, Scan Region
- `OcrToolService.HandleWorkflowAsync()` - OCR Text Recognition

### Pattern 3: Tray Icon Helper
Tray-specific actions handled separately.

**File:** `src/XerahS.UI/TrayIconHelper.cs`

- `OpenMainWindow`

---

## Detailed Breakdown by Category

### Upload (2 of 8 wired)

| WorkflowType | Status | Location | Notes |
|--------------|--------|----------|-------|
| `FileUpload` | ✅ Wired | WorkerTask.cs | Full implementation with dialog callback |
| `FolderUpload` | ❌ Not Wired | � | Stub only |
| `ClipboardUpload` | ✅ Wired | WorkerTask.cs | Full implementation with image/text/file support |
| `ClipboardUploadWithContentViewer` | ✅ Wired | WorkerTask.cs | Same as ClipboardUpload |
| `UploadText` | ❌ Not Wired | � | Stub only |
| `UploadURL` | ❌ Not Wired | � | Stub only |
| `DragDropUpload` | ❌ Not Wired | � | Stub only |
| `ShortenURL` | ❌ Not Wired | � | Stub only |
| `StopUploads` | ❌ Not Wired | � | Stub only |
| `IndexFolder` | ✅ Wired | WorkerTask.cs | Full implementation with HTML/TXT/XML/JSON output |

---

### Screen Capture (9 of 12 wired)

| WorkflowType | Status | Location | Notes |
|--------------|--------|----------|-------|
| `PrintScreen` | ✅ Wired | WorkerTask.cs | Fullscreen capture |
| `ActiveWindow` | ✅ Wired | WorkerTask.cs | Active window capture |
| `CustomWindow` | ✅ Wired | WorkerTask.cs | Window selector integration |
| `ActiveMonitor` | ✅ Wired | WorkerTask.cs | Captures active monitor via `GetActiveScreenBounds()` |
| `RectangleRegion` | ✅ Wired | WorkerTask.cs | Region selection with frozen background |
| `RectangleLight` | ❌ Not Wired | � | Stub only |
| `RectangleTransparent` | ✅ Wired | WorkerTask.cs | Region selection with transparent overlay |
| `CustomRegion` | ✅ Wired | WorkerTask.cs | Captures pre-configured `CaptureCustomRegion` rect |
| `LastRegion` | ✅ Wired | WorkerTask.cs | Re-captures last used region rect |
| `ScrollingCapture` | ✅ Wired | App.axaml.cs | Via `ScrollingCaptureToolService` |
| `AutoCapture` | ❌ Not Wired | � | Stub only |
| `StartAutoCapture` | ❌ Not Wired | � | Stub only |
| `StopAutoCapture` | ❌ Not Wired | � | Stub only |

---

### Screen Record (11 of 11 wired) - COMPLETE

| WorkflowType | Status | Location | Notes |
|--------------|--------|----------|-------|
| `ScreenRecorder` | ✅ Wired | WorkerTask.cs | Region selector + MP4 recording |
| `StartScreenRecorder` | ✅ Wired | WorkerTask.cs | Uses last region |
| `ScreenRecorderActiveWindow` | ✅ Wired | WorkerTask.cs | Active window recording |
| `ScreenRecorderCustomRegion` | ✅ Wired | WorkerTask.cs | Pre-configured region |
| `ScreenRecorderGIF` | ✅ Wired | WorkerTask.cs | Region selector + GIF recording |
| `StartScreenRecorderGIF` | ✅ Wired | WorkerTask.cs | Uses last region + GIF |
| `ScreenRecorderGIFActiveWindow` | ✅ Wired | WorkerTask.cs | Active window GIF |
| `ScreenRecorderGIFCustomRegion` | ✅ Wired | WorkerTask.cs | Custom region GIF |
| `StopScreenRecording` | ✅ Wired | WorkerTask.cs | Stop signal handler |
| `PauseScreenRecording` | ✅ Wired | WorkerTask.cs | Pause/resume toggle |
| `AbortScreenRecording` | ✅ Wired | WorkerTask.cs | Abort handler |

**Status:** ✅ All screen recording workflows are fully implemented!

---

### Tools (7 of 31 wired)

| WorkflowType | Status | Location | Notes |
|--------------|--------|----------|-------|
| `ColorPicker` | ✅ Wired | App.axaml.cs | Via `ColorPickerToolService` |
| `ScreenColorPicker` | ✅ Wired | App.axaml.cs | Via `ColorPickerToolService` |
| `Ruler` | ❌ Not Wired | � | � |
| `PinToScreen` | ❌ Not Wired | � | � |
| `PinToScreenFromScreen` | ❌ Not Wired | � | � |
| `PinToScreenFromClipboard` | ❌ Not Wired | � | � |
| `PinToScreenFromFile` | ❌ Not Wired | � | � |
| `PinToScreenCloseAll` | ❌ Not Wired | � | � |
| `ImageEditor` | ✅ Wired | App.axaml.cs | Opens file picker → loads image → `ShowEditorAsync()` |
| `ImageBeautifier` | ❌ Not Wired | � | � |
| `ImageEffects` | ❌ Not Wired | � | � |
| `ImageViewer` | ❌ Not Wired | � | � |
| `ImageCombiner` | ❌ Not Wired | � | � |
| `ImageSplitter` | ❌ Not Wired | � | � |
| `ImageThumbnailer` | ❌ Not Wired | � | � |
| `VideoConverter` | ❌ Not Wired | � | � |
| `VideoThumbnailer` | ❌ Not Wired | � | � |
| `AnalyzeImage` | ❌ Not Wired | � | � |
| **`OCR`** | ✅ Wired | App.axaml.cs | Via `OcrToolService` — Windows.Media.Ocr native, stubs for Linux/macOS |
| `QRCode` | ✅ Wired | App.axaml.cs | Via `QrCodeToolService` |
| `QRCodeDecodeFromScreen` | ✅ Wired | App.axaml.cs | Via `QrCodeToolService` |
| `QRCodeScanRegion` | ✅ Wired | App.axaml.cs | Via `QrCodeToolService` |
| `HashCheck` | ❌ Not Wired | � | � |
| `Metadata` | ❌ Not Wired | � | � |
| `StripMetadata` | ❌ Not Wired | � | � |
| `ClipboardViewer` | ❌ Not Wired | � | � |
| `BorderlessWindow` | ❌ Not Wired | � | � |
| `ActiveWindowBorderless` | ❌ Not Wired | � | � |
| `ActiveWindowTopMost` | ❌ Not Wired | � | � |
| `InspectWindow` | ❌ Not Wired | � | � |
| `MonitorTest` | ❌ Not Wired | � | � |

---

### Other (6 of 8 wired)

| WorkflowType | Status | Location | Notes |
|--------------|--------|----------|-------|
| `DisableHotkeys` | ✅ Wired | WorkerTask.cs | Toggles hotkeys via `ToggleHotkeysCallback` |
| `OpenMainWindow` | ✅ Wired | TrayIconHelper.cs | Tray double-click action |
| `OpenScreenshotsFolder` | ✅ Wired | WorkerTask.cs | Opens screenshots folder in file explorer |
| `OpenHistory` | ✅ Wired | WorkerTask.cs | Via `OpenHistoryCallback` |
| `OpenImageHistory` | ✅ Wired | WorkerTask.cs | Via `OpenHistoryCallback` (shared with OpenHistory) |
| `ToggleActionsToolbar` | ❌ Not Wired | � | � |
| `ToggleTrayMenu` | ❌ Not Wired | � | � |
| `ExitShareX` | ✅ Wired | WorkerTask.cs | Via `ExitApplicationCallback` → `Shutdown()` |

---

## Architectural Concerns

### 1. Fragmented Implementation
Tool workflows (ColorPicker, QRCode) are currently handled in `App.axaml.cs` rather than `WorkerTask.cs`. This creates inconsistency:
- Some workflows go through `TaskManager` ? `WorkerTask`
- Others bypass the task system entirely

**Recommendation:** Consolidate all workflow execution into `WorkerTask` or a unified handler.

### 2. WorkerTask.cs Size
`WorkerTask.cs` is currently ~850 lines and growing. Consider splitting into partial classes:
- `WorkerTask.Capture.cs` - Screen capture workflows
- `WorkerTask.Recording.cs` - Screen recording workflows
- `WorkerTask.Upload.cs` - File/clipboard upload workflows
- `WorkerTask.Tools.cs` - Tool workflows (future)

### 3. Missing End-to-End Flow
Many workflows have enum definitions but no execution path:
- No `case` statement in any switch
- No service handler
- No UI integration

---

## Priority Recommendations

### High Priority (Core Functionality)
1. ~~**`OCR`** - Optical Character Recognition~~ ✅ Done (v0.8.2) — Windows.Media.Ocr native
2. ~~**`ImageEditor`** - Basic image editing capabilities~~ ✅ Done (v0.8.2) — reuses existing EditorWindow
3. ~~**`ScrollingCapture`** - Long page capture~~ ✅ Done (v0.8.2)
4. **`UploadText`** - Text upload workflow

### Medium Priority (Utility)
5. **`ImageCombiner`** - Merge multiple images
6. **`ImageThumbnailer`** - Generate thumbnails
7. **`HashCheck`** - File integrity verification
8. ~~**`OpenHistory`** - Quick history access~~ ✅ Done (v0.8.2)

### Low Priority (Nice to Have)
9. Pin-to-screen variants
10. Borderless window tools
11. Monitor test patterns

---

## Implementation Checklist Template

When implementing a new workflow, ensure:

- [ ] Enum value exists in `WorkflowType`
- [ ] `case` statement in `WorkerTask.DoWorkAsync()` (or service handler)
- [ ] Media type mapping in `TaskHelpers.cs`
- [ ] Capture workflow flag in `TaskHelpers.IsCaptureWorkflow()` (if applicable)
- [ ] CLI command support (if applicable)
- [ ] Hotkey configuration UI support
- [ ] Troubleshooting logging integration

---

## Related Files

| File | Purpose |
|------|---------|
| `src/XerahS.Core/Enums.cs` | WorkflowType definitions |
| `src/XerahS.Core/Tasks/WorkerTask.cs` | Main execution engine |
| `src/XerahS.Core/Helpers/TaskHelpers.cs` | Media type helpers |
| `src/XerahS.Core/Helpers/TaskHelpers.ExecuteJob.cs` | Job execution entry |
| `src/XerahS.UI/App.axaml.cs` | Tool service routing |
| `src/XerahS.UI/TrayIconHelper.cs` | Tray action handling |
| `src/XerahS.UI/Services/ColorPickerToolService.cs` | Color picker implementation |
| `src/XerahS.UI/Services/QrCodeToolService.cs` | QR code implementation |
| `src/XerahS.UI/Services/OcrToolService.cs` | OCR tool implementation |
| `src/XerahS.Platform.Abstractions/IOcrService.cs` | OCR platform abstraction |
| `src/XerahS.Platform.Windows/WindowsOcrService.cs` | Windows OCR (Windows.Media.Ocr) |

---

*This audit was generated automatically. Please update as implementations are added.*
