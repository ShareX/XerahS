# ImageEditor Integration Findings (2026-02-11)

## Scope
Analyze how to replace the current editor dependency with `https://github.com/ShareX/ImageEditor.git` as a submodule and identify where the branch will break, without implementing code changes.

## Executive Summary
The migration is feasible, but a direct swap will fail immediately due to:
- broken/fragile project reference paths,
- namespace and assembly rename breadth (`XerahS.Editor` -> `ShareX.ImageEditor`),
- API drift between current in-branch editor and upstream `ImageEditor`,
- serialization type-name compatibility risks for existing settings/presets.

## Current State Snapshot
- Current submodule: `XerahS.Editor` (`.gitmodules`, branch `integrate/feature-jx-018-025-manual`).
- Current editor references exist in:
  - `src/XerahS.UI/XerahS.UI.csproj`
  - `src/XerahS.Core/XerahS.Core.csproj`
  - `src/XerahS.RegionCapture/XerahS.RegionCapture.csproj`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
- Solution includes editor project entry:
  - `XerahS.sln` -> `XerahS.Editor/src/XerahS.Editor/XerahS.Editor.csproj`

## Key Findings

### 1. Project Wiring Is Already Inconsistent
The current `ProjectReference` paths point to a sibling repo pattern (`..\..\..\XerahS.Editor...`) instead of reliably resolving to the in-repo submodule folder in all environments. This is already causing build failures on this branch.

### 2. Namespace and Assembly Migration Is Broad
Code and AXAML use `XerahS.Editor` extensively across `src` and `tests`.
- C# `using` references include:
  - `XerahS.Editor`
  - `XerahS.Editor.Annotations`
  - `XerahS.Editor.ViewModels`
  - `XerahS.Editor.ImageEffects.*`
- AXAML assembly references include:
  - `assembly=XerahS.Editor` in app and view markup.

Upstream repo uses `ShareX.ImageEditor` namespaces/assembly.

### 3. API Drift Exists Beyond Namespace Rename
The current in-branch editor fork has custom surface area not present upstream:
- Missing upstream control:
  - `AnnotationToolbar` (required by region capture overlay).
- Missing/changed editor APIs:
  - `EditorView.ShowMenuBar` is used by host UI but not available upstream.
  - `EditorCore` effect-list APIs used by `ImageEffectsViewModel` are not available upstream (`LoadEffects`, `SetEffects`, `AddEffect`, `RemoveEffect`, `ToggleEffect`, `EffectsChanged`, `Effects` collection workflow).
- Enum mismatches:
  - Branch uses `EditorTool.Pen`, `EditorTool.Highlighter`, `EditorTool.Number`.
  - Upstream uses `EditorTool.Freehand`, `EditorTool.Highlight`, `EditorTool.Step`.

### 4. Serialization Compatibility Risk
Effect type binding currently enforces `XerahS.Editor.ImageEffects.*` prefixes.
- Example: `src/XerahS.Core/Helpers/ImageEffectPresetSerializer.cs`
- Settings serialization also uses type metadata (`TypeNameHandling.Auto`) in common settings loaders.
Without migration support, previously saved settings/presets referencing old type names can fail to deserialize after swap.

## Breakage Hotspots (High Priority)
- `src/XerahS.RegionCapture/UI/OverlayWindow.axaml`
- `src/XerahS.RegionCapture/UI/OverlayWindow.axaml.cs`
- `src/XerahS.RegionCapture/ViewModels/RegionCaptureAnnotationViewModel.cs`
- `src/XerahS.UI/Views/MainWindow.axaml.cs`
- `src/XerahS.UI/ViewModels/ImageEffectsViewModel.cs`
- `src/XerahS.Core/Helpers/ImageEffectPresetSerializer.cs`
- `tests/XerahS.Tests/Editor/*`

## Recommended Implementation Strategy (No Changes Applied Yet)

