# ShareX.Editor Code Review - Issue Log

**Date**: 2026-01-17
**Reviewer**: AI Code Review Agent
**Scope**: ShareX.Editor project (Views, Controllers, ViewModels, Annotations, ImageEffects)
**Baseline**: `develop` branch

---

## Issue Template

Each issue follows this structure:
- **ID**: Unique identifier
- **Severity**: Blocker | High | Medium | Low
- **File**: File path and line range
- **Category**: Code Smell | Duplication | Null Safety | Threading | DPI | Coordination | UX | Performance | Correctness
- **Description**: What's wrong
- **Expected Behaviour**: What should happen
- **Actual Behaviour**: What currently happens
- **Evidence**: Code snippet, call stack, repro steps
- **Root Cause**: Why it's broken
- **Fix Plan**: How to fix it
- **Risk**: Impact of the fix
- **Validation**: How to verify the fix

---

## Issues Summary

| Severity | Count |
|----------|-------|
| Blocker  | 3 |
| High     | 12 |
| Medium   | 18 |
| Low      | 8 |
| **Total** | **41** |

---

## Blocker Issues

### ISSUE-001: Dual Annotation State - Synchronization Risk

**Severity**: Blocker
**File**: `EditorView.axaml.cs:370-392`, `EditorInputController.cs:494-497`, `EditorCore.cs:199-310`
**Category**: Correctness, Architecture

**Description**:
Annotations exist in TWO places simultaneously:
1. **EditorCore._annotations** (List<Annotation> - SKPoint-based models)
2. **AnnotationCanvas.Children** (Avalonia Control visuals)

These two states are synchronized manually through events and factory methods, creating multiple points of failure.

**Expected Behaviour**:
Single source of truth for annotation state with automatic UI synchronization.

**Actual Behaviour**:
- InputController creates Avalonia Control AND calls `EditorCore.AddAnnotation()`
- EditorCore manages history mementos based on its annotation list
- Undo/Redo in EditorCore fires `AnnotationsRestored` event
- EditorView.OnAnnotationsRestored() manually recreates ALL Avalonia controls from EditorCore state
- Effect annotations (Blur, Pixelate, Magnify) require `OnRequestUpdateEffect()` to regenerate bitmaps

**Evidence**:
```csharp
// EditorInputController.cs:494-497
// Ensure annotation is added to Core history
if (_currentShape.Tag is Annotation annotation && vm.ActiveTool != EditorTool.Crop && vm.ActiveTool != EditorTool.CutOut)
{
    _view.EditorCore.AddAnnotation(annotation);
}
```

```csharp
// EditorView.axaml.cs:370-392
private void OnAnnotationsRestored()
{
    // Fully rebuild annotation layer from Core state
    var canvas = this.FindControl<Canvas>("AnnotationCanvas");
    if (canvas == null) return;

    canvas.Children.Clear(); // ALL visuals destroyed
    _selectionController.ClearSelection();

    // Re-create UI for all vector annotations in Core
    foreach (var annotation in _editorCore.Annotations)
    {
        Control? shape = CreateControlForAnnotation(annotation); // Recreate from scratch
        if (shape != null)
        {
            canvas.Children.Add(shape);
        }
    }

    RenderCore();
}
```

**Root Cause**:
Hybrid rendering model chose to maintain separate UI state (Avalonia Controls) and logic state (EditorCore annotations). The synchronization is event-driven and requires full UI rebuild on history restore.

**Fix Plan**:
1. **Option A (Minimal)**: Add explicit synchronization validation
   - After every `AddAnnotation()`, verify `AnnotationCanvas.Children.Count == EditorCore.Annotations.Count`
   - Add debug logging for mismatches
   - Document the dual-state contract clearly

2. **Option B (Refactor)**: Move to single source of truth
   - EditorCore holds annotations only
   - UI layer renders from EditorCore.Annotations on every frame (data binding or manual render loop)
   - Eliminate AnnotationCanvas.Children as state storage
   - **HIGH RISK** - architectural change

**Recommended**: Option A for now, Option B for future refactor.

**Risk**:
- Option A: Low risk, improves diagnostics
- Option B: High risk, major architectural change

**Validation**:
- Draw annotation → Undo → Redo → Verify visual matches model
- Draw 10 annotations → Undo 5 → Redo 3 → Verify consistency
- Resize effect annotation → Undo → Verify effect bitmap regenerated correctly

