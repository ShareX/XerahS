# Image Editor Hardening & Refinement Plan

This document outlines the actionable items derived from user feedback during testing of the Image Editor. The tasks are categorized by area to help prioritize and track progress.

## 1. Performance & Memory Management (High Priority)
- [ ] **Memory Leak Investigation:** Identify why memory usage grows from ~200MB to 1.4GB over extended use.
- [ ] **Debouncing Implementations:**
  - [ ] Add debounce/throttling to the HSV map color picker to prevent UI lag when changing highlight color.
  - [ ] Add debounce to effect strength sliders (Magnifier/Blur) to prevent constant re-rendering.
- [ ] **Tool Performance Optimizations:**
  - [ ] Investigate and resolve CPU spikes and lag when moving/drawing with the Highlight and Magnifier tools.
  - [ ] Optimize the Smart Eraser tool, which currently exhibits the most significant slow-downs.
- [ ] **Filter Optimization:**
  - [ ] Address the 4-second delay when applying the Black & White filter.
  - [ ] Fix unresponsiveness and CPU spikes (up to 14%) caused by Selective Color sliders, Replace Color, Outline, Slice, and Sharpen filters.
- [ ] **Canvas Rendering:** Improve canvas scaling performance when multiple drawn elements are present.

## 2. Tools & Drawing Enhancements
- [ ] **Highlight Tool:**
  - [ ] Fix bug where the highlight tool overwrites drawn arrows (check z-index / rendering order).
  - [ ] Correct the tooltip text that mistakenly refers to the highlight color as "border color".
- [ ] **Undo functionality:** Fix `Alt + Z` behavior so it undoes a single action (e.g., arrow OR highlight) instead of multiple actions simultaneously.
- [ ] **Tool Defaults:**
  - [ ] Set a sensible default thickness for the Smart Eraser (currently blank).
  - [ ] Set a sensible default color for the Text tool (currently initialized as transparent).
- [ ] **Text Tool Styling:** Change the font size label to white to match the editor's monochrome aesthetic.
- [ ] **Color Picker:** Redesign or tweak the value picker so its functionality is immediately obvious to the user.
- [ ] **Pin Screen:** Improve the scaling feel of the pin screen functionality to make it more natural.

## 3. Image Transformations & Filters
- [ ] **Resize Image:** Fix issue where drawn elements disappear when the image gets resized.
- [ ] **Resize Canvas:** Implement a visual preview of the canvas resize to prevent guesswork.
- [ ] **Rotation & Flipping:**
  - [ ] Ensure predefined Rotate/Flip actions do not cause drawn edits to disappear.
  - [ ] Fix Custom Rotate so that drawn edits rotate alongside the background image.
- [ ] **Filters Application:**
  - [ ] Apply global image filters (Skew, Brightness, Contrast, Gamma, Alpha, etc.) to drawn objects on the canvas, not just the background layer.
  - [ ] Fix Selective Color sliders completely freezing the editor window.
  - [ ] Update Replace Color to use the standard application color picker.
- [ ] **Specific Filter Fixes:**
  - [ ] **Shadow Filter:** Investigate and fix the drop shadow effect failing to apply/render.
  - [ ] **Glow Filter:** Fix bug where applying glow shifts the position of all drawn objects.
  - [ ] **Border Filter:** Ensure borders added to the "outside" are properly retained and rendered when the image is saved.

## 4. UI/UX & Workflow Features
- [ ] **Window State:** Set the Image Editor window to start maximized by default for the best layout experience.
- [ ] **Export Consistency:** Ensure drop shadows applied to effects within the editor are accurately preserved when the image is exported.
- [ ] **Save Feedback & State:**
  - [ ] Add visual feedback/progress indicators when the user saves an image or applies heavy operations.
  - [ ] Contextual "Save" button: Hide or disable the "Save image" button (or provide clear "Save As" flow) if the original image came from the clipboard instead of a file.
- [ ] **Clarity:** Add a tooltip or clearer UI indication explaining what "Auto crop image" actually does.
