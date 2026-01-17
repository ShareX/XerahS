# ShareX.Editor Code Review - Additional Issues (MainViewModel Complete)

**Date**: 2026-01-17
**Files Reviewed**: MainViewModel.cs (lines 700-1543)
**New Issues Found**: 8

---

## Additional High Severity Issues

### ISSUE-024: Memory Management - Disposal Before Reassignment Missing

**Severity**: High
**File**: `MainViewModel.cs:1036-1037, 1313, 1466`
**Category**: Memory Management

**Description**:
Multiple locations reassign SKBitmap fields (_originalSourceImage, _preEffectImage, _rotateCustomAngleOriginalBitmap) without disposing the previous value. This causes memory leaks when fields are overwritten.

**Expected Behaviour**:
Dispose old bitmap before assigning new value.

**Actual Behaviour**:
Old bitmaps leak when reassigned.

**Evidence**:
```csharp
// MainViewModel.cs:1036-1037
_originalSourceImage?.Dispose();
_originalSourceImage = image.Copy(); // Good! But missing in other places

// MainViewModel.cs:1313 - BAD
_preEffectImage = _currentSourceImage.Copy(); // No disposal of previous _preEffectImage

// MainViewModel.cs:1466 - BAD
_rotateCustomAngleOriginalBitmap = current.Copy(); // No disposal of previous
```

**Root Cause**:
Inconsistent disposal discipline across methods.

**Fix Plan**:
Add disposal before all bitmap field assignments:
```csharp
// Line 1313
_preEffectImage?.Dispose();
_preEffectImage = _currentSourceImage.Copy();

// Line 1466
_rotateCustomAngleOriginalBitmap?.Dispose();
_rotateCustomAngleOriginalBitmap = current.Copy();
```

**Risk**: Low - defensive programming

**Validation**:
- Open effect dialog → cancel → open again → verify no leak
- Open rotate dialog → cancel → open again → verify no leak

---

### ISSUE-025: Null Safety - No Check After SKBitmap.Copy()

**Severity**: High
**File**: `MainViewModel.cs:733, 737, 758, 1036, 1067, 1092, 1128, 1142, 1156, 1170, 1184, 1205, 1226, 1245, 1287, 1313, 1349, 1422, 1466, 1502`
**Category**: Null Safety, Correctness

**Description**:
`SKBitmap.Copy()` can return null on out-of-memory conditions. Code never checks for null after calling Copy(), leading to potential NullReferenceException.

**Expected Behaviour**:
Check if Copy() returns null and handle gracefully.

**Actual Behaviour**:
Assumes Copy() always succeeds.

**Evidence**:
```csharp
// MainViewModel.cs:1067 - CropImage
_imageUndoStack.Push(_currentSourceImage.Copy()); // Can return null!

// MainViewModel.cs:1036 - UpdatePreview
_originalSourceImage = image.Copy(); // Can return null!
```

**Root Cause**:
No error handling for SKBitmap.Copy() failures.

**Fix Plan**:
Add null checks and error handling:
```csharp
var copy = _currentSourceImage.Copy();
if (copy == null)
{
    StatusText = "Error: Out of memory";
    return;
}
_imageUndoStack.Push(copy);
```

**Risk**: Medium - affects all image operations under low memory

**Validation**:
- Simulate low memory conditions
- Verify graceful failure instead of crash

---

### ISSUE-026: Use-After-Free - CancelRotateCustomAngle Disposes Bitmap After UpdatePreview

**Severity**: **Blocker**
**File**: `MainViewModel.cs:1528-1530`
**Category**: Memory Management, Correctness

**Description**:
`CancelRotateCustomAngle()` calls `UpdatePreview(_rotateCustomAngleOriginalBitmap)` which sets `_currentSourceImage = image` (line 1031), then **immediately disposes the bitmap** (line 1530). This leaves `_currentSourceImage` pointing to a freed bitmap, causing crashes on next operation.

**Expected Behaviour**:
UpdatePreview takes ownership of bitmap, so caller should NOT dispose.

