# SIP0014: Decouple Editor as Shared DLL

## Priority
**MEDIUM** - Cross-platform reusability enablement

## Assignee
**TBD**

## Branch
`feature/editor-decoupling`

## Instructions
**CRITICAL**: Create the `feature/editor-decoupling` branch first. This is a **refactor-only** task - no code rewriting allowed.

```bash
git checkout master
git pull origin master
git checkout -b feature/editor-decoupling
```

## Objective
Extract the Editor logic into a standalone `ShareX.Editor` Dynamic Link Library (DLL) that can be consumed by **both**:
1. **ShareX.Avalonia** - The new cross-platform Avalonia UI
2. **ShareX (WinForms)** - The original Windows Forms application

### Primary Use Cases
| Consumer | Use Case |
|----------|----------|
| ShareX.Avalonia | Full editor experience (region capture → annotate → save/upload) |
| ShareX (WinForms) | **Annotate existing screenshots/images** - Load saved images for annotation without re-capture |

> [!NOTE]
> For WinForms, the primary goal is to enable opening and annotating **already-saved images** (from History, file picker, etc.), providing a standalone image editor experience separate from the region capture flow.

> [!WARNING]
> **Scope Boundary**: This DLL is for **image annotation/editing ONLY**.
> - ❌ **NO screen capture logic** (no screenshots, no region selection, no window detection)
> - ❌ **NO monitor/display handling**
> - ✅ **ONLY** editing/annotating images that are already loaded into memory

> [!CAUTION]
> This is a **refactor exercise only**. Strict controls must be in place to:
> - **NOT rewrite** any existing code
> - **ONLY move** and reorganize existing files
> - **ONLY create** thin abstraction layers where necessary
> - Preserve all existing functionality

---

## Background

### Current State
The Editor functionality is currently split across multiple locations:

| Project | Purpose | Key Files |
|---------|---------|-----------|
| `ShareX.Avalonia.Annotations` | Annotation models | 19 annotation types (Rectangle, Ellipse, Arrow, Text, Blur, etc.) |
| `ShareX.Avalonia.UI` | View layer | `EditorView.axaml`, `EditorView.axaml.cs` |
| `ShareX.ScreenCaptureLib` (WinForms) | Shape management | `ShapeManager.cs` (~79KB), `BaseShape.cs`, Drawing/, Effect/ subdirs |

### Goal
Create a **UI-agnostic** `ShareX.Editor` DLL that encapsulates:
- Shape/annotation models
- Shape management logic (create, select, move, resize, delete)
- Undo/redo command stack
- Serialization/deserialization of annotations

The DLL should expose clean interfaces that both Avalonia and WinForms can implement for rendering.

---

## Scope

### Phase 1: Define Abstraction Interfaces

**Create**: `src/ShareX.Editor/Abstractions/`

```
IEditorCanvas          - Platform-agnostic rendering surface
IAnnotation            - Base annotation interface
IAnnotationRenderer    - Platform-specific rendering bridge
ICommandStack          - Undo/redo operations
```

> [!IMPORTANT]
> These interfaces must be **pure abstractions** with no Avalonia or WinForms dependencies.

### Phase 2: Move Existing Models

**Source**: `ShareX.Avalonia.Annotations/Models/`
**Target**: `ShareX.Editor/Models/`

Files to move (19 files):
- `Annotation.cs` (base class)
- `EditorTool.cs` (enum)
- All 17 annotation implementations

> [!NOTE]
> The existing models in `ShareX.Avalonia.Annotations` are already UI-agnostic (no Avalonia dependencies in the model layer). This makes them ideal candidates for direct migration.

### Phase 3: Extract Shape Management Logic

**Reference**: `ShareX.ScreenCaptureLib/Shapes/ShapeManager.cs`

Extract the following **logic-only** components:
- Selection logic (hit testing)
- Move/resize operations
- Z-order management
- Clone/duplicate operations
- Undo/redo stack

**DO NOT extract**:
- Menu creation code
- WinForms-specific rendering
- UI event handlers

### Phase 4: Wire Up Platform Implementations

**Avalonia Implementation**:
- Create `ShareX.Avalonia.Editor` wrapper project
- Implement `IAnnotationRenderer` using Avalonia's `DrawingContext`
- Update `EditorView.axaml.cs` to use shared `ShareX.Editor` types

**WinForms Implementation** (future):
- Create `ShareX.Editor.WinForms` wrapper project
- Implement `IAnnotationRenderer` using `System.Drawing.Graphics`
- Update `RegionCaptureForm` to use shared types

