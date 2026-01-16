# ShareX.Editor Architecture Map

**Generated**: 2026-01-17
**Scope**: Comprehensive architectural overview of ShareX.Editor project
**Purpose**: Support detailed code review and maintenance

---

## Executive Summary

ShareX.Editor is a **platform-agnostic image annotation and editing library** with an Avalonia UI frontend. The architecture follows a **hybrid rendering model** where:
- **Raster effects** (blur, pixelate, magnify) render via SkiaSharp to SKBitmap
- **Vector annotations** (shapes, text, arrows) render via Avalonia controls
- **Core logic** is UI-agnostic in EditorCore.cs

---

## Repository Location

- **Main Project**: `C:\Users\liveu\source\repos\ShareX Team\ShareX.Editor\src\ShareX.Editor\`
- **Host Integration**: Referenced by XerahS.UI project at `C:\Users\liveu\source\repos\ShareX Team\XerahS\src\XerahS.UI\`

---

## Core Assembly Structure

### Entry Points

| Component | Path | Responsibility |
|-----------|------|----------------|
| **EditorView** | `Views/EditorView.axaml.cs` | Main Avalonia UserControl, coordinates UI and controllers |
| **EditorWindow** | `Views/EditorWindow.axaml.cs` | Window shell hosting EditorView |
| **EditorCore** | `EditorCore.cs` | Platform-agnostic core logic (annotations, undo/redo, input) |
| **MainViewModel** | `ViewModels/MainViewModel.cs` | MVVM bridge between UI and EditorCore |

### Key Namespaces

```
ShareX.Editor
├── Annotations/           # Annotation types (20+ classes)
├── Controls/              # Custom Avalonia controls
├── Converters/            # Value converters for XAML binding
├── Extensions/            # Helper extension methods
├── Helpers/               # Utility classes (BitmapConversionHelpers, etc.)
├── ImageEffects/          # Image manipulation effects (35+ classes)
│   ├── Adjustments/       # Color/brightness/contrast effects
│   ├── Filters/           # Blur, sharpen, pixelate, etc.
│   └── Manipulations/     # Resize, rotate, crop, flip
├── Serialization/         # JSON serialization support
├── Services/              # Editor services (clipboard, etc.)
├── ViewModels/            # MVVM ViewModels
└── Views/                 # Avalonia views and controllers
    ├── Controllers/       # Input, zoom, selection controllers
    └── Dialogs/           # Effect/tool configuration dialogs