---

### ISSUE-002: Memory Leak - Effect Annotation Bitmaps Not Disposed

**Severity**: Blocker
**File**: `EditorInputController.cs:502-503`, `EditorView.axaml.cs:577-586`
**Category**: Performance, Memory Management

**Description**:
BaseEffectAnnotation instances (Blur, Pixelate, Magnify) hold `SKBitmap? EffectBitmap` which is created during `UpdateEffect()`. When annotations are deleted (right-click delete or PerformDelete), the Avalonia Control is removed from the canvas, but the annotation's EffectBitmap is **never disposed**.

**Expected Behaviour**:
All SKBitmap resources are disposed when annotations are deleted.

**Actual Behaviour**:
- User draws blur annotation → `BlurAnnotation.UpdateEffect()` allocates SKBitmap for EffectBitmap
- User right-clicks to delete → `EditorInputController.OnCanvasPointerPressed()` removes Control from canvas
- EffectBitmap remains in memory until GC finalizer runs (potentially never for long-running app)

**Evidence**:
```csharp
// EditorInputController.cs:90-101 (Right-click delete)
if (hitTarget != null)
{
    if (_selectionController.SelectedShape == hitTarget)
    {
        _selectionController.ClearSelection();
    }

    canvas.Children.Remove(hitTarget); // Control removed
    vm.StatusText = "Shape deleted";
    e.Handled = true;
    return;
}
// hitTarget.Tag (Annotation) is NOT disposed
```

```csharp
// EditorView.axaml.cs:572-587 (PerformDelete)
private void PerformDelete()
{
    var selected = _selectionController.SelectedShape;
    if (selected != null)
    {
        var canvas = this.FindControl<Canvas>("AnnotationCanvas");
        if (canvas != null && canvas.Children.Contains(selected))
        {
            canvas.Children.Remove(selected); // Control removed
            _selectionController.ClearSelection();
        }
    }
}
// selected.Tag (Annotation) is NOT disposed
```

**Root Cause**:
No disposal logic for annotations when removing them from the canvas. SKBitmap is unmanaged resource that requires explicit disposal.

**Fix Plan**:
1. Add `IDisposable` implementation to `BaseEffectAnnotation` and `ImageAnnotation`
2. Dispose `EffectBitmap` and `ImageBitmap` in Dispose()
3. Call `(hitTarget.Tag as IDisposable)?.Dispose()` before `canvas.Children.Remove(hitTarget)`
4. Same for PerformDelete()
5. Also dispose in OnAnnotationsRestored() when clearing old controls

**Risk**: Medium - must ensure disposal doesn't break undo/redo (annotations in history mementos must NOT be disposed)

**Validation**:
- Use memory profiler to track SKBitmap allocations
- Draw 50 blur annotations → delete all → verify memory released
- Draw, delete, undo → verify restored annotation still has valid bitmap

---

### ISSUE-003: Undo/Redo - Crop/CutOut Canvas Memento Size

**Severity**: Blocker (Performance)
**File**: `EditorCore.cs:921`, `EditorHistory.cs` (assumed)
**Category**: Performance, Memory

**Description**:
Crop and CutOut operations call `_history.CreateCanvasMemento()` which stores a **full copy** of the source SKBitmap in the memento. For large images (e.g., 4K screenshot = ~8MB), each crop/cutout adds 8MB to the undo stack. With default undo limit (e.g., 20), this could consume 160MB for crop operations alone.

**Expected Behaviour**:
Memory-efficient undo for destructive operations.

**Actual Behaviour**:
Full canvas bitmap cloned into every canvas memento.

**Evidence**:
```csharp
// EditorCore.cs:920-922
// Create canvas memento before destructive crop operation
_history.CreateCanvasMemento();
```

**Root Cause**:
Crop/CutOut are destructive operations that modify `SourceImage`. To undo, the entire previous canvas must be restored. Memento pattern requires full state capture.

**Fix Plan**:
1. **Option A**: Limit undo stack depth for canvas mementos (e.g., max 5 canvas operations)
2. **Option B**: Compress canvas bitmaps in mementos (PNG encode) - trades CPU for memory
3. **Option C**: Track crop/cutout regions instead of full canvas (complex to reverse multiple crops)