---

## Project Structure

```
src/
├── ShareX.Editor/                    # [NEW] Shared DLL
│   ├── ShareX.Editor.csproj          # net9.0 for modern compatibility
│   ├── Abstractions/
│   │   ├── IEditorCanvas.cs
│   │   ├── IAnnotation.cs
│   │   ├── IAnnotationRenderer.cs
│   │   └── ICommandStack.cs
│   ├── Models/
│   │   ├── Annotation.cs             # [MOVED from Annotations]
│   │   ├── EditorTool.cs             # [MOVED from Annotations]
│   │   ├── RectangleAnnotation.cs    # [MOVED from Annotations]
│   │   └── ... (17 more)
│   ├── Services/
│   │   ├── ShapeManager.cs           # [EXTRACTED logic from ScreenCaptureLib]
│   │   ├── SelectionManager.cs       # Hit testing, selection logic
│   │   └── CommandStack.cs           # Undo/redo implementation
│   └── Serialization/
│       └── AnnotationSerializer.cs   # JSON/XML serialization
│
├── ShareX.Avalonia.Editor/           # [NEW] Avalonia-specific wrapper
│   ├── ShareX.Avalonia.Editor.csproj
│   └── AvaloniaAnnotationRenderer.cs # Implements IAnnotationRenderer
│
└── ShareX.Avalonia.UI/               # [MODIFIED]
    └── Views/
        └── EditorView.axaml.cs       # Updated to use ShareX.Editor
```

---

## Guidelines

### DO ✅
- Move files as-is with minimal changes
- Create thin interface wrappers
- Maintain backward compatibility
- Add XML documentation to interfaces
- Target `netstandard2.0` for maximum compatibility

### DO NOT ❌
- Rewrite existing logic
- Change existing method signatures unnecessarily
- Add new features during this refactor
- Remove any existing functionality
- Add new NuGet dependencies without approval

---

## Dependencies

### ShareX.Editor (Shared DLL)
- `net9.0` (Compatible with both WinForms net9.0 and Avalonia net10.0)
- No UI framework dependencies
- Minimal external packages (preferably none)

### ShareX.Avalonia.Editor (Avalonia Wrapper)
- References `ShareX.Editor`
- References `Avalonia`
- References `SkiaSharp` (for rendering)

---

### Phase 5: Integration Verification (Avalonia)

**Objective**: Ensure `EditorView` remains accessible via both Nav Bar and History.

1.  **Nav Bar Integration**:
    *   Verify `EditorView` can be hosted in the main navigation shell.
    *   Verify it initializes correctly with no image (empty state) or default image.

2.  **History Integration (Standalone Mode)**:
    *   Verify "Open in Editor" from History loads the selected image into `EditorView`.
    *   **Crucial**: Ensure `EditorView` can be instantiated **independently** of the global `MainViewModel` capture state (e.g., via a dedicated `EditorViewModel` wrapper if needed) to allow editing history images without affecting the main capture workflow.

3.  **UI Components**:
    *   Verify capture toolbar is NOT present when opened from History (Visual Tree alignment).
    *   Verify "Save" / "Copy" actions work for the specific editor instance.

---

## Acceptance Criteria

1.  **Build**: Both ShareX.Avalonia and ShareX (WinForms) compile successfully
2.  **No Regressions**: All existing Editor features work identically
3.  **Integration**: Successfully edit an image from History *while* the main capture window is idle or doing something else.
4.  **Tests**: Existing annotation tests still pass
5.  **Code Review**: No logic changes, only reorganization

---

## Verification Plan

### Automated
- Run `dotnet build ShareX.Avalonia.sln` - must pass with 0 errors
- Run existing annotation unit tests (if any exist)

### Manual Verification
1. Launch ShareX.Avalonia
2. Capture a screenshot
3. Use each annotation tool (Rectangle, Arrow, Text, Blur, etc.)
4. Verify undo/redo works
5. Save annotated image
6. Verify image saves correctly with annotations baked in

---

## Estimated Effort
**HIGH** - 8-12 hours
- Phase 1 (Abstractions): 2 hours
- Phase 2 (Model migration): 1 hour
- Phase 3 (Logic extraction): 4-6 hours
- Phase 4 (Wiring): 2-3 hours

---

## Related Documents
- [SIP0004_Annotation_Canvas.md](./SIP0004_Annotation_Canvas.md) - Original annotation implementation
- [AGENTS.md](../AGENTS.md) - Code style rules
- `ShareX.ScreenCaptureLib/Shapes/ShapeManager.cs` - Reference implementation
