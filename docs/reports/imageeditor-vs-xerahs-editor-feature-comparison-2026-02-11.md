# Feature Comparison: `XerahS.Editor` (`develop`) vs `ImageEditor` (current branch)

## Scope
- Compared `XerahS.Editor` from temporary `develop` worktree against `ImageEditor` from current root branch `experimental/ShareX.ImageEditor`.
- `develop` editor revision: `74f8f8aacfe01013c04e9a06f60f49c4ce634940` (`XerahS.Editor`).
- current editor revision: `2af5b2b4af0af8ab2f872e39011dcf30e6b35c08` (`ImageEditor`), with local modifications: `M src/ShareX.ImageEditor/UI/ViewModels/MainViewModel.cs`.
- Comparison method: symbol inventory, relay-command/event surface diff, dialog/control inventory diff, and targeted manual code review.

## Executive Summary
- Core editing capabilities (annotations, effects, crop/cutout, undo/redo, rendering) are largely equivalent; most differences are architectural packaging and host-integration surface.
- `ImageEditor` current branch does not include several host-facing features that exist in `XerahS.Editor` on `develop` (preset save/load pipeline, open-image replacement flow, navigation/close hooks, zoom-to-fit hook, and two dialogs).
- `ImageEditor` current branch adds modularization and adapter contracts not present in `develop` `XerahS.Editor` (Core/UI split, toolbar adapter abstraction, per-annotation visual adapter files, tests in solution).

## What `ImageEditor` Current Branch Lacks (vs `XerahS.Editor` on `develop`)
1. In-editor preset save/load and legacy preset migration pipeline is missing in the editor library surface.
Evidence: `XerahS.Editor/src/XerahS.Editor/Helpers/ImageEffectPresetSerializer.cs:34`, `XerahS.Editor/src/XerahS.Editor/Helpers/LegacyImageEffectExporter.cs:37`, `XerahS.Editor/src/XerahS.Editor/Helpers/LegacyImageEffectImporter.cs:36`, plus commands `SavePreset`/`LoadPreset` in `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1585` and `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1635`.
2. Open-image replace/add flow with explicit choice dialog is missing.
Evidence: `OpenImage` command in `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:769` opens `XerahS.Editor/src/XerahS.Editor/Views/Dialogs/OpenImageChoiceDialog.axaml.cs:34`; the dialog type is absent in current `ImageEditor` dialog inventory.
3. Generic message modal dialog component is missing.
Evidence: `XerahS.Editor/src/XerahS.Editor/Views/Dialogs/MessageDialog.axaml.cs:32`; absent from current `ImageEditor/src/ShareX.ImageEditor/UI/Views/Dialogs`.
4. MainViewModel host wiring commands/events are reduced (navigation, close, zoom-to-fit hook, open-image event, add-image-annotation event).
Evidence: `Navigate` in `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1296`, `Close` in `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:863`, `ZoomToFit` and event in `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1402`, `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1405`, and extra events listed at `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:76`, `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:756`, `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:765`; no corresponding symbols in current MainViewModel search.
5. Dedicated view controls `AnnotationToolbar` and `EditorToolsPanel` are not present in current editor submodule.
Evidence: `XerahS.Editor/src/XerahS.Editor/Views/Controls/AnnotationToolbar.axaml.cs:39`, `XerahS.Editor/src/XerahS.Editor/Views/Controls/EditorToolsPanel.axaml.cs:37`; current `ImageEditor/src/ShareX.ImageEditor/UI/Views/Controls` has no `.axaml.cs` control wrappers for those.