**Recommended**: Option A + warning to user if image > 10MB

**Risk**: Low for Option A

**Validation**:
- Load 4K image → crop 10 times → measure memory usage
- Crop → Undo → verify full image restored
- Crop → Crop → Undo → Undo → verify original restored

---

## High Severity Issues

### ISSUE-004: Null Safety - Missing Null Checks in EditorInputController

**Severity**: High
**File**: `EditorInputController.cs` (multiple locations)
**Category**: Null Safety, Correctness

**Description**:
Multiple code paths assume non-null values without checking:
- `ViewModel` property can return null but many call sites don't check
- `FindControl<Canvas>("AnnotationCanvas")` can return null
- `_currentShape.Tag as Annotation` can be null but is dereferenced

**Evidence**:
```csharp
// EditorInputController.cs:404-405
if (_currentShape.Tag is RectangleAnnotation rectAnn) { rectAnn.StartPoint = ToSKPoint(new Point(left, top)); rectAnn.EndPoint = ToSKPoint(new Point(left + width, top + height)); }
// What if Tag is null? What if Tag is wrong type?
```

```csharp
// EditorInputController.cs:310
var vm = ViewModel;
if (vm == null) return; // Good check

// But later at line 416:
path.Data = new ArrowAnnotation().CreateArrowGeometry(_startPoint, currentPoint, vm.StrokeWidth * 3);
// vm was checked earlier but could have changed between lines 310 and 416?
```

**Root Cause**:
Inconsistent null checking discipline. Some methods check `vm == null`, others assume it's non-null after prior check.

**Fix Plan**:
1. Add null-conditional operators for all `ViewModel` accesses: `vm?.StrokeWidth ?? 4`
2. Add null guards for all `FindControl` results
3. Use pattern matching with null guards: `if (_currentShape?.Tag is RectangleAnnotation rectAnn)`

**Risk**: Low - defensive programming

**Validation**:
- Trigger all code paths with null ViewModel
- Trigger code paths with missing XAML controls (rename control in XAML)

---

### ISSUE-005: Duplication - Arrow Geometry Creation Logic

**Severity**: High
**File**: `EditorInputController.cs:416`, `EditorSelectionController.cs:325, 460`
**Category**: Duplication

**Description**:
Arrow geometry creation code `new ArrowAnnotation().CreateArrowGeometry(start, end, width)` is duplicated in 3 locations:
1. InputController during drawing (line 416)
2. SelectionController during resize (line 325)
3. SelectionController during move (line 460)

All use `vm.StrokeWidth * 3` for arrow head size.

**Expected Behaviour**:
Single method to update arrow geometry.

**Actual Behaviour**:
Copy-paste code in 3 locations. If arrow head size formula changes, must update 3 places.

**Evidence**:
```csharp
// EditorInputController.cs:416
path.Data = new ArrowAnnotation().CreateArrowGeometry(_startPoint, currentPoint, vm.StrokeWidth * 3);

// EditorSelectionController.cs:325
arrowPath.Data = new ArrowAnnotation().CreateArrowGeometry(arrowStart, arrowEnd, vm.StrokeWidth * 3);

// EditorSelectionController.cs:460
arrowPath.Data = new ArrowAnnotation().CreateArrowGeometry(newStart, newEnd, vm.StrokeWidth * 3);
```

**Root Cause**:
Lack of shared helper method.

**Fix Plan**:
Add to EditorSelectionController or shared helper class:
```csharp
private void UpdateArrowGeometry(Path arrowPath, Point start, Point end, double arrowHeadWidth)
{
    arrowPath.Data = new ArrowAnnotation().CreateArrowGeometry(start, end, arrowHeadWidth);
    _shapeEndpoints[arrowPath] = (start, end);
}
```

Replace all 3 call sites.

**Risk**: Low

**Validation**:
- Draw arrow → resize → move → verify geometry updates correctly

---

### ISSUE-006: Code Smell - Magic Number "3" for Arrow Head Size

**Severity**: Medium (upgraded to High for documentation)
**File**: `EditorInputController.cs:178, 416`, `EditorSelectionController.cs:325, 460`
**Category**: Code Smell

**Description**:
Arrow head size is calculated as `vm.StrokeWidth * 3` in multiple locations with no explanation. Why 3? This is a magic number.

