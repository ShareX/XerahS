# Implementation Status & Checklists (Snapshot Jan 2025)

### Architecture Status
- **Common/Core/Uploaders**: Porting in progress.
- **Uploader Plugin System**:
  - ✅ Multi-Instance Provider Catalog (Dec 2024)
  - ✅ Dynamic Plugin System (Jan 2025) - Pure dynamic loading, isolation.
  - ⏳ File-Type Routing (Planned) - See `FILETYPE_ROUTING_SPEC.md`.
  - ⏳ Full Automation Workflow (Path B) - See `automation_workflow_plan.md`.

### Annotation Subsystem
- ✅ **Phase 1**: Core Models (Dec 2024) - `XerahS.Annotations` created.
- ⏳ **Phase 2**: Canvas Control (~6-8h) - Replace WinForms/GDI+ with Avalonia/Skia.

### Native & ARM64
- **Goal**: Native support for Windows ARM64; portable to Linux/Mac ARM64.
- **Tasks**:
  - Add `win-arm64` to build matrix.
  - Audit P/Invoke for pointer sizes (`nint`).
  - Provide ARM64 FFmpeg builds.

### Pending Backend Tasks (Highlights)
*See full gap report in previous docs for exhaustive list.*
- **ShareX.HelpersLib**: Many utilities ported (`FileDownloader`, `Encryption`, `Helpers`).
  - *Pending*: `BlackStyle*` controls, `ColorPicker` UI, `PrintHelper`.
- **ShareX.ScreenCaptureLib**:
  - *Pending*: Shapes, Tools (Crop, CutOut, Spotlight), Region Capture UI.
