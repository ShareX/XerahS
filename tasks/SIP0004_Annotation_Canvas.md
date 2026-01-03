# CP01: Annotation Canvas (Phase 2)

## Priority
**HIGH** - Core ShareX Feature

## Assignee
**Copilot** (Surface Laptop 7, VS2026 IDE)

## Branch
`feature/annotation-canvas`

## Instructions
**CRITICAL**: You must START by creating (or checking out if it exists) the branch `feature/annotation-canvas`. Do not work on `main`.

## Objective
Implement the core image annotation tools for the `AnnotationCanvas` control. This is the heart of ShareX's image editor.
Ref: `ShareX.ScreenCaptureLib/Shapes`

## Architectural Vision

> [!IMPORTANT]
> **EditorView must be designed as a reusable component**, not coupled to any specific window or context.

### Use Cases
1. **New Screenshot → Editor**: When a screenshot is captured, it immediately opens in `EditorView`
2. **History → Editor**: When user right-clicks a historical screenshot and selects "Edit Image", a new instance of `EditorView` opens with that image

### Design Requirements
- `EditorView.axaml` and `EditorViewModel` must be **self-contained** and **portable**
- Support **multiple simultaneous instances** (user can edit multiple images at once in separate windows/tabs)
- The `EditorViewModel` should accept an image via constructor (e.g., `EditorViewModel(Image image, string? filePath = null)`)
- **Do not couple** the editor to the main window or any specific parent container
- The view should work equally well for:
  - Fresh screenshots (new image, no file path)
  - Historical images (existing image, known file path)

## Scope

> [!NOTE]
> **Update (2026-01-02)**: Core annotation models (Rectangle, Ellipse, Arrow, Text, Highlight) have been implemented by Jaex in `ShareX.Avalonia.Annotations`. Focus is now on UI integration.

### 1. ✅ Shape Architecture (COMPLETE)
Jaex has implemented:
- Base `Annotation` class with `Render()` and `HitTest()` methods
- All 5 core tools: `RectangleAnnotation`, `EllipseAnnotation`, `ArrowAnnotation`, `TextAnnotation`, `HighlightAnnotation`
- Additional tools: `LineAnnotation`, `FreehandAnnotation`, `BlurAnnotation`, `PixelateAnnotation`, `NumberAnnotation`, etc.
- `EditorTool` enum for tool selection
- Color management and rendering helpers

**Location**: `src/ShareX.Avalonia.Annotations/Models/`

### 2. UI Canvas Control (NEW FOCUS)
Create the Avalonia UI control to host and interact with annotations:
- **Create `AnnotationCanvas.axaml` + `AnnotationCanvas.axaml.cs`** in `ShareX.Avalonia.UI/Controls/`
- **Rendering**: Use `DrawingContext` in `OnRender()` to draw all annotations
- **Mouse Interaction**: Handle mouse down/move/up for drawing new annotations
- **Tool Selection**: Wire up tool switching (Rectangle, Ellipse, Arrow, Text, Highlighter)
- **Color/Width Picker**: UI elements for stroke color and width

### 3. Interaction Features
- **Selection**: Click to select annotations, show selection handles
- **Resize/Move**: Drag handles to resize, drag body to move
- **Delete**: Delete key to remove selected annotation
- **Undo/Redo**: Command stack for undo/redo operations

### 4. ViewModel Integration
- Create `AnnotationCanvasViewModel` or integrate into existing editor ViewModel
- Expose `ObservableCollection<Annotation>` for data binding
- Command bindings for tool selection, undo/redo, delete

## Guidelines
- **Reuse Models**: Use the existing `ShareX.Avalonia.Annotations` models, don't recreate them
- **Performance**: Use `InvalidateVisual()` only when needed to minimize redraws
- **MVVM**: Keep business logic in ViewModel, canvas handles rendering and input
- **Portability**: Design `EditorView` as a standalone, reusable component that can be instantiated in different contexts (new screenshots, history editing)
- **No Tight Coupling**: Avoid dependencies on MainWindow or specific parent containers

## Deliverables
- Functional `AnnotationCanvas` control that can draw, select, move, and delete annotations
- Integration with existing UI (likely in image editor window)
- Basic undo/redo support

