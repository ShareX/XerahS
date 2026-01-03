# CP02: Implement Dark Theme

## Priority
**HIGH** - UI Polish

## Assignee
**Copilot** (Surface Laptop 7, VS2026 IDE)

## Branch
`feature/dark-theme`

## Instructions
**CRITICAL**: You must START by creating (or checking out if it exists) the branch `feature/dark-theme`. Do not work on `main`.

## Objective
Implement a dedicated Dark Theme for the application, matching the specific color palette provided. This involves creating a resource dictionary, defining colors/brushes, and wiring it into the application.

## Scope

### 1. Theme Resources
- Create directory: `src/App/Themes/Dark/` (Adjust path to actual UI project location, likely `src/ShareX.Avalonia.UI/Themes/Dark/` or similar).
  * *Note*: If `App.axaml` is in `ShareX.Avalonia`, put themes there or in UI project. Verify `App.axaml` location first.
- Create `DarkTheme.axaml` (ResourceDictionary).
- Define these **Color** and **SolidColorBrush** resources:
  - `BackgroundColor`: `#272727`
  - `LightBackgroundColor`: `#2E2E2E`
  - `DarkBackgroundColor`: `#222222`
  - `TextColor`: `#E7E9EA`
  - `BorderColor`: `#1F1F1F`
  - `CheckerColor`: `#2E2E2E`
  - `CheckerColor2`: `#272727`
  - `LinkColor`: `#A6D4FF`
  - `MenuHighlightColor`: `#2E2E2E`
  - `MenuHighlightBorderColor`: `#3F3F3F`
  - `MenuBorderColor`: `#3F3F3F`
  - `MenuCheckBackgroundColor`: `#333333`
  - `SeparatorLightColor`: `#2C2C2C`
  - `SeparatorDarkColor`: `#1F1F1F`
- Define `CheckerSize` (Double) = `15`.
- Define Global Typography:
  - `FontFamily`: `Segoe UI`
  - `FontSize`: `13`

### 2. Application Wiring
- Merge `DarkTheme.axaml` into `App.axaml` (`Application.Styles`).
- Ensure it loads *before* control-specific styles.

### 3. Control Styles
Apply base styles using the new resources:
- **Window/Main Panels**: `BackgroundColor`
- **Inputs/Tool Surfaces**: `DarkBackgroundColor` or `LightBackgroundColor` (depth dependent)
- **TextBlock/ContentPresenter**: Foreground = `TextColor`
- **Borders**: BorderBrush = `BorderColor`
- **Separator**: Use `SeparatorLightColor` (top) and `SeparatorDarkColor` (bottom) for 3D effect if needed, or flat.
- **Links**: `LinkColor` (ensure hover states work).
- **Menus/ContextMenus**:
  - Background: `BackgroundColor`
  - Border: `MenuBorderColor`
  - Highlight: `MenuHighlightColor` (Background) / `MenuHighlightBorderColor` (Border)
  - Check Background: `MenuCheckBackgroundColor`
  - Opacity: `1.0`

### 4. Utilities
- Create a `CheckerBrush` (VisualBrush/DrawingBrush) using `CheckerColor`, `CheckerColor2`, and `CheckerSize`.

### 5. Update Existing Controls
- Refactor existing hardcoded colors to use `{DynamicResource ...}`.

### 6. Verification
- Create a temporary visual verification view (e.g., `ThemeShowcaseView.axaml`) displaying:
  - Text examples
  - Inputs
  - Borders
  - Separators
  - Links
  - Menus
  - Checkerboard surface
- Verify contrast and match against requirements.

## Deliverables
- `DarkTheme.axaml`
- Updated `App.axaml`
- Verification View