**Expected Behaviour**:
Named constant with documentation.

**Actual Behaviour**:
Magic number repeated 4+ times.

**Evidence**:
```csharp
var arrowAnnotation = new ArrowAnnotation { /* ... */ };
// Later:
path.Data = new ArrowAnnotation().CreateArrowGeometry(_startPoint, currentPoint, vm.StrokeWidth * 3);
```

**Root Cause**:
No constant definition.

**Fix Plan**:
Add to ArrowAnnotation or shared constants:
```csharp
/// <summary>
/// Arrow head width is proportional to stroke width for visual balance
/// </summary>
public const double ArrowHeadWidthMultiplier = 3.0;
```

Use: `vm.StrokeWidth * ArrowAnnotation.ArrowHeadWidthMultiplier`

**Risk**: Low

**Validation**:
Visual inspection - arrow heads should look unchanged.

---

### ISSUE-007: Threading - UI Thread Dispatch Inconsistency

**Severity**: High
**File**: `EditorView.axaml.cs:72-88`, `EditorZoomController.cs:94-111`
**Category**: Threading

**Description**:
EditorCore events (`InvalidateRequested`, `ImageChanged`, `AnnotationsRestored`, `HistoryChanged`) are fired from EditorCore methods which may be called from non-UI threads. EditorView subscribes and dispatches to UI thread via `Dispatcher.UIThread.Post()`. However, EditorZoomController also uses `Dispatcher.UIThread.Post()` for scroll offset updates. No clear threading contract.

**Expected Behaviour**:
Clear contract: either EditorCore guarantees UI thread OR subscribers must dispatch.

**Actual Behaviour**:
Mixed approach - subscribers defensively dispatch.

**Evidence**:
```csharp
// EditorView.axaml.cs:72
_editorCore.InvalidateRequested += () => Avalonia.Threading.Dispatcher.UIThread.Post(RenderCore);
```

```csharp
// EditorZoomController.cs:94
Dispatcher.UIThread.Post(() => { /* offset update */ }, DispatcherPriority.Render);
```

**Root Cause**:
No documented threading contract for EditorCore.

**Fix Plan**:
1. Document in EditorCore that all events are fired on the calling thread (may not be UI thread)
2. Add `[CallerShouldDispatchToUIThread]` attribute or XML comment
3. Ensure all subscribers use `Dispatcher.UIThread.InvokeAsync()` consistently

**Risk**: Low - defensive dispatching is safe

**Validation**:
- Trigger EditorCore events from background thread → verify no crashes
- Verify all UI updates complete successfully

---

### ISSUE-008: Coordinate Mapping - DPI Scaling Inconsistency

**Severity**: High
**File**: `EditorInputController.cs:572-580, 594-596`, `EditorView.axaml.cs:687-696`
**Category**: DPI, Coordinate Mapping

**Description**:
Crop and CutOut operations apply `RenderScaling` to convert logical coordinates to physical pixels. However, effect annotations (Blur, Pixelate, Magnify) do NOT apply scaling when calling `UpdateEffect()`. This causes effects to sample wrong regions on high-DPI displays.

**Expected Behaviour**:
All coordinate conversions account for DPI scaling.

**Actual Behaviour**:
- Crop/CutOut: Apply scaling ✓
- Effect annotations: No scaling ✗

**Evidence**:
```csharp
// EditorInputController.cs:572-580 (Crop - HAS scaling)
var scaling = 1.0;
var topLevel = TopLevel.GetTopLevel(_view);
if (topLevel != null) scaling = topLevel.RenderScaling;

var physX = (int)(x * scaling);
var physY = (int)(y * scaling);
```

```csharp
// EditorInputController.cs:537-538 (Effect - NO scaling)
annotation.StartPoint = new SKPoint((float)x, (float)y);
annotation.EndPoint = new SKPoint((float)(x + width), (float)(y + height));
```

**Root Cause**:
Inconsistent application of RenderScaling.

**Fix Plan**:
1. Add `GetRenderScaling()` helper to EditorView or controller
2. Apply scaling to effect annotation bounds before `UpdateEffect()`
3. Test on 150% and 200% DPI displays

**Risk**: Medium - affects visual correctness on high-DPI displays