```

---

## Architecture Layers

### Layer 1: Platform-Agnostic Core

**EditorCore.cs** (1080 lines)
- **State Management**: SourceImage (SKBitmap), ActiveTool, StrokeColor, StrokeWidth, Zoom, NumberCounter
- **Annotation Repository**: `List<Annotation>` with CRUD operations
- **Input Processing**: OnPointerPressed/Moved/Released → creates/updates annotations
- **Undo/Redo**: EditorHistory with memento pattern
- **Rendering**: Render(SKCanvas, bool renderVectorAnnotations)
- **Destructive Operations**: PerformCrop(), PerformCutOut()
- **Events**: InvalidateRequested, ImageChanged, AnnotationsRestored, HistoryChanged, StatusTextChanged, EditAnnotationRequested

**EditorHistory.cs** (likely exists)
- Memento pattern for undo/redo
- CreateAnnotationsMemento() - snapshot before annotation changes
- CreateCanvasMemento() - snapshot before crop/cutout (includes canvas bitmap)
- Undo() / Redo() - restore previous state

### Layer 2: Avalonia UI Presentation

**EditorView.axaml.cs** (1055 lines)
- **Controllers**: EditorZoomController, EditorSelectionController, EditorInputController
- **Hybrid Rendering**: SKCanvasControl for raster (background + effects) + Avalonia Canvas for vector (shapes, text)
- **Event Coordination**:
  - Subscribe to EditorCore events (InvalidateRequested, ImageChanged, AnnotationsRestored, HistoryChanged)
  - Forward input events to controllers
  - Sync ViewModel properties (Zoom, SelectedColor, StrokeWidth, ActiveTool, PreviewImage)
- **Annotation Factory**: CreateControlForAnnotation() - restores Avalonia visuals from Core annotations after undo/redo
- **Effect Update**: OnRequestUpdateEffect() - regenerates effect bitmaps for blur/pixelate/magnify
- **Dialogs**: ShowEffectDialog() - opens resize, crop, rotate, effects panels
- **Lifecycle**: OnLoaded, OnUnloaded - wire/unwire ViewModel events

**EditorView.axaml**
- Layout: ScrollViewer → Grid → (SKCanvasControl for raster + Canvas "AnnotationCanvas" for vector)
- Sidebars: Left (tools), Right (effects panel)
- Modal overlay for dialogs

### Layer 3: Input & Interaction Controllers

**EditorInputController.cs**
- Delegates canvas pointer events (pressed/moved/released)
- Creates Avalonia visual controls during drawing (e.g., Rectangle, Ellipse, Polyline)
- Manages crop overlay and interactive annotation creation
- Caches SKBitmap for effect annotations to avoid re-conversion
- Adds completed annotations to EditorCore via `EditorCore.AddAnnotation()`

**EditorSelectionController.cs**
- Manages selection adorners (resize handles)
- Drag-to-move and drag-to-resize logic
- Emits `RequestUpdateEffect` event when effect annotations are resized
- Clears selection when tool changes

**EditorZoomController.cs**
- Zoom via Ctrl+MouseWheel
- Pan via middle mouse button drag
- Manages ScrollViewer offset and zoom level
- Syncs with ViewModel.Zoom property

### Layer 4: MVVM ViewModels

**MainViewModel.cs**
- Properties: PreviewImage, Zoom, SelectedColor, StrokeWidth, ActiveTool, EditorTitle, ImageWidth, ImageHeight
- Commands: UndoCommand, RedoCommand, DeleteSelectedCommand, ClearAnnotationsCommand, SelectToolCommand, SetColorCommand, SetStrokeWidthCommand
- Image Operations: CropImage, ResizeImage, ResizeCanvas, Rotate90Clockwise, FlipHorizontal, etc.
- Effect Operations: PreviewEffect, ApplyEffect, CancelEffectPreview (stores original bitmap for preview/cancel)
- Events: UndoRequested, RedoRequested, DeleteRequested, ClearAnnotationsRequested, CopyRequested, SaveAsRequested, SnapshotRequested
- Panels: IsEffectsPanelOpen, EffectsPanelContent, IsSettingsPanelOpen

**EditorViewModel.cs** (purpose unclear - may be legacy or alternate ViewModel)

**ViewModelBase.cs**
- Base class with INotifyPropertyChanged implementation

---

## Annotation System

### Base Class

**Annotation.cs**
- Properties: Id, ToolType, StrokeColor, StrokeWidth, StartPoint, EndPoint, IsSelected, ZIndex
- Abstract Methods: Render(SKCanvas), HitTest(SKPoint, tolerance)
- Virtual Methods: GetBounds() → SKRect, Clone() → Annotation (for undo/redo deep copy)
- Helpers: ParseColor(), CreateStrokePaint(), CreateFillPaint()

### Annotation Types (20+)

| Type | Tool | Render Mode | Special Features |
|------|------|-------------|------------------|
| **RectangleAnnotation** | Rectangle | Vector (Avalonia Shape) | Simple rect stroke |
| **EllipseAnnotation** | Ellipse | Vector (Avalonia Shape) | Simple ellipse stroke |
| **LineAnnotation** | Line | Vector (Avalonia Line) | StartPoint, EndPoint |
| **ArrowAnnotation** | Arrow | Vector (Avalonia Path) | CreateArrowGeometry() with arrow head |
| **TextAnnotation** | Text | Vector (Avalonia TextBox) | Text, FontSize |
| **NumberAnnotation** | Number | Vector (Avalonia Grid with Ellipse + TextBlock) | Auto-incrementing number counter |
| **FreehandAnnotation** | Pen | Vector (Avalonia Polyline) | List\<SKPoint\> points |
| **HighlightAnnotation** | Highlighter | Vector (Avalonia Shape with semi-transparent fill) | Alpha 0x55 |
| **SmartEraserAnnotation** | SmartEraser | Vector (Avalonia Polyline) | Samples color from canvas on press |
| **BlurAnnotation** | Blur | Raster effect (ImageBrush fill) | BaseEffectAnnotation, UpdateEffect() generates EffectBitmap |
| **PixelateAnnotation** | Pixelate | Raster effect (ImageBrush fill) | BaseEffectAnnotation |
| **MagnifyAnnotation** | Magnify | Raster effect (ImageBrush fill) | BaseEffectAnnotation |
| **SpotlightAnnotation** | Spotlight | Vector (custom SpotlightControl) | Darkens everything except highlighted area, needs CanvasSize |
| **SpeechBalloonAnnotation** | SpeechBalloon | Vector (custom SpeechBalloonControl) | Tail direction |
| **ImageAnnotation** | Image | Vector (Avalonia Image control) | ImageBitmap (SKBitmap) |
| **CropAnnotation** | Crop | Immediate execution | Executes PerformCrop() on release, modifies SourceImage |
| **CutOutAnnotation** | CutOut | Immediate execution | Executes PerformCutOut() on release, IsVertical flag |

### BaseEffectAnnotation

Shared logic for Blur, Pixelate, Magnify, Highlight:
- `EffectBitmap` property (SKBitmap)
- `UpdateEffect(SKBitmap source)` - extracts region from source, applies effect, stores in EffectBitmap
- `CreateVisual()` - returns Avalonia Shape with ImageBrush fill set to EffectBitmap

---

## ImageEffects System

**Purpose**: Apply global image transformations (non-annotation-based)

### Base Class

**ImageEffect.cs**
- Abstract `Apply(SKBitmap source) → SKBitmap`
- Properties: Name, Description, Category

### Categories

1. **Adjustments** (13 effects): Alpha, Brightness, Contrast, Hue, Saturation, Gamma, Colorize, Grayscale, Invert, BlackAndWhite, Sepia, Polaroid, SelectiveColor, ReplaceColor
2. **Filters** (9 effects): Blur, Pixelate, Sharpen, Shadow, Glow, Outline, Border, Reflection, TornEdge, Slice
3. **Manipulations** (6 effects): Resize, Rotate, Flip, AutoCrop, RoundedCorners, Skew

### Effect Application Flow

1. User clicks effect menu item (e.g., "Brightness")
2. EditorView.OnBrightnessRequested() → ShowEffectDialog(new BrightnessDialog())
3. Dialog wires PreviewRequested, ApplyRequested, CancelRequested events
4. User adjusts slider → PreviewRequested → MainViewModel.PreviewEffect(effectOperation)
   - VM stores original bitmap if first preview
   - VM applies effect to preview bitmap
   - VM updates PreviewImage property → triggers EditorView reload
5. User clicks Apply → ApplyRequested → MainViewModel.ApplyEffect()
   - Effect becomes permanent (original discarded)
   - Close dialog
6. User clicks Cancel → CancelRequested → MainViewModel.CancelEffectPreview()
   - Restore original bitmap
   - Close dialog

---

## Tool System

### Tool Selection Flow

1. User clicks tool button or presses keyboard shortcut (R = Rectangle, E = Ellipse, etc.)
2. MainViewModel.SelectToolCommand.Execute(EditorTool.Rectangle)
3. MainViewModel.ActiveTool = EditorTool.Rectangle
4. EditorView.OnViewModelPropertyChanged() detects ActiveTool change → _selectionController.ClearSelection()
5. EditorCore.ActiveTool = EditorTool.Rectangle

### Drawing Flow (Vector Annotation Example: Rectangle)

1. User presses pointer on canvas
2. EditorView.OnCanvasPointerPressed → EditorInputController.OnCanvasPointerPressed
3. InputController creates Avalonia.Controls.Shapes.Rectangle control
4. Simultaneously: Calls EditorCore.OnPointerPressed(skPoint)
5. EditorCore creates RectangleAnnotation, adds to _annotations, sets _isDrawing = true
6. User moves pointer → InputController updates Rectangle.Width/Height
7. EditorCore.OnPointerMoved() updates RectangleAnnotation.EndPoint
8. User releases pointer → InputController finalizes Rectangle control on AnnotationCanvas
9. EditorCore.OnPointerReleased() → _history.CreateAnnotationsMemento() → auto-selects annotation
10. EditorView.RenderCore() triggered via InvalidateRequested event

### Drawing Flow (Raster Effect Example: Blur)

1-3. Same as above
4. InputController creates Avalonia.Controls.Shapes.Rectangle with transparent fill initially
5. EditorCore creates BlurAnnotation
6. On pointer moved: InputController updates rectangle size + caches source SKBitmap (if not cached)
7. On pointer released:
   - InputController calls BlurAnnotation.UpdateEffect(cachedBitmap)
   - BlurAnnotation generates blurred EffectBitmap
   - InputController sets Rectangle.Fill = new ImageBrush(BitmapConversionHelpers.ToAvaloniBitmap(EffectBitmap))
   - EditorCore saves annotation via memento
   - InputController adds annotation to EditorCore via EditorCore.AddAnnotation()

---

## Undo/Redo System

### Memento Pattern

**EditorHistory** maintains two stacks:
- Undo stack: List of EditorMemento
- Redo stack: Cleared on new action

**EditorMemento** contains:
- `List<Annotation> Annotations` - deep cloned snapshot
- `SKBitmap? Canvas` - for crop/cutout operations
- `SKSize CanvasSize` - for crop/cutout operations

### Snapshot Timing

- **Annotation Add**: CreateAnnotationsMemento() BEFORE adding new annotation
  - Excludes current annotation from snapshot (revert will remove it)
- **Annotation Delete**: (Not implemented in current code review scope)
- **Crop/CutOut**: CreateCanvasMemento() BEFORE modifying SourceImage
  - Includes full canvas bitmap to restore image

### Restore Flow

1. User clicks Undo
2. MainViewModel.UndoCommand → EditorView.PerformUndo() → EditorCore.Undo()
3. EditorHistory.Undo() → EditorCore.RestoreState(memento)
4. EditorCore.RestoreState():
   - Clears _annotations
   - Restores memento.Annotations (deep cloned objects)
   - If memento.Canvas exists, restores SourceImage
   - Fires AnnotationsRestored event
5. EditorView.OnAnnotationsRestored():
   - Clears AnnotationCanvas children
   - Recreates Avalonia controls via CreateControlForAnnotation() for each restored annotation
   - Calls OnRequestUpdateEffect() for effect annotations to regenerate bitmaps
   - Triggers RenderCore() for raster layer

---

## Rendering Pipeline

### Hybrid Rendering Model

**Raster Layer** (SKCanvasControl):
- Background image (SourceImage)
- Future: Raster annotations like stickers or complex effects

**Vector Layer** (Avalonia Canvas "AnnotationCanvas"):
- All shape annotations (Rectangle, Ellipse, Line, Arrow, Text, Number, Freehand, SmartEraser)
- Effect annotations with ImageBrush fills (Blur, Pixelate, Magnify, Highlight)
- Spotlight and SpeechBalloon via custom controls

**Render Flow**:
1. EditorCore.InvalidateRequested event fires
2. EditorView.RenderCore() calls SKCanvasControl.Draw(canvas => EditorCore.Render(canvas, renderVectorAnnotations: false))
3. EditorCore.Render() draws SourceImage + (future raster annotations only)
4. Avalonia renders AnnotationCanvas with all vector controls on top

### Snapshot for Export

1. User clicks Copy or Save
2. MainViewModel requests snapshot via SnapshotRequested event
3. EditorView.GetSnapshot() renders CanvasContainer (Grid) to RenderTargetBitmap
4. Converts to SKBitmap for clipboard or file save

---

## Input Handling

### Pointer Event Routing

```
User Input (Mouse/Touch)
  ↓
