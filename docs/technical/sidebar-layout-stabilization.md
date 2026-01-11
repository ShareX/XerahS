# Stabilize Sidebar Layout

The user reports that the editor sidebar looks "tighter" on the right when the scrollbar is hidden, but symmetrical when it is visible. This contradicts the expected behavior of the previous fix (which enforced 16px spacing). A likely cause is that the `StackPanel` layout allows content to flow into the right gutter in a way that visually unbalances the controls (e.g., slider thumb positions or text alignment).

To fix this and guarantee visual symmetry:
I will replace the manual `Margin` approach with a structured `Grid` layout that enforces equal "gutters" on both sides.

## User Review Required

> [!NOTE]
> This change introduces a `Grid` with `ColumnDefinitions="16,*,16"` to strictly contain the sidebar settings. The content will live in the center column. The scrollbar (when visible) will float over the right 16px column. This ensures the available width for controls is always `TotalWidth - 32px`, keeping them perfectly centered.

## Proposed Changes

### ShareX.Editor

#### [MODIFY] [EditorView.axaml](file:///c:/Users/liveu/source/repos/ShareX Team/ShareX.Editor/src/ShareX.Editor/Views/EditorView.axaml)

-   Replace the `Margin="0,0,16,0"` on the sidebar `StackPanel` (Line 259) with a new structural parent `Grid`.
-   Wrap the content `StackPanel` in a `Grid` with:
    -   `ColumnDefinitions="16,*,16"`
    -   Content `StackPanel` placed in `Grid.Column="1"`.
-   Remove the custom right padding from the container `Border` (Line 254) if needed (currently `16,16,0,16` - I will adjust this to `16,16,0,16` and handle the left padding in the Grid as well? No, current Border has Left Padding 16.
    -   **Correction**: The `Border` currently has `Padding="16,16,0,16"`.
    -   I should remove the Left padding from the Border as well to make the Grid fully responsible for horizontal spacing.
    -   New Border Padding: `Padding="0,16,0,16"`.
    -   New Grid: `ColumnDefinitions="16,*,16"`.
    -   Content in Column 1.
    -   Result: Content is exactly in the middle. Right 16px is empty (for scrollbar). Left 16px is distinct empty.

## Verification Plan

### Manual Verification
1.  **Visual Inspection**:
    -   Open Editor Sidebar.
    -   Verify controls look centered.
    -   Verify left spacing matches right spacing visually.
    -   Trigger scrollbar (resize window/expand items).
    -   Verify scrollbar appears in the right buffer zone.
    -   Verify content does not shift or jump (it might shift if scrollbar takes space, but with overlay it shouldn't).
    -   Confirm symmetry is maintained in both states.