**Actual Behaviour**:
Bitmap is disposed after being transferred to _currentSourceImage.

**Evidence**:
```csharp
// MainViewModel.cs:1528-1530
UpdatePreview(_rotateCustomAngleOriginalBitmap, clearAnnotations: false);
// UpdatePreview sets _currentSourceImage = _rotateCustomAngleOriginalBitmap

_rotateCustomAngleOriginalBitmap.Dispose(); // FREES THE BITMAP
_rotateCustomAngleOriginalBitmap = null;
// Now _currentSourceImage points to freed memory!
```

**Root Cause**:
Unclear ownership semantics for UpdatePreview(). Does it take ownership or make a copy?

Looking at UpdatePreview (line 1031): `_currentSourceImage = image;` - it DOES take ownership.

**Fix Plan**:
Remove the dispose call in CancelRotateCustomAngle:
```csharp
UpdatePreview(_rotateCustomAngleOriginalBitmap, clearAnnotations: false);
_rotateCustomAngleOriginalBitmap = null; // Transfer ownership, don't dispose
```

**Risk**: **CRITICAL** - causes use-after-free crash

**Validation**:
- Open rotate dialog → change angle → cancel → try to crop → verify no crash

---

### ISSUE-027: Confusing Ownership - UpdatePreview Parameter Semantics

**Severity**: High
**File**: `MainViewModel.cs:1028-1052`
**Category**: Design, Documentation

**Description**:
`UpdatePreview(SKBitmap image, ...)` is unclear about ownership. Line 1031 sets `_currentSourceImage = image` (takes ownership), but line 1036-1037 creates a copy for `_originalSourceImage`. Callers are confused about whether they should dispose the bitmap after calling UpdatePreview.

**Expected Behaviour**:
Clear documentation or consistent ownership transfer (e.g., always take ownership and copy internally if needed).

**Actual Behaviour**:
- Line 1031: Takes ownership (assigns directly)
- Line 1037: Makes a copy (allocates new memory)
- Callers don't know if they should dispose

**Evidence**:
```csharp
// MainViewModel.cs:1028-1031
public void UpdatePreview(SkiaSharp.SKBitmap image, bool clearAnnotations = true)
{
    // Store source image for operations like Crop
    _currentSourceImage = image; // TAKES OWNERSHIP

// MainViewModel.cs:1036-1037
_originalSourceImage?.Dispose();
_originalSourceImage = image.Copy(); // MAKES A COPY

// Caller examples:
// Line 1071: UpdatePreview(cropped, ...) - transfers ownership, good
// Line 1528: UpdatePreview(bitmap, ...) then disposes bitmap - BAD (ISSUE-026)
```

**Root Cause**:
No XML doc comment specifying ownership contract.

**Fix Plan**:
Add clear documentation:
```csharp
/// <summary>
/// Updates the preview image. TAKES OWNERSHIP of the bitmap parameter.
/// Caller must NOT dispose the bitmap after calling this method.
/// </summary>
/// <param name="image">Image bitmap (ownership transferred to ViewModel)</param>
/// <param name="clearAnnotations">Whether to clear annotations</param>
public void UpdatePreview(SKBitmap image, bool clearAnnotations = true)
```

**Risk**: Low - documentation only

**Validation**:
- Review all UpdatePreview call sites for correct ownership transfer

---

## Additional Medium Severity Issues

### ISSUE-028: Duplication - Duplicate ApplyEffect Method Overloads

**Severity**: Medium
**File**: `MainViewModel.cs:1339, 1418`
**Category**: Duplication, Design

**Description**:
Two `ApplyEffect` methods with different signatures but similar logic:
1. `ApplyEffect(SKBitmap result, string statusMessage)` - lines 1339-1363
2. `ApplyEffect(Func<SKBitmap, SKBitmap> effect, string statusMessage)` - lines 1418-1442

Both do the same thing: push to undo stack, update preview, clear _preEffectImage, restore background effects.

**Expected Behaviour**:
Single method or clear differentiation of purpose.

**Actual Behaviour**:
Duplication of undo/redo logic and state cleanup.