EditorView.axaml (Canvas PointerPressed/Moved/Released events)
  ↓
EditorView.axaml.cs event handlers
  ↓
EditorInputController.OnCanvasPointerPressed/Moved/Released
  ↓
EditorCore.OnPointerPressed/Moved/Released (SKPoint coordinates)
  ↓
EditorCore updates _currentAnnotation or _selectedAnnotation
  ↓
EditorCore fires InvalidateRequested event
  ↓
EditorView.RenderCore() updates SKCanvasControl
```

### Keyboard Shortcuts

Handled in EditorView.OnKeyDown():
- Delete → DeleteSelectedCommand
- Ctrl+Z → Undo
- Ctrl+Shift+Z / Ctrl+Y → Redo
- Tool shortcuts: V (Select), R (Rectangle), E (Ellipse), A (Arrow), L (Line), T (Text), S (Spotlight), B (Blur), P (Pixelate), I (Image), F (Pen), H (Highlighter), M (Magnify), C (Crop)

---

## Data Flow: Tool Change Example

```
User clicks "Rectangle" button in UI
  ↓
MainViewModel.SelectToolCommand.Execute(EditorTool.Rectangle)
  ↓
MainViewModel.ActiveTool = EditorTool.Rectangle (property setter fires PropertyChanged)
  ↓
