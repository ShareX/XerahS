# XIP-0023: Annotation Toolbar Refactor for Overlay Support

## Summary
Refactor the existing Annotation Toolbar from `EditorView.axaml` into two reusable controls: `AnnotationToolbar` (for standard annotations) and `EditorToolsPanel` (for Editor-specific tools). This separation allows the `AnnotationToolbar` to be used in the Region Capture Overlay, enabling users to annotate the screen directly during capture.

## Motivation
The user intends to "annotate while taking a screenshot". This requires the annotation tools to be available in the Region Capture Overlay window. The current toolbar is tightly coupled to the Image Editor and contains tools (like Crop, Background, Effects) that are redundant or confusing in a capture context. 

## Design

### 1. `AnnotationToolbar` Control
A new UserControl `ShareX.Editor/Views/Controls/AnnotationToolbar.axaml` will be created.
- **Scope**: Contains only "High" and "Medium" feasibility tools (Drawing, Properties, Actions).
- **Structure**: Horizontal StackPanel with a `ContentPresenter` slot (`ExtensionContent`) to allow other controls to be injected.
- **Usage**:
  - **Overlay**: `AnnotationToolbar` used as-is.
  - **Editor**: `AnnotationToolbar` with `EditorToolsPanel` injected into the slot.

### 2. `EditorToolsPanel` Control
A new UserControl `ShareX.Editor/Views/Controls/EditorToolsPanel.axaml` will be created.
- **Scope**: Contains tools excluded from the Overlay (Crop, CutOut, Background, Effects).
- **Usage**: Typically injected into the `AnnotationToolbar`'s slot when in Editor mode.

## Feasibility Legend
- **High**: Feature works cleanly in Overlay with standard implementation.
- **Medium**: Feature is feasible but requires access to the underlying screen snapshot (e.g., Blur needs pixels to blur). Since `RegionCaptureControl` can provide a background bitmap, these are **feasible to implement**.
- **N/A**: Feature is excluded from Overlay (redundant or unwanted).

## Tool Feasibility Assessment

### Included Tools (AnnotationToolbar)
These tools will be moved to the reusable `AnnotationToolbar`.

| Icon | Name | Feasibility | Reasoning |
| :--- | :--- | :--- | :--- |
| **Select** | Select | **High** | Essential for interaction. |
| **Rectangle** | Rectangle | **High** | Vector shape; easy to render. |
| **Ellipse** | Ellipse | **High** | Vector shape; easy to render. |
| **Line** | Line | **High** | Vector shape; easy to render. |
| **Arrow** | Arrow | **High** | Vector shape; easy to render. |
| **Pen** | Freehand | **High** | Vector path; easy to render. |
| **Highlighter** | Highlighter | **High** | Semi-transparent path; easy to render. |
| **Text** | Text | **High** | Standard text control. |
| **SpeechBalloon**| Speech Balloon| **High** | Vector shape. |
| **Number** | Step Number | **High** | Vector group. |
| **Blur** | Blur | **Medium** | **Feasible**. Requires `_backgroundBitmap` from RegionCapture to render the blurred area. |
| **Pixelate** | Pixelate | **Medium** | **Feasible**. Same requirement as Blur. |
| **Magnify** | Magnify | **Medium** | **Feasible**. Same requirement as Blur. |
| **Spotlight** | Spotlight | **Medium** | **Feasible**. Same requirement as Blur. |
| **SmartEraser** | Smart Eraser | **Medium** | **Feasible**. Requires `Inpaint` logic and background bitmap. Heavy dependency but possible. |
| **Color** | Border Color | **High** | Property control. |
| **Fill** | Fill Color | **High** | Property control. |
| **Width** | Thickness | **High** | Property control. |
| **Font** | Font Size | **High** | Property control. |
| **Strength** | Effect Strength| **High** | Property control. |
| **Shadow** | Shadow Toggle| **High** | Property control. |
| **Undo** | Undo | **High** | Essential action. |
| **Redo** | Redo | **High** | Essential action. |
| **Delete** | Delete | **High** | Essential action. |
| **Clear** | Clear | **High** | Essential action. |

### Excluded Tools (EditorToolsPanel)
These tools will be moved to `EditorToolsPanel` and **excluded** from the Overlay.

| Icon | Name | Feasibility | Reasoning |
| :--- | :--- | :--- | :--- |
| **Crop** | Crop | **N/A** | The Region Capture *is* the crop tool. Redundant. |
| **CutOut** | Cut Out | **N/A** | Modifies the canvas size/content in ways confusing for capture selection. |
| **Background** | Background | **N/A** | Overlay is transparent; background settings don't apply. |
| **Effects** | Effects Menu | **N/A** | Explicitly requested to be removed; applying post-process effects during capture isn't primary workflow. |

## Region Capture Integration Plan

### Positioning
The `AnnotationToolbar` will be placed in the `OverlayWindow`.
- **Vertical Alignment**: Top.
- **Margin**: `0, 50, 0, 0`.
  - The "Click and drag to select a region..." text is centered at the top (~12px Y-pos).
  - Placing the toolbar at 50px ensures it sits cleanly **below** the instruction text without overlap.

### Composition
- **Overlay Window XAML**:
  ```xml
  <Grid>
      <RegionCaptureControl IsHitTestVisible="True" ... />
      <!-- Toolbar sits on top, below text -->
      <controls:AnnotationToolbar VerticalAlignment="Top" Margin="0,50,0,0" />
  </Grid>
  ```
- **Rendering**:
  - The `RegionCaptureControl` continues to handle the selection rect and dimming.
  - The `AnnotationToolbar` creates annotations.
  - A rendering layer (Canvas) must be added to visualize these annotations.
  - Upon capture completion, the `RegionCaptureOrchestrator` must composite the annotations onto the captured image.

## Implementation Steps
1.  **Refactor**: Create `AnnotationToolbar` and `EditorToolsPanel` from existing `EditorView` XAML.
2.  **Binding**: Ensure `AnnotationToolbar` binds to a generic interface/ViewModel property set for tools.
3.  **Overlay Update**: Add `AnnotationToolbar` to `OverlayWindow.axaml` at the specified position.
4.  **Snapshots**: Ensure `RegionCaptureControl` passes the screen snapshot (`_backgroundBitmap`) to the annotation system to support Blur/Pixelate/SmartEraser.