### Phase 1: Dependency Wiring
- Add `ImageEditor` as a submodule under this repo.
- Rewire all editor `ProjectReference` entries to the new in-repo path.
- Update `XerahS.sln` editor project entry to `ShareX.ImageEditor.csproj`.

### Phase 2: Mechanical Rename
- Replace namespace and AXAML assembly references:
  - `XerahS.Editor` -> `ShareX.ImageEditor`.

### Phase 3: API Adaptation
- Map tool enum usage:
  - `Pen` -> `Freehand`
  - `Highlighter` -> `Highlight`
  - `Number` -> `Step`
- Replace or localize unsupported controls:
  - move/host `AnnotationToolbar` in XerahS-owned UI layer (region capture path).
- Remove/replace `ShowMenuBar` usage.

### Phase 4: Effects Workflow Refactor
- Decouple `ImageEffectsViewModel` from editor-core-specific effect list APIs.
- Keep effect class usage from `ShareX.ImageEditor.ImageEffects.*`.
- Add/adjust tests around effect list state, undo/redo, preset behavior.

### Phase 5: Compatibility Layer
- Accept both type prefixes during effect preset load:
  - `XerahS.Editor.ImageEffects.*`
  - `ShareX.ImageEditor.ImageEffects.*`
- Add mapping for legacy persisted settings where needed.

### Phase 6: Validation
- Full `dotnet build XerahS.sln` with zero errors.
- Run affected test suites (`tests/XerahS.Tests` editor/effects/serializer areas).

## Risk Assessment
- High risk:
  - region capture overlay annotation UX (toolbar dependency).
  - effects editor workflow (API drift in `EditorCore` integration model).
- Medium risk:
  - preset/settings backwards compatibility.
- Low risk:
  - pure namespace and csproj rewiring once API gaps are handled.

## Conclusion
A direct hard swap to upstream `ImageEditor` is not safe on this branch. A phased migration is required, with API adaptation and compatibility mapping, not only a submodule/path replacement.

## Comprehensive Undo/Redo Comparison (Line-Level)

### Baseline Compared
- `XerahS.Editor` (in-branch): commit `74f8f8a` (`2026-02-09`)
- `ShareX/ImageEditor` (upstream): commit `50fd92c` (`2026-02-11`)

### Side-by-Side Behavior Matrix (Annotations + Image Effects)