**Validation**:
- Set Windows scaling to 150%
- Draw blur annotation at specific coordinates
- Verify blur region matches visual selection box

---

### ISSUE-009: UX Defect - Effect Bitmaps Not Updated During Real-Time Resize in InputController

**Severity**: High
**File**: `EditorInputController.cs:402, 525-554`
**Category**: UX

**Description**:
During drawing (OnCanvasPointerMoved), effect annotations call `UpdateEffectVisual()` which updates the effect bitmap in real-time. However, the update only happens if `_isCreatingEffect` is true. After pointer release, `_isCreatingEffect` is set to false, so subsequent resizes via SelectionController do NOT update in real-time during drag - only on release.

**Expected Behaviour**:
Effect bitmaps update in real-time during both initial drawing AND resize dragging.

**Actual Behaviour**:
- Initial drawing: Real-time update ✓ (line 402)
- Resize via handles: Update only on release ✓ (SelectionController.cs:266, 429)

Actually, looking at SelectionController lines 427-430, it DOES call `RequestUpdateEffect` during resize drag. So this might be OK.

**Evidence**:
```csharp
// EditorInputController.cs:402
UpdateEffectVisual(_currentShape, left, top, width, height); // Real-time during draw
```

```csharp
// EditorSelectionController.cs:427-430
// Real-time effect update during resize
if (_selectedShape?.Tag is BaseEffectAnnotation)
{
    RequestUpdateEffect?.Invoke(_selectedShape);
}
```

**Root Cause**:
False alarm - both paths update in real-time.

**Fix Plan**:
None needed. Verify in testing.

**Risk**: N/A

**Validation**:
- Draw blur → drag resize handle → verify blur updates during drag

**DOWNGRADE TO LOW PRIORITY - VERIFICATION NEEDED**

---

### ISSUE-010: Undo/Redo - Selection State Not Persisted in Mementos

**Severity**: High
**File**: `EditorCore.cs:704-725`
**Category**: UX, Correctness

**Description**:
When RestoreState() is called during undo/redo, `_selectedAnnotation` is **always cleared** (line 720). This means:
1. User draws rectangle → auto-selected
2. User draws ellipse → auto-selected
3. User undos → ellipse removed, but rectangle is NOT re-selected (even though it was selected before ellipse was drawn)

**Expected Behaviour**:
Undo/Redo restores selection state to match the state before the undone action.

**Actual Behaviour**:
Selection always cleared on undo/redo.

**Evidence**:
```csharp
// EditorCore.cs:720
// Clear current selection as it may no longer be valid
_selectedAnnotation = null;
```

**Root Cause**:
EditorMemento does not store `_selectedAnnotation`. Clearing is defensive to avoid dangling reference.

**Fix Plan**:
1. Add `Guid? SelectedAnnotationId` to EditorMemento
2. Store `_selectedAnnotation?.Id` in memento
3. Restore selection by finding annotation with matching Id after restore
4. If not found (annotation was deleted), leave selection null

**Risk**: Low

**Validation**:
- Draw A → Draw B (B selected) → Undo → Verify A is re-selected

---

### ISSUE-011: Code Smell - InputController._cutOutDirection Mutable State

**Severity**: Medium (upgraded to High for clarity)
**File**: `EditorInputController.cs:32, 137, 336-347, 521`
**Category**: Code Smell, State Management

**Description**:
`_cutOutDirection` is a nullable bool field used to track whether the cutout is vertical or horizontal. It's set to null on press, determined during move, and used on release. However, this state is NOT reset after release, only on next press. If release is skipped (e.g., right-click cancel), state could leak.

**Expected Behaviour**:
State is cleared after use or on any cancellation path.

**Actual Behaviour**:
State cleared in `CancelActiveRegionDrawing()` (line 521) but not in `PerformCutOut()`.

**Evidence**:
```csharp
// EditorInputController.cs:521
_cutOutDirection = null; // Cleared on cancel
```

```csharp
// EditorInputController.cs:588-617 (PerformCutOut)
// ... uses _cutOutDirection ...
// NO _cutOutDirection = null; at end
```

**Root Cause**:
Incomplete cleanup in PerformCutOut.

**Fix Plan**:
Add `_cutOutDirection = null;` at end of PerformCutOut() and in any error paths.

**Risk**: Low

**Validation**:
- Start cutout → release → start new cutout → verify direction resets