## What `ImageEditor` Current Branch Has Exclusively (vs `XerahS.Editor` on `develop`)
1. Explicit Core/UI layering with adapter boundaries (instead of monolithic flat project structure).
Evidence: `ImageEditor/src/ShareX.ImageEditor/Core/*` and `ImageEditor/src/ShareX.ImageEditor/UI/*` trees, including `ImageEditor/src/ShareX.ImageEditor/Core/Abstractions/IAnnotationToolbarAdapter.cs:34` and `ImageEditor/src/ShareX.ImageEditor/UI/Adapters/EditorToolbarAdapter.cs:38`.
2. Per-annotation visual adapter partials split out under `UI/Adapters/AnnotationVisuals` (13 files).
Evidence: `ImageEditor/src/ShareX.ImageEditor/UI/Adapters/AnnotationVisuals/RectangleAnnotation.Visual.cs:31` (representative) and directory inventory count = 13.
3. Test project is part of solution structure in current editor repo.
Evidence: `ImageEditor/ShareX.ImageEditor.sln:10` and `ImageEditor/ShareX.ImageEditor.sln:12`; develop solution has only loader + core at `XerahS.Editor/XerahS.Editor.sln:6` and `XerahS.Editor/XerahS.Editor.sln:8`.
4. Conditional target framework for Windows/non-Windows in current editor project file.
Evidence: `ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj:3`, `ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj:4`; develop uses single `net10.0` target in `XerahS.Editor/src/XerahS.Editor/XerahS.Editor.csproj:3`.
5. Current branch local patch adds bitmap-lifecycle hardening not found in `develop` editor.
Evidence: `ImageEditor/src/ShareX.ImageEditor/UI/ViewModels/MainViewModel.cs:1399` (`IsBitmapAlive`), `ImageEditor/src/ShareX.ImageEditor/UI/ViewModels/MainViewModel.cs:1406` (`SafeCopyBitmap`), `ImageEditor/src/ShareX.ImageEditor/UI/ViewModels/MainViewModel.cs:1424` (`GetBestAvailableSourceBitmap`), and guarded preview entry at `ImageEditor/src/ShareX.ImageEditor/UI/ViewModels/MainViewModel.cs:1619`.

## Command Surface Diff (`MainViewModel` Relay Commands)
- `develop` command count: 43
- current command count: 35
- Missing in current:
  - `Close`
  - `LoadPreset`
  - `Navigate`
  - `OpenImage`
  - `QuickSave`
  - `SavePreset`
  - `SetZoom`
  - `ZoomToFit`
- Exclusive in current:
  - (none)

## Event Surface Diff (`MainViewModel`)
- Missing in current:
  - `AddImageAnnotationRequested`
  - `CloseRequested`
  - `NavigateRequested`
  - `ZoomToFitRequested`
- Exclusive in current:
  - (none)

## Dialog Inventory Diff
- `develop` dialog code-behind count: 30
- current dialog code-behind count: 28
- Missing in current:
  - `MessageDialog.axaml.cs`
  - `OpenImageChoiceDialog.axaml.cs`

## Type Inventory Diff (name-level)
- `develop` unique type names: 152
- current unique type names: 135
- Missing in current: 19
- Exclusive in current: 2

### Missing Type Names in Current
- `AnnotationGeometryHelper` — `Helpers\AnnotationGeometryHelper.cs`
- `AnnotationToolbar` — `Views\Controls\AnnotationToolbar.axaml.cs`
- `App` — `App.axaml.cs`
- `EditorToolsPanel` — `Views\Controls\EditorToolsPanel.axaml.cs`
- `EffectMapping` — `Helpers\LegacyImageEffectImporter.cs`
- `FuncImageEffect` — `EditorCore.cs`
- `ImageEffectPresetSerializer` — `Helpers\ImageEffectPresetSerializer.cs`
- `ImageEffectSerializationBinder` — `Helpers\ImageEffectPresetSerializer.cs`
- `LegacyEffectMapping` — `Helpers\LegacyImageEffectExporter.cs`
- `LegacyImageEffectExporter` — `Helpers\LegacyImageEffectExporter.cs`
- `LegacyImageEffectImporter` — `Helpers\LegacyImageEffectImporter.cs`
- `LegacyPresetExportResult` — `Helpers\LegacyImageEffectExporter.cs`
- `LegacyPresetImportResult` — `Helpers\LegacyImageEffectImporter.cs`
- `MappedEffect` — `Helpers\LegacyImageEffectImporter.cs`
- `MessageDialog` — `Views\Dialogs\MessageDialog.axaml.cs`
- `OpenImageChoiceDialog` — `Views\Dialogs\OpenImageChoiceDialog.axaml.cs`
- `SkColorJsonConverter` — `Helpers\ImageEffectPresetSerializer.cs`
- `SpeechBalloonTailEdge` — `Helpers\AnnotationGeometryHelper.cs`
- `XsiePreset` — `Helpers\ImageEffectPresetSerializer.cs`

### Exclusive Type Names in Current
- `EditorToolbarAdapter` — `UI\Adapters\EditorToolbarAdapter.cs`
- `IAnnotationToolbarAdapter` — `Core\Abstractions\IAnnotationToolbarAdapter.cs`