| Area | XerahS.Editor (this branch) | ShareX/ImageEditor (upstream) | Practical impact |
|---|---|---|---|
| Memento payload | `EditorMemento` stores `Effects` in addition to `Annotations/Canvas/SelectedAnnotationId` (`XerahS.Editor/src/XerahS.Editor/EditorMemento.cs:59`, `XerahS.Editor/src/XerahS.Editor/EditorMemento.cs:71`). | No `Effects` field in memento (`src/ShareX.ImageEditor/EditorMemento.cs:64`). | Upstream cannot restore effect-list state as part of core undo snapshots. |
| History snapshot content | Both annotation and canvas snapshots capture effects via `GetEffectsSnapshot()` (`XerahS.Editor/src/XerahS.Editor/EditorHistory.cs:144`, `XerahS.Editor/src/XerahS.Editor/EditorHistory.cs:156`). | Snapshot creation captures annotations/canvas only (`src/ShareX.ImageEditor/EditorHistory.cs:157`, `src/ShareX.ImageEditor/EditorHistory.cs:168`). | Mixed annotation/effect undo chains are first-class only in XerahS.Editor. |
| Effect-specific history entry | Dedicated `CreateEffectsMemento()` exists (`XerahS.Editor/src/XerahS.Editor/EditorHistory.cs:208`). | No equivalent API (`src/ShareX.ImageEditor/EditorHistory.cs`, no `CreateEffectsMemento`). | Upstream has no core-native "effect action" history node type. |
| Annotation transform transactions | Explicit transaction boundaries: `BeginAnnotationTransform` / `EndAnnotationTransform` (`XerahS.Editor/src/XerahS.Editor/EditorCore.cs:874`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:889`) with UI/controller call sites (`XerahS.Editor/src/XerahS.Editor/Views/EditorView.axaml.cs:197`, `XerahS.Editor/src/XerahS.Editor/Views/Controllers/EditorSelectionController.cs:123`, `XerahS.Editor/src/XerahS.Editor/Views/Controllers/EditorSelectionController.cs:334`). | No transform transaction APIs in `EditorCore`, and no controller call sites (`src/ShareX.ImageEditor/EditorCore.cs`, `src/ShareX.ImageEditor/Views/Controllers/EditorSelectionController.cs`). | Drag/resize history coalescing and reliability is stronger in XerahS.Editor. |
| Region-tool guard override | `CreateAnnotationsMemento(..., ignoreToolGuard)` available (`XerahS.Editor/src/XerahS.Editor/EditorHistory.cs:190`) and used for transform operations (`XerahS.Editor/src/XerahS.Editor/EditorCore.cs:881`). | `CreateAnnotationsMemento` has no override (`src/ShareX.ImageEditor/EditorHistory.cs:201`). | XerahS.Editor can record legitimate transforms even when tool guards would otherwise suppress history entries. |
| Core effect lifecycle | Core owns list and mutation APIs: `AddEffect/RemoveEffect/ToggleEffect/SetEffects/LoadEffects` and `EffectsChanged` (`XerahS.Editor/src/XerahS.Editor/EditorCore.cs:80`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:901`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:917`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:930`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:943`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:957`). | No core effect list/event/mutation surface (`src/ShareX.ImageEditor/EditorCore.cs`). | Upstream effect behavior is not integrated into core undo semantics. |
| Preview/commit effect pipeline | Core-level preview API exists (`SetPreviewEffect`, `ClearPreviewEffect`, `CommitPreviewEffect`) (`XerahS.Editor/src/XerahS.Editor/EditorCore.cs:178`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:197`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:208`). | No core preview pipeline (`src/ShareX.ImageEditor/EditorCore.cs`). | Non-destructive preview + committed undo path is more deterministic in XerahS.Editor. |
| Undo restore completeness | `RestoreState` restores annotations, canvas, selection, and effects (`XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1009`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1037`). | `RestoreState` restores annotations, canvas, selection only (`src/ShareX.ImageEditor/EditorCore.cs:759`). | Effect-list restoration on core undo/redo is absent upstream. |
| View wiring for effects | View subscribes to core `EffectsChanged` and syncs VM applied-effects list (`XerahS.Editor/src/XerahS.Editor/Views/EditorView.axaml.cs:117`). | No corresponding subscription (`src/ShareX.ImageEditor/Views/EditorView.axaml.cs`). | UI consistency around applied-effects metadata is stronger in XerahS.Editor. |
| Undo command precedence (VM) | `Undo/Redo` prefer core history first, then legacy image stack fallback (`XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1311`, `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1341`). | `Undo/Redo` prefer image stack first, then core (`src/ShareX.ImageEditor/ViewModels/MainViewModel.cs:1121`, `src/ShareX.ImageEditor/ViewModels/MainViewModel.cs:1145`, `src/ShareX.ImageEditor/ViewModels/MainViewModel.cs:1152`, `src/ShareX.ImageEditor/ViewModels/MainViewModel.cs:1175`). | Mixed-operation chronology is more coherent in XerahS.Editor. |
| VM effect stack coupling | VM snapshots include `_effectsUndoStack/_effectsRedoStack` alongside image stack (`XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:637`, `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:642`, `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:654`, `XerahS.Editor/src/XerahS.Editor/ViewModels/MainViewModel.cs:1921`). | Only image bitmap stacks exist (`src/ShareX.ImageEditor/ViewModels/MainViewModel.cs:1431`). | Upstream VM undo cannot restore applied-effect metadata list. |
| Rotate/flip integration path | Menu handlers route to core rotate/flip operations (`XerahS.Editor/src/XerahS.Editor/Views/EditorView.axaml.cs:1400`, `XerahS.Editor/src/XerahS.Editor/Views/EditorView.axaml.cs:1415`), and core creates canvas mementos for crop/cutout/rotate/flip (`XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1272`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1395`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1663`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1729`). | Menu handlers route rotate/flip to VM commands (`src/ShareX.ImageEditor/Views/EditorView.axaml.cs:1174`, `src/ShareX.ImageEditor/Views/EditorView.axaml.cs:1209`); core only snapshots crop/cutout (`src/ShareX.ImageEditor/EditorCore.cs:1001`, `src/ShareX.ImageEditor/EditorCore.cs:1082`). | XerahS.Editor keeps geometric transforms in core history with annotation transforms; upstream keeps them outside core history. |
| Rotate/flip annotation preservation | Core rotate/flip methods transform annotations and fire restore events (`XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1660`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1711`, `XerahS.Editor/src/XerahS.Editor/EditorCore.cs:1769`). | VM rotate/flip updates preview with `clearAnnotations: true` (`src/ShareX.ImageEditor/ViewModels/MainViewModel.cs:1576`, `src/ShareX.ImageEditor/ViewModels/MainViewModel.cs:1633`). | Annotation continuity across rotate/flip undo chains is materially better in XerahS.Editor. |
| Effect apply entrypoint | Immediate effect actions call core `AddEffect` (`XerahS.Editor/src/XerahS.Editor/Views/EditorView.axaml.cs:1433`). | Immediate effect actions execute VM bitmap commands (`src/ShareX.ImageEditor/Views/EditorView.axaml.cs:1240`). | XerahS.Editor keeps effect operations in one core history model; upstream splits concerns. |
| Automated regression coverage | Scenario tests exist for mixed annotation/effect undo-redo, crop+effect chains, effect toggle, preview commit/cancel, and rotate+annotation survival (`tests/XerahS.Tests/Editor/EditorHistoryEffectsTests.cs:55`, `tests/XerahS.Tests/Editor/EditorHistoryEffectsTests.cs:461`, `tests/XerahS.Tests/Editor/EditorHistoryEffectsTests.cs:583`, `tests/XerahS.Tests/Editor/EditorRotateAnnotationsTests.cs:240`). | No `tests/` directory present in inspected upstream snapshot. | XerahS.Editor behavior is validated against more real workflows. |

### Net Assessment
- For undo/redo behavior specifically, this branch (`XerahS.Editor`) is currently more robust across mixed annotations + effects + crop/rotate workflows.
- The primary architectural reason is that XerahS.Editor keeps far more of the state machine inside `EditorCore` history/mementos, while upstream still splits responsibility across core history and VM image snapshots.

## TODO: Improve ShareX/ImageEditor Undo/Redo
Based on this branch comparison, `XerahS.Editor` currently has the more robust undo/redo behavior for mixed annotation/effect/crop/rotate workflows. To close that gap upstream, track the following TODOs for `ShareX/ImageEditor`:

- TODO: Centralize full history ownership in `EditorCore` (not split between `EditorCore` and host `MainViewModel`) so all edit operations participate in one coherent stack.
- TODO: Add effect-list history APIs in `EditorCore` (`AddEffect`, `RemoveEffect`, `ToggleEffect`, `SetEffects`, `LoadEffects`) with deterministic undo/redo semantics.
- TODO: Add a dedicated `EffectsChanged` event contract from `EditorCore` for host synchronization and predictable UI refresh.
- TODO: Ensure crop/rotate/flip operations preserve and correctly restore annotations and active effects across deep undo/redo chains.
- TODO: Add transaction-style history entries for composite operations (for example, "set preset", "crop then keep effects") to avoid partial/fragmented restores.
- TODO: Guarantee consistent invalidation signaling after every undo/redo mutation path (image state, annotations state, and effects state).
- TODO: Port/replicate scenario-driven regression tests similar to XerahS coverage (mixed annotations+effects, redo-stack invalidation after new action, rotate/crop interaction, preview/commit/cancel behavior).