---

### ISSUE-012: Missing Null Check - EditorInputController.HandleTextTool

**Severity**: High
**File**: `EditorInputController.cs:654-736`
**Category**: Null Safety

**Description**:
`HandleTextTool()` checks `vm == null` at line 657, but the closure `OnCreationLostFocus` (lines 685-720) captures `_view` which may be null if view is disposed or unloaded during text editing.

**Expected Behaviour**:
All captured variables are validated before use in closures.

**Actual Behaviour**:
`_view.EditorCore.AddAnnotation(annotation)` at line 708 assumes `_view` and `EditorCore` are non-null.

**Evidence**:
```csharp
// EditorInputController.cs:708
_view.EditorCore.AddAnnotation(annotation); // No null check
```

**Root Cause**:
Closure captures `_view` which is readonly field, assumed to be non-null. But `EditorCore` could be null.

**Fix Plan**:
```csharp
if (_view?.EditorCore != null)
{
    _view.EditorCore.AddAnnotation(annotation);
}
```

**Risk**: Low - unlikely scenario but defensive coding

**Validation**:
- Start typing text → close window mid-edit → verify no crash

---

### ISSUE-013: Duplication - Polyline Point Translation Logic

**Severity**: Medium
**File**: `EditorSelectionController.cs:492-523`
**Category**: Duplication

**Description**:
Polyline point translation code (lines 492-523) translates all points by delta. Similar logic appears for FreehandAnnotation and SmartEraserAnnotation separately. Could be unified.

**Expected Behaviour**:
Shared method for translating point collections.

**Actual Behaviour**:
Inline duplication for each annotation type.

**Evidence**:
```csharp
if (polyline.Tag is FreehandAnnotation freehand)
{
    for (int i = 0; i < freehand.Points.Count; i++)
    {
        var oldPt = freehand.Points[i];
        freehand.Points[i] = new SKPoint(oldPt.X + (float)deltaX, oldPt.Y + (float)deltaY);
    }
}
else if (polyline.Tag is SmartEraserAnnotation eraser)
{
    for (int i = 0; i < eraser.Points.Count; i++)
    {
        var oldPt = eraser.Points[i];
        eraser.Points[i] = new SKPoint(oldPt.X + (float)deltaX, oldPt.Y + (float)deltaY);
    }
}
```

**Root Cause**:
No shared base class or interface for point-based annotations.

**Fix Plan**:
1. Add `IPointBasedAnnotation` interface with `List<SKPoint> Points { get; }`
2. Implement in FreehandAnnotation and SmartEraserAnnotation
3. Replace with:
```csharp
if (polyline.Tag is IPointBasedAnnotation pointBased)
{
    for (int i = 0; i < pointBased.Points.Count; i++)
    {
        var oldPt = pointBased.Points[i];
        pointBased.Points[i] = new SKPoint(oldPt.X + (float)deltaX, oldPt.Y + (float)deltaY);
    }
}
```

**Risk**: Low

**Validation**:
- Draw freehand → move → verify points translated
- Draw smart eraser → move → verify points translated

---

### ISSUE-014: Missing Feature - No Visual Feedback for Crop/CutOut Direction

**Severity**: Medium (UX)
**File**: `EditorInputController.cs:324-378`
**Category**: UX

**Description**:
CutOut tool determines direction (vertical vs horizontal) based on drag distance. Overlay is shown ONLY after direction is determined and exceeds threshold (line 351-377). Before threshold, user has no visual feedback.

**Expected Behaviour**:
Immediate visual feedback as soon as user starts dragging.

**Actual Behaviour**:
Overlay hidden until drag exceeds 15px in one direction.

**Evidence**:
```csharp
// EditorInputController.cs:332-338
const double directionThreshold = 15;

// Reset direction if user moves back close to start point
if (deltaX < directionThreshold && deltaY < directionThreshold)
{
    _cutOutDirection = null;
    _currentShape.IsVisible = false; // Hidden
    return;
}
```

**Root Cause**:
Design choice to avoid showing overlay for accidental tiny drags.

**Fix Plan**:
Show semi-transparent overlay immediately with text hint "Drag to choose cut direction". Once threshold exceeded, solidify and show direction indicator.

**Risk**: Low - UX improvement

