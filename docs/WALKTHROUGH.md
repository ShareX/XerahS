# XerahS Development Walkthrough

## Snapshot (Updated 2026-02-23)

- Desktop capture workflow (capture -> annotate/edit -> save/copy/upload) is implemented.
- Editor integration is now based on the `ShareX.ImageEditor` submodule.
- Release automation builds Windows, Linux (x64/arm64), and macOS (x64/arm64) packages.
- Current work is mostly stabilization, parity testing, and macOS hardening rather than missing core features.

## Build Guardrails

- `Directory.Build.props` enforces `TreatWarningsAsErrors=true`.
- Desktop projects use `net11.0-windows10.0.26100.0` for Windows targets and `net11.0` for cross-platform builds.
- `SkiaSharp` is centrally managed from `Directory.Packages.props` and currently resolves to `3.119.3-preview.1.1`.

## Repository Map

- `src/desktop/app/XerahS.App`:
  process entrypoint, single-instance guard, platform bootstrap, async background initialization.
- `src/desktop/app/XerahS.UI`:
  Avalonia shell, tray integration, workflow orchestration, editor window hosting.
- `src/desktop/app/XerahS.RegionCapture`:
  region overlay windows, per-monitor interaction, annotation overlay wiring.
- `src/desktop/core/XerahS.Core`:
  task/job processing, capture/upload pipeline, settings, helpers.
- `src/desktop/core/XerahS.Uploaders`:
  provider catalog, dynamic plugin loading, instance management, file-type routing.
- `src/platform`:
  platform service implementations for Windows, Linux, and macOS.
- `ShareX.ImageEditor/src/ShareX.ImageEditor`:
  upstream editor submodule used by UI/Core/RegionCapture.
- `tests/XerahS.Tests`:
  test coverage for core helpers, uploader config, hotkeys, Linux capture orchestration, coordinate transforms.

## Runtime Walkthrough

### 1. Startup and Platform Initialization

1. `src/desktop/app/XerahS.App/Program.cs` starts the app and enforces single-instance behavior.
2. Settings are loaded before platform bootstrap so runtime flags are available.
3. Platform services are initialized by OS branch (`WindowsPlatform`, `LinuxPlatform`, `MacOSPlatform`).
4. UI is launched through Avalonia; background initialization then loads plugins and recording services asynchronously.

### 2. UI Bootstrapping and Global Services

1. `src/desktop/app/XerahS.UI/App.axaml.cs` initializes theme synchronization, tray controller, and workflow orchestration.
2. The editor clipboard adapter is registered so `ShareX.ImageEditor` clipboard operations route through XerahS services.
3. Theme sync keeps XerahS and `ShareX.ImageEditor` theme state aligned (`src/desktop/app/XerahS.UI/Services/ThemeService.cs`).

### 3. Workflow and Hotkey Dispatch

1. `src/desktop/app/XerahS.UI/Services/WorkflowOrchestrator.cs` wires `WorkerTask` callbacks and global hotkeys.
2. Tool workflows are routed through `src/desktop/app/XerahS.UI/Services/ToolWorkflowDispatcher.cs`.
3. Hotkey-triggered execution eventually flows into the core task pipeline (`TaskHelpers.ExecuteWorkflow` and task processors).

### 4. Region Capture and Annotation Overlay

1. `src/desktop/app/XerahS.RegionCapture/RegionCaptureService.cs` starts per-monitor overlays through `OverlayManager`.
2. `src/desktop/app/XerahS.RegionCapture/UI/OverlayWindow.axaml.cs` handles selection, keyboard shortcuts, and annotation interaction.
3. The overlay uses `RegionCaptureAnnotationViewModel`, which owns a `ShareX.ImageEditor.EditorCore` instance.
4. Annotation output is composited into the final capture result when confirming annotated selections.
5. A local compatibility toolbar is retained at `src/desktop/app/XerahS.RegionCapture/UI/Controls/AnnotationToolbar.cs` because upstream does not provide that exact control surface.

### 5. Editor Integration

1. `ShareX.ImageEditor` is referenced by UI/Core/RegionCapture projects (`*.csproj` project references).
2. Legacy preset type names are remapped for compatibility in `src/desktop/core/XerahS.Core/Helpers/ImageEffectPresetSerializer.cs`.
3. The host app opens editor windows/tooling through UI services and view models that consume `ShareX.ImageEditor` APIs.

### 6. Capture Output and Upload Pipeline

1. `src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs` executes after-capture tasks:
   save, copy to clipboard, upload to host.
2. Upload resolution and fallback behavior are handled in both:
   `src/desktop/core/XerahS.Core/Tasks/Processors/CaptureJobProcessor.cs` and
   `src/desktop/core/XerahS.Core/Tasks/Processors/UploadJobProcessor.cs`.
3. Provider discovery/loading lives in `src/desktop/core/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`.
4. Configured destination instances, defaults, and routing are managed by `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`.
5. File-type routing is implemented via `FileTypeScope` on `UploaderInstance` with conflict validation and blocked-type reporting.

### 7. Packaging and Release

1. Main release workflow:
   `.github/workflows/release-build-all-platforms.yml`.
2. Platform packaging scripts:
   `build/windows/package-windows.ps1`,
   `build/linux/package-linux.sh`,
   `build/macos/package-mac.sh`.
3. Packaging includes plugin publish/copy steps so shipped builds include uploader providers.

## Test Coverage Notes

- Active tests include coordinate transforms, Linux capture orchestration, Linux hotkey mapping, uploader/config helpers, custom uploader repository, and workflow/hotkey behaviors.
- `tests/XerahS.Tests/XerahS.Tests.csproj` currently excludes:
  `Editor/EditorHistoryEffectsTests.cs` and `Editor/EditorRotateAnnotationsTests.cs`
  because those legacy tests target APIs from the prior in-repo editor implementation.

## Current Gaps

- macOS still has MVP/stub areas (for example `ScreenCaptureKitStrategy`, OCR, and parts of window services) that require on-device hardening.
- End-to-end verification matrices for annotation/effects/native-app interoperability remain to be completed.
- URL shortener after-upload automation is still marked not implemented in `UploadJobProcessor`.

## Related Docs

- `docs/ROADMAP.md`
- `docs/CHANGELOG.md`
- `docs/PROJECT_STATUS.md`