**Evidence**:
```csharp
// Lines 1339-1363
public void ApplyEffect(SkiaSharp.SKBitmap result, string statusMessage)
{
    // ... undo stack push ...
    UpdatePreview(result, clearAnnotations: true);
    _preEffectImage?.Dispose();
    _preEffectImage = null;
    _isPreviewingEffect = false;
    // ... background effects restore ...
}

// Lines 1418-1442
public void ApplyEffect(Func<SkiaSharp.SKBitmap, SkiaSharp.SKBitmap> effect, string statusMessage)
{
    // ... undo stack push ...
    var result = effect(_preEffectImage);
    UpdatePreview(result, clearAnnotations: true);
    _preEffectImage?.Dispose();
    _preEffectImage = null;
    _isPreviewingEffect = false;
    // ... background effects restore ...
}
```

**Root Cause**:
Two different calling conventions (pass result vs pass effect function).

**Fix Plan**:
Extract common logic into private method:
```csharp
private void CommitEffectAndCleanup(SKBitmap result, string statusMessage)
{
    _imageUndoStack.Push(_currentSourceImage.Copy());
    _imageRedoStack.Clear();
    UpdatePreview(result, clearAnnotations: true);
    UpdateUndoRedoProperties();
    StatusText = statusMessage;
    _preEffectImage?.Dispose();
    _preEffectImage = null;
    _isPreviewingEffect = false;
    OnPropertyChanged(nameof(AreBackgroundEffectsActive));
    UpdateCanvasProperties();
    ApplySmartPaddingCrop();
}
```

Then both overloads call this helper.

**Risk**: Low - refactoring only

**Validation**:
- Apply brightness effect → verify works
- Apply custom rotate → verify works

---

### ISSUE-029: Memory Leak - PreviewEffect Disposes Result Bitmap Too Early

**Severity**: Medium (needs verification)
**File**: `MainViewModel.cs:1406-1407`
**Category**: Memory Management

**Description**:
`PreviewEffect()` converts SKBitmap to Avalonia Bitmap via `ToAvaloniBitmap()`, then immediately disposes the source SKBitmap. Need to verify that ToAvaloniBitmap creates a copy (safe) vs wraps the SKBitmap (use-after-free).

**Expected Behaviour**:
If ToAvaloniBitmap creates a copy, disposal is correct.
If ToAvaloniBitmap wraps/references the SKBitmap, disposal causes corruption.

**Actual Behaviour**:
Unknown without inspecting BitmapConversionHelpers.ToAvaloniBitmap implementation.

**Evidence**:
```csharp
// MainViewModel.cs:1406-1407
PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(result);
result.Dispose();
```

**Root Cause**:
Unclear ownership contract for ToAvaloniBitmap.

**Fix Plan**:
1. Inspect BitmapConversionHelpers.ToAvaloniBitmap implementation
2. If it creates a copy (WriteableBitmap.FromPixelBuffer), disposal is safe
3. If it wraps, remove disposal or change to async disposal after rendering

**Risk**: Medium - could cause visual corruption if wrapping

**Validation**:
- Apply brightness effect with live preview → drag slider rapidly → check for corruption
- Memory profiler → verify bitmaps are released

---

### ISSUE-030: Missing Disposal - Clear() Command Doesn't Dispose Image Stacks

**Severity**: Medium
**File**: `MainViewModel.cs:811-823`
**Category**: Memory Management

**Description**:
`Clear()` command sets image references to null but doesn't dispose bitmaps in `_imageUndoStack` and `_imageRedoStack`. These stacks can contain many full-size image copies.

**Expected Behaviour**:
Dispose all bitmaps in undo/redo stacks before clearing.

**Actual Behaviour**:
Stacks are cleared without disposing contents (relies on GC finalizer).

**Evidence**:
```csharp
// MainViewModel.cs:811-823
private void Clear()
{
    PreviewImage = null;
    _currentSourceImage = null; // Not disposed!
    _originalSourceImage = null; // Not disposed!
    // _imageUndoStack not cleared!
    // _imageRedoStack not cleared!
    ImageDimensions = "No image";
    StatusText = "Ready";
    ResetNumberCounter();
    ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
}
```