**Validation**:
- Start cutout → drag 5px → verify some feedback visible
- Continue drag past 15px → verify direction locks in

---

### ISSUE-015: Performance - Hover Outline Cleared and Recreated Every Frame

**Severity**: Medium
**File**: `EditorSelectionController.cs:994-1147`
**Category**: Performance

**Description**:
`UpdateHoverOutline()` is called on every pointer move. For some shape types, it clears and recreates Polyline/Ellipse/Rectangle outlines even when the shape hasn't changed position. Should be more conservative about recreation.

**Expected Behaviour**:
Outline controls reused and position updated without recreation.

**Actual Behaviour**:
New Polyline/Ellipse/Rectangle created every time `UpdateHoverOutline()` is called.

**Evidence**:
```csharp
// EditorSelectionController.cs:1020-1030
if (_hoverPolylineBlack == null)
{
    _hoverPolylineBlack = new Polyline { /* ... */ };
    overlay.Children.Add(_hoverPolylineBlack);
}
// But then:
_hoverPolylineBlack.Points = outlinePoints; // Reassigned every frame
```

Actually, the code does check `if (_hoverPolylineBlack == null)` so it's NOT recreated every frame, only when null. This is correct.

**DOWNGRADE TO LOW - FALSE ALARM**

**Root Cause**:
None - code is correct.

**Fix Plan**:
None.

**Risk**: N/A

**Validation**:
Performance profiling - hover over shapes and check allocation rate.

---

## Medium Severity Issues

### ISSUE-016: Code Smell - Hard-Coded XAML Control Names

**Severity**: Medium
**File**: `EditorInputController.cs:51, 122, 305, 453, etc.` (Many locations)
**Category**: Code Smell, Maintainability

**Description**:
XAML control names like `"AnnotationCanvas"`, `"CanvasScrollViewer"`, `"CropOverlay"`, `"OverlayCanvas"` are hard-coded as string literals in many locations. If a control is renamed in XAML, all call sites must be updated manually.

**Expected Behaviour**:
Centralized constants or nameof() usage.

**Actual Behaviour**:
Magic strings scattered throughout controllers.

**Evidence**:
```csharp
var canvas = _view.FindControl<Canvas>("AnnotationCanvas") ?? sender as Canvas;
```

**Root Cause**:
No constant definitions.

**Fix Plan**:
Add to EditorView or shared constants class:
```csharp
public static class EditorControlNames
{
    public const string AnnotationCanvas = "AnnotationCanvas";
    public const string CanvasScrollViewer = "CanvasScrollViewer";
    public const string CropOverlay = "CropOverlay";
    public const string OverlayCanvas = "OverlayCanvas";
}
```

Use: `_view.FindControl<Canvas>(EditorControlNames.AnnotationCanvas)`

**Risk**: Low

**Validation**:
- Compilation check - all references updated

---

### ISSUE-017: Missing Validation - Crop Region Can Be Outside Image Bounds

**Severity**: Medium
**File**: `EditorInputController.cs:556-586`
**Category**: Correctness

**Description**:
`PerformCrop()` clamps crop region to image bounds when calling `ViewModel.CropImage()`, but doesn't validate that the region has positive size AFTER clamping. Edge case: user draws crop box completely outside image → clamping results in 0x0 region → crash or undefined behavior in CropImage.

**Expected Behaviour**:
Validate clamped region is non-empty before calling CropImage.

**Actual Behaviour**:
Only checks `if (w <= 0 || h <= 0)` BEFORE scaling (line 566), not after.

**Evidence**:
```csharp
// EditorInputController.cs:566-570
if (w <= 0 || h <= 0)
{
    cropOverlay.IsVisible = false;
    return;
}
// No check after scaling
```

**Root Cause**:
Missing validation after coordinate transformation.

**Fix Plan**:
```csharp
if (physW <= 0 || physH <= 0)
{
    vm.StatusText = "Invalid crop region";
    cropOverlay.IsVisible = false;
    return;
}
```

**Risk**: Low

**Validation**:
- Draw crop box entirely outside image → verify graceful handling

---

### ISSUE-018: UX - No Cursor Feedback for Drawing Tools

**Severity**: Medium
**File**: `EditorInputController.cs` (entire file)
**Category**: UX