EditorView.OnViewModelPropertyChanged(ActiveTool)
  ↓
_selectionController.ClearSelection()
  ↓
EditorCore.ActiveTool = EditorTool.Rectangle
  ↓
User clicks canvas → EditorCore.OnPointerPressed() creates RectangleAnnotation based on ActiveTool
```

---

## Data Flow: Undo After Drawing

```
User draws rectangle → releases pointer
  ↓
EditorCore.OnPointerReleased() → _history.CreateAnnotationsMemento(excludeAnnotation: _currentAnnotation)
  ↓
EditorHistory captures snapshot of all annotations EXCEPT current one
  ↓
User clicks Undo button
  ↓
MainViewModel.UndoCommand → EditorView.PerformUndo() → EditorCore.Undo()
  ↓
EditorHistory.Undo() retrieves last memento → EditorCore.RestoreState(memento)
  ↓
EditorCore clears _annotations, restores memento.Annotations (which doesn't include the rectangle)
  ↓
EditorCore fires AnnotationsRestored event
  ↓
EditorView.OnAnnotationsRestored() clears AnnotationCanvas, recreates controls from restored annotations
  ↓
Rectangle is gone (not in restored set)
```

---

## Critical Dependencies

### Avalonia (v11.3.10)
- UI framework: UserControl, Canvas, Shapes (Rectangle, Ellipse, Line, Path, Polyline), ScrollViewer, Grid
- Data binding: INotifyPropertyChanged, Binding
- Input: Pointer events, keyboard events
- Rendering: RenderTargetBitmap for snapshots

### SkiaSharp (v2.88.9)
- SKBitmap: Image storage and manipulation
- SKCanvas: Rendering surface for effects
- SKPaint: Drawing primitives
- SKColor: Color representation

### CommunityToolkit.Mvvm (v8.4.0)
- RelayCommand: ICommand implementation for ViewModel commands
- ObservableObject: Base class for INPC

### Newtonsoft.Json (v13.0.4)
- Annotation serialization (JSON)

---

## Known Architectural Patterns

1. **MVVM**: ViewModel (MainViewModel) ↔ View (EditorView.axaml)
2. **Memento**: EditorHistory + EditorMemento for undo/redo
3. **Factory**: EditorCore.CreateAnnotation(EditorTool) → Annotation instance
4. **Observer**: Events (InvalidateRequested, ImageChanged, AnnotationsRestored, etc.)
5. **Controller Pattern**: EditorInputController, EditorZoomController, EditorSelectionController separate concerns from EditorView
6. **Hybrid Rendering**: Platform-agnostic SKCanvas + platform-specific Avalonia Canvas

---

## Key Files by Line Count (Estimated)

| File | Lines | Complexity |
|------|-------|-----------|
| EditorView.axaml.cs | 1055 | High - coordinates UI, controllers, dialogs, events |
| EditorCore.cs | 1080 | High - core logic, input, rendering, undo/redo |
| EditorInputController.cs | ~500 | High - input processing, annotation creation |
| MainViewModel.cs | ~800 | High - ViewModel commands, image operations, effect preview |
| EditorSelectionController.cs | ~400 | Medium - selection, resize handles |
| EditorZoomController.cs | ~300 | Medium - zoom/pan logic |
| Annotation.cs | 168 | Low - base class |
| ArrowAnnotation.cs | ~200 | Medium - geometry for arrow head |
| BaseEffectAnnotation.cs | ~150 | Medium - effect update logic |
| BlurAnnotation.cs | ~100 | Medium - blur algorithm |

---

## Potential Architectural Concerns (To Investigate in Phase 2)

1. **Dual Annotation State**: Annotations exist in both EditorCore._annotations (SKPoint-based models) AND AnnotationCanvas.Children (Avalonia Control visuals). Synchronization risk?
2. **Effect Bitmap Caching**: InputController caches source bitmap for effect updates. Is this memory efficient for large images?
3. **Undo/Redo for Effects**: Effect annotations regenerate bitmaps on restore. Performance impact?
4. **Crop/CutOut Memento Size**: Full canvas bitmap stored in memento. Memory concern for large images or deep undo stack?
5. **Selection After Undo**: EditorCore.RestoreState() clears _selectedAnnotation. Is selection state persisted in mementos?
6. **Tool State Transitions**: When switching tools, is there cleanup of in-progress annotations?
7. **Coordinate Mapping**: DPI scaling handled? Evidence: EditorView.PerformCrop() uses `topLevel.RenderScaling`. Is this applied consistently?
8. **Threading**: Events dispatched to UI thread (Dispatcher.UIThread.Post). Any race conditions?
9. **Disposal**: Annotations hold SKBitmap references (ImageAnnotation.ImageBitmap, BaseEffectAnnotation.EffectBitmap). Are they disposed on annotation removal?
10. **Null Safety**: Nullable reference types enabled. Are all null checks present?

---

## Testing Surface

### Manual Test Scenarios (For Phase 5)

1. Draw → Undo → Redo → Verify annotation restored correctly
2. Draw effect annotation (blur) → Resize → Verify effect bitmap updates
3. Crop → Undo → Verify full image restored
4. Draw multiple annotations → Undo multiple times → Redo → Verify stack consistency
5. Select annotation → Change color → Verify visual updates
6. Select annotation → Delete → Verify removal
7. Zoom in/out → Pan → Verify rendering quality
8. Apply image effect (brightness) → Preview → Cancel → Verify original restored
9. Apply image effect → Apply → Undo → Verify effect reverted (if undo captures pre-effect state)
10. Keyboard shortcuts → Verify all tools activate correctly
11. Multi-monitor → DPI scaling → Verify coordinate mapping
12. Large image (10MB+) → Performance of undo/redo with full canvas mementos

---

## Summary

ShareX.Editor implements a sophisticated **hybrid rendering architecture** with clear separation between:
- **Platform-agnostic core** (EditorCore, Annotations, ImageEffects)
- **Platform-specific UI** (Avalonia controls, views, controllers)
- **MVVM bridging** (MainViewModel)

The **undo/redo system** uses deep cloning and full canvas snapshots for destructive operations.

The **tool system** supports 20+ annotation types with both vector and raster rendering modes.

The **input pipeline** routes user interactions through controllers to the core, maintaining separation of concerns.

**Next Step**: Phase 2 - Line-by-line review with issue log.
