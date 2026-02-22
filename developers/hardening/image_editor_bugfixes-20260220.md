# Image Editor Hardening & Refinement Plan

This document outlines the actionable items derived from user feedback during testing of the Image Editor. The tasks are categorized by area to help prioritize and track progress.

**Verdict key:** Items kept have **merit to amend source code** (bug or valuable UX). Items removed were either already addressed, cosmetic-only, or not recommended for code change.

---

## 1. Performance & Memory Management (High Priority)
- [x] **Memory Leak Investigation:** Identify why memory usage grows from ~200MB to 1.4GB over extended use.
- [x] **Debouncing Implementations:**
  - [x] Add debounce/throttling to the HSV map color picker to prevent UI lag when changing highlight color.
  - [x] Add debounce to effect strength sliders (Magnifier/Blur) to prevent constant re-rendering.
- [x] **Tool Performance Optimizations:**
  - [x] Investigate and resolve CPU spikes and lag when moving/drawing with the Highlight and Magnifier tools.
  - [x] Optimize the Smart Eraser tool, which currently exhibits the most significant slow-downs.
- [x] **Filter Optimization:**
  - [x] Address the 4-second delay when applying the Black & White filter.
  - [x] Fix unresponsiveness and CPU spikes (up to 14%) caused by Selective Color sliders, Replace Color, Outline, Slice, and Sharpen filters.
- [x] **Canvas Rendering:** Improve canvas scaling performance when multiple drawn elements are present.

## 2. Tools & Drawing Enhancements
- [x] **Highlight Tool:**
  - [x] Fix bug where the highlight tool overwrites drawn arrows (check z-index / rendering order).
  - [x] Correct the tooltip text that mistakenly refers to the highlight color as "border color".
- [ ] **Undo functionality:** Fix undo (Ctrl+Z in code; user may have meant Alt+Z) so it undoes a single action (e.g., arrow OR highlight) instead of multiple actions. **Merit:** Investigate—one `Undo()` pops one memento; if multiple actions disappear per keypress, either key repeat or multiple mementos per logical action.
- [ ] **Tool Defaults:**
  - [x] Set a sensible default thickness for the Smart Eraser (currently blank).
  - [x] Set a sensible default color for the Text tool (currently initialized as transparent). **Done:** `EditorInputController.HandleTextTool` uses fallback (Options.TextColor or black) when selected color is transparent.
- [ ] **Color Picker:** Redesign or tweak the value picker so its functionality is immediately obvious to the user. **Merit:** UX improvement.
- [ ] **Pin Screen:** Improve the scaling feel of the pin screen functionality to make it more natural. **Merit:** UX improvement.

## 3. Image Transformations & Filters
- [ ] **Resize Image:** Fix issue where drawn elements disappear when the image gets resized. **Merit:** Confirmed—`EditorCore.ResizeImage` uses `clearAnnotations: true`; consider transforming annotations to new dimensions instead.
- [ ] **Resize Canvas:** Implement a visual preview of the canvas resize to prevent guesswork. **Merit:** Feature.
- [ ] **Rotation & Flipping:**
  - [ ] Ensure predefined Rotate/Flip actions do not cause drawn edits to disappear. **Merit:** Confirmed—`Rotate90Clockwise`, `FlipHorizontal`, etc. use `clearAnnotations: true`; transform annotations with image instead.
  - [ ] Fix Custom Rotate so that drawn edits rotate alongside the background image. **Merit:** Same as above.
- [ ] **Filters Application:**
  - [ ] Apply global image filters (Skew, Brightness, Contrast, Gamma, Alpha, etc.) to drawn objects on the canvas, not just the background layer. **Merit:** Feature.
  - [ ] Fix Selective Color sliders completely freezing the editor window. **Merit:** Perf/bug.
  - [ ] Update Replace Color to use the standard application color picker. **Merit:** Consistency (Replace Color dialog currently uses hex text, not shared picker).
- [ ] **Specific Filter Fixes:**
  - [ ] **Shadow Filter:** Investigate and fix the drop shadow effect failing to apply/render. **Merit:** Bug.
  - [ ] **Glow Filter:** Fix bug where applying glow shifts the position of all drawn objects. **Merit:** Bug.
  - [ ] **Border Filter:** Ensure borders added to the "outside" are properly retained and rendered when the image is saved. **Merit:** Verify export/composite path; `BorderImageEffect` supports Outside.

## 4. UI/UX & Workflow Features
- [x] **Window State:** Set the Image Editor window to start maximized by default for the best layout experience.
- [ ] **Export Consistency:** Ensure drop shadows applied to effects within the editor are accurately preserved when the image is exported. **Merit:** Bug (export/composite pipeline).
- [ ] **Save Feedback & State:**
  - [ ] Add visual feedback/progress indicators when the user saves an image or applies heavy operations. **Merit:** UX.
  - [ ] Contextual "Save" button: Hide or disable the "Save image" button (or provide clear "Save As" flow) if the original image came from the clipboard instead of a file. **Merit:** UX (editor can be opened from clipboard; save vs save-as semantics).
- [x] **Clarity:** Add a tooltip or clearer UI indication explaining what "Auto crop image" actually does.

---

**Removed from list (no source change recommended):**
- **Text Tool Styling (font size label to white):** Cosmetic only; monochrome aesthetic is preference. Omitted from checklist; can re-add if product decides to adopt.