**Description**:
When a drawing tool is selected (Rectangle, Ellipse, Pen, etc.), the cursor does not change to indicate the active tool. User has no visual feedback until they start drawing.

**Expected Behaviour**:
Cursor changes to crosshair or custom cursor when drawing tool is active.

**Actual Behaviour**:
Default cursor (arrow) for all tools.

**Evidence**:
No cursor changes in EditorInputController.

**Root Cause**:
Not implemented.

**Fix Plan**:
1. Add cursor property to EditorView
2. Subscribe to ViewModel.ActiveTool changes
3. Set cursor based on tool:
   - Select → Arrow
   - Drawing tools → Crosshair
   - Crop/CutOut → Crosshair with scissors icon

**Risk**: Low - UX improvement

**Validation**:
- Select each tool → verify cursor changes

---

### ISSUE-019: Dead Code - EditorView.PushUndo() and ClearRedoStack() Are No-Ops

**Severity**: Medium
**File**: `EditorView.axaml.cs:185-193`, `EditorInputController.cs:111, 292, 644`
**Category**: Dead Code

**Description**:
EditorView has legacy methods `PushUndo()` and `ClearRedoStack()` that are called from InputController but are no-ops (lines 185-193). The actual undo/redo is handled by EditorCore.

**Expected Behaviour**:
Remove dead code or implement if needed.

**Actual Behaviour**:
Methods exist but do nothing, creating confusion.

**Evidence**:
```csharp
// EditorView.axaml.cs:185-193
internal void PushUndo(Control shape)
{
    // Legacy support: Handled by EditorCore history
}

internal void ClearRedoStack()
{
    // Legacy support: Handled by EditorCore history
}
```

```csharp
// EditorInputController.cs:111
_view.ClearRedoStack(); // Called but does nothing
```

**Root Cause**:
Legacy code not cleaned up after refactoring to EditorCore-based undo/redo.

**Fix Plan**:
Remove both methods and all call sites. Undo/redo is fully handled by EditorCore now.

**Risk**: Low - these methods do nothing

**Validation**:
- Remove methods → verify undo/redo still works

---

### ISSUE-020: Incorrect Assumption - SelectionController Assumes _selectedShape.Tag Is Annotation

**Severity**: Medium
**File**: `EditorSelectionController.cs:264, 276, 427, 548`
**Category**: Correctness

**Description**:
Multiple locations cast `_selectedShape.Tag` to `BaseEffectAnnotation` or other annotation types without validation. If Tag is null or wrong type, cast silently fails and code path is skipped. Should validate with pattern matching.

**Expected Behaviour**:
Explicit pattern matching: `if (_selectedShape.Tag is BaseEffectAnnotation effect)`

**Actual Behaviour**:
Direct cast with null-conditional: `_selectedShape?.Tag is BaseEffectAnnotation`

Actually, the code DOES use pattern matching (`is` keyword), so this is correct. False alarm.

**DOWNGRADE TO LOW - VERIFICATION NEEDED**

**Evidence**:
```csharp
// EditorSelectionController.cs:264
if (_selectedShape?.Tag is BaseEffectAnnotation)
{
    RequestUpdateEffect?.Invoke(_selectedShape);
}
```

This is correct usage of pattern matching.

**Root Cause**:
None - code is correct.

**Fix Plan**:
None.

**Risk**: N/A

**Validation**:
None needed.

---

### (Continued in next section...)

---

## Issue Counts by Category

| Category | Count |
|----------|-------|
| Null Safety | 5 |
| Duplication | 3 |
| Code Smell | 6 |
| Performance | 3 |
| Memory Management | 1 |
| Threading | 1 |
| DPI | 1 |
| UX | 4 |
| Correctness | 6 |
| Dead Code | 1 |
| Architecture | 1 |

---

## Next Steps

1. Complete review of remaining files:
   - MainViewModel.cs
   - EditorHistory.cs
   - Annotation subclasses (BaseEffectAnnotation, etc.)
   - ImageEffect classes
   - Controls (SpotlightControl, SpeechBalloonControl, etc.)

2. Prioritize fixes:
   - All Blocker issues first
   - High severity issues second
   - Medium/Low as time permits

3. Create fix batches (Phase 3)

4. Validate fixes (Phase 5)

---

**Status**: Phase 2 In Progress (Controller files reviewed, ViewModel and Annotation files pending)