**Root Cause**:
Missing cleanup logic.

**Fix Plan**:
```csharp
private void Clear()
{
    PreviewImage = null;

    _currentSourceImage?.Dispose();
    _currentSourceImage = null;

    _originalSourceImage?.Dispose();
    _originalSourceImage = null;

    // Clear undo/redo stacks with disposal
    while (_imageUndoStack.Count > 0)
    {
        _imageUndoStack.Pop()?.Dispose();
    }
    while (_imageRedoStack.Count > 0)
    {
        _imageRedoStack.Pop()?.Dispose();
    }

    ImageDimensions = "No image";
    StatusText = "Ready";
    ResetNumberCounter();
    ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
}
```

**Risk**: Low

**Validation**:
- Load image → crop 5 times → click Clear → verify memory released

---

### ISSUE-031: Missing Disposal - Undo/Redo Creates Copies Without Disposing Originals

**Severity**: Medium
**File**: `MainViewModel.cs:733, 758`
**Category**: Memory Management

**Description**:
`Undo()` and `Redo()` call `_currentSourceImage.Copy()` to save to the opposite stack (line 733, 758), but they don't dispose `_currentSourceImage` after `UpdatePreview()` replaces it.

**Expected Behaviour**:
UpdatePreview takes ownership of new bitmap, old _currentSourceImage should be disposed.

**Actual Behaviour**:
Old _currentSourceImage is leaked when replaced by UpdatePreview.

**Evidence**:
```csharp
// MainViewModel.cs:731-738
if (_currentSourceImage != null)
{
    _imageRedoStack.Push(_currentSourceImage.Copy()); // Saves copy to redo
}
var previousImage = _imageUndoStack.Pop();
UpdatePreview(previousImage, clearAnnotations: false);
// OLD _currentSourceImage is now leaked (replaced by previousImage in UpdatePreview line 1031)
```

**Root Cause**:
UpdatePreview doesn't dispose old _currentSourceImage before replacing.

**Fix Plan**:
Modify UpdatePreview to dispose old _currentSourceImage:
```csharp
public void UpdatePreview(SKBitmap image, bool clearAnnotations = true)
{
    // Dispose old image before replacing
    if (_currentSourceImage != null && _currentSourceImage != image)
    {
        _currentSourceImage.Dispose();
    }
    _currentSourceImage = image;
    // ... rest of method ...
}
```

**Risk**: Medium - affects all image operations

**Validation**:
- Load image → crop → undo → redo → verify no leaks
- Memory profiler during undo/redo cycle

---

## Summary of New Issues

| ID | Severity | Category | Fix Complexity |
|----|----------|----------|----------------|
| ISSUE-024 | High | Memory Management | Low |
| ISSUE-025 | High | Null Safety | Medium |
| **ISSUE-026** | **Blocker** | Use-After-Free | **Low** |
| ISSUE-027 | High | Design/Documentation | Low |
| ISSUE-028 | Medium | Duplication | Low |
| ISSUE-029 | Medium | Memory Management (verify) | Medium |
| ISSUE-030 | Medium | Memory Management | Low |
| ISSUE-031 | Medium | Memory Management | Medium |

---

## Updated Issue Counts

**Total Issues**: 31 (23 previous + 8 new)

| Severity | Count |
|----------|-------|
| Blocker  | 4 (3 fixed + 1 new) |
| High     | 16 (12 previous + 4 new) |
| Medium   | 11 (8 previous + 3 new + verification) |
| Low      | 0 |

---

## Next Actions

1. ✅ **Fix ISSUE-026 immediately** - Use-after-free blocker in CancelRotateCustomAngle
2. Review BitmapConversionHelpers.ToAvaloniBitmap to verify ISSUE-029
3. Add disposal logic to UpdatePreview for ISSUE-031
4. Add null checks after all .Copy() calls for ISSUE-025
5. Document UpdatePreview ownership for ISSUE-027

**Status**: MainViewModel review complete. Ready to create fix batches.
