# ShareX.Avalonia Reimagined UI Specification

## Full Application Structure

```
┌──────────────────────────────────────────────────────────────────┐
│ TITLE BAR (40px height, glass effect)                           │
│ [📷 ShareX]                                       [─][□][✕]    │
├─────────────────┬────────────────────────────────────────────────┤
│ NAVIGATION      │ CAPTURE TOOLBAR (glass)                        │
│ [≡]             │ [Function Buttons...]                     [⚙]  │
│ [Editor]        ├────────────────────────────────────────────────┤
│ [History]       │ ANNOTATION TOOLBAR (shown when screenshot)     │
│ [Workflows]     │ [Tools] [Colors] [Widths]                      │
│                 ├──────────────────────────────────────┬─────────┤
│                 │                                      │ SETTINGS│
│                 │                                      │ PANEL   │
│                 │   CANVAS (flex-1)                    │ (Right) │
│ [Settings]      │   with padding/effects               │ (288px) │
│                 │                                      │         │
│                 │                                      │         │
│                 └──────────────────────────────────────┴─────────┘
├─────────────────┼────────────────────────────────────────────────┤
│                 │ EXPORT TOOLBAR (bottom)                        │
├─────────────────┴────────────────────────────────────────────────┤
│ STATUS BAR (glass, always shown)                                │
└──────────────────────────────────────────────────────────────────┘
```

## 3.0 Navigation Architecture (Added Phase 7)
**Component**: `FluentAvalonia.UI.Controls.NavigationView`  
**Purpose**: Primary Shell for the application, organizing top-level views.  
**Style**: SnapX-inspired `NavigationViewItem` implementation.  
**Position**: Left Sidebar (Expanded/Compact).

### Structure
1.  **Header**: (Optional) App Logo/Title if not in TitleBar.
2.  **Menu Items**:
    - **Editor** (Current Screenshot View): The default view.
    - **History**: Gallery of previous captures.
    - **Workflows**: Task configuration (Capture -> Upload -> Share).
    - **Tools**: Access to extra utilities (Color Picker, DNS, etc.).
3.  **Footer**:
    - **Settings**: global application settings.

### Integration
- The current **Main Content Area** (Editor + Right Settings Panel + Toolbars) is hosted inside the `Editor` Page of the NavigationView.
- Transitions between pages should use modern animations.

## Component Breakdown

### 1. Title Bar
**Position**: Top, 40px height  
**Style**: Glass effect, draggable, custom window controls  
**Contents**:
- Left: App icon (Camera, violet-400) + "ShareX" title
- Right: Minimize, Maximize, Close buttons (44px wide each)
**Special**: Close button checks config for "close to tray" behavior

### 2. Capture Toolbar
**Position**: Below title bar  
**Style**: Glass, padding 16px horizontal, 12px vertical  
**Contents** (Left to Right):
1. **Capture Buttons**:
   - Fullscreen (Violet/Purple gradient)
   - Region (Cyan/Teal gradient)
   - Window (Pink/Rose gradient)
   - Clipboard (Green/Emerald gradient)
   - Import (Amber/Orange gradient)
2. **Divider** + **Clear Button** (outline style, only shown when screenshot exists)
3. **Spacer** (flex-1)
4. **Settings Button** (icon-only, right side)
5. **Minimize to Tray** (icon-only, ChevronDown, right side)

### 3. Annotation Toolbar
**Position**: Below capture toolbar  
**Style**: Glass-light, padding 12px horizontal, 8px vertical  
**Visibility**: Only shown when `screenshot exists` AND `!cropMode`  
**Contents** (Left to Right):

#### Section 1: Tools (Border Right)
9 tool buttons with keyboard shortcuts:
- Select (V), Rectangle (R), Ellipse (E)
- Arrow (A), Line (L), Text (T)
- Number (N), Spotlight (S), Crop (C)

#### Section 2: Color Picker (Border Right)
- Label: "Color"
- 12 color swatches (24px × 24px each)
- Ring selection effect for active color

**Color Palette**:
```
#ef4444 (Red), #f97316 (Orange), #eab308 (Yellow), #22c55e (Green)
#0ea5e9 (Blue), #6366f1 (Indigo), #a855f7 (Purple), #ec4899 (Pink)
#ffffff (White), #000000 (Black), #64748b (Gray), #1e293b (Dark)
```

#### Section 3: Contextual Controls (Border Right)
**Default** (Non-text tools):
- Label: "Width"
- 5 stroke width buttons (2, 4, 6, 8, 10px)
- Visual circles showing size

**Text Tool Active or Text Selected**:
- Label: "Size"
- Font size dropdown (16, 24, 32, 48, 64, 80, 96px)
- Bold button (icon)
- Italic button (icon)

**Arrow Selected**:
- Shows "Curved" toggle button

#### Section 4: Undo/Redo (Border Right)
- Undo button (disabled when !canUndo)
- Redo button (disabled when !canRedo)

#### Section 5: Delete
- Delete button (rose color)
- Disabled when no selection

### 4. Crop Toolbar
**Position**: Centered below annotation toolbar  
**Style**: Semi-transparent backdrop blur, rounded, padding 16px horizontal, 8px vertical  
**Visibility**: Only shown when `cropMode === true`  
**Contents**:
- Aspect Ratio label + 6 buttons (Free, 16:9, 4:3, 1:1, 9:16, 3:4)
- Divider
- Reset button (red, disabled when !canReset)
- Cancel button
- Apply button (green, disabled when !canApply)
- Hint text: "Press Esc to cancel"

### 5. Main Content Area
**Position**: Flex-1 row container  
**Layout**: Flex overflow-hidden

#### 5a. Editor Canvas
**Position**: Flex-1 (takes remaining space)  
**Contents**:
- Canvas with layers
- Background (gradient/solid/image)
- Screenshot with effects (padding, corner radius, shadow)
- Annotations overlay
- Spotlight overlay (dims outside regions)
- Crop overlay (selection rectangle)

**Features**:
- Zoom indicator (top-left when zoomed)
- Pan/zoom support (Space+drag, mouse wheel)
- Auto-fit to container

#### 5b. Settings Panel
**Position**: Right side, fixed width  
**Style**: 288px width, glass, padding 16px, vertical scroll  
**Visibility**: Only shown when `screenshot exists`  
**Contents** (Top to Bottom):

1. **Header**: "Settings" (gradient text)

2. **Padding Slider** (margin-bottom 24px):
   - Label + value badge (violet)
   - Range: 0 to `Math.floor(Math.min(width, height) / 3)`

3. **Corner Radius Slider** (margin-bottom 24px):
   - Label + value badge (cyan)
   - Range: 0-200px
   - Disabled when `!showBackground` (opacity 50%)

4. **Shadow Blur Slider** (margin-bottom 24px):
   - Label + value badge (pink)
   - Range: 0-100px

5. **Output Ratio** (margin-bottom 24px):
   - Label
   - 3-column grid with 9 buttons:
     - Auto, 1:1, 4:3, 3:2
     - 16:9, 5:3, 9:16, 3:4, 2:3
   - Violet gradient for selected
   - Disabled when `!showBackground`

6. **Background Toggle** (margin-bottom 24px):
   - Label "Background" + toggle button
   - Eye/EyeOff icon
   - Shows "Visible" or "Hidden"

7. **Gradient Presets** (margin-bottom 24px):
   - Label: "Gradient Presets"
   - 4-column grid with 24 gradients (6 rows)
   - Ring effect for selected
   - Disabled when `!showBackground`

**24 Gradient Presets**:
```
Row 1: Sunset, Ocean, Forest, Fire
Row 2: Cool Blue, Lavender, Aqua, Grape
Row 3: Peach, Sky, Warm, Mint
Row 4: Midnight, Carbon, Deep Space, Noir
Row 5: Royal, Rose Gold, Emerald, Amethyst
Row 6: Neon, Aurora, Candy, Clean
```

8. **Custom Color** (margin-bottom 24px):
   - Label
   - Color picker input
   - Disabled when `!showBackground`

9. **Image Background** (margin-bottom 24px):
   - Label + count (e.g., "3/8")
   - Gallery grid (4 columns) showing uploaded images
   - Remove button (X) on hover
   - Upload button (dashed border, ImagePlus icon)
   - Max 8 images
   - Disabled when `!showBackground`

### 6. Export Toolbar
**Position**: Below main content  
**Style**: Glass, padding 12px horizontal, 10px vertical  
**Visibility**: Only shown when `screenshot exists`  
**Contents** (Left to Right):

#### Section 1: Format Selection (Border Right)
- Label: "Format"
- PNG button (violet gradient when selected)
- JPEG button (violet gradient when selected)

#### Section 2: Export Actions
- **Copy** button (outline style, ClipboardCopy icon) - Ctrl+C
- **Copy Path** button (outline, shown only after save, Link icon)
- **Quick Save** button (cyan/teal gradient, Download icon) - Ctrl+S
- **Save As...** button (violet gradient, Save icon) - Ctrl+Shift+S

#### Section 3: Export Indicator
- Loading animation + "Exporting..." text when `isExporting`

### 7. Status Bar
**Position**: Bottom, always visible  
**Style**: Glass, padding 16px horizontal, 8px vertical, small text  
**Contents**:
- **Left**: Status message OR image dimensions
  - "Ready" (gray dot)
  - "Image: 1920 × 1080" (emerald dot, violet numbers)
  - Custom message (cyan text)
- **Right**: Version + attribution
  - "ShareX v4.0" (gradient)
  - Separator "•"
  - Attribution text (link)

## Conditional Rendering Rules

1. **Annotation Toolbar**: `screenshot && !cropMode`
2. **Crop Toolbar**: `screenshot && cropMode`
3. **Settings Panel**: `screenshot`
4. **Export Toolbar**: `screenshot`
5. **Clear Button**: `hasScreenshot`
6. **Copy Path Button**: `lastSavedPath !== null`

## Keyboard Shortcuts

### Global
- **PrintScreen**: Fullscreen capture
- **Ctrl+PrintScreen**: Region capture
- **Ctrl+Shift+PrintScreen**: Window capture
- **Ctrl+O**: Import image
- **Ctrl+V**: Paste from clipboard

### When Screenshot Exists
- **Ctrl+S**: Quick save
- **Ctrl+Shift+S**: Save as
- **Ctrl+C**: Copy to clipboard
- **Ctrl+Z**: Undo
- **Ctrl+Shift+Z** / **Ctrl+Y**: Redo
- **Delete** / **Backspace**: Delete selected annotation
- **Esc**: Cancel crop mode / deselect

### Tool Selection (✅ Implemented)
- **V**: Select
- **R**: Rectangle
- **E**: Ellipse
- **A**: Arrow
- **L**: Line
- **P**: Pen/Freehand
- **H**: Highlighter
- **T**: Text
- **B**: Speech Balloon
- **N**: Number/Step
- **C**: Crop
- **M**: Magnify
- **S**: Spotlight
- **F**: Toggle FX (Effects) Panel

## Color Scheme

### Primary Colors
- **Violet/Purple**: Active tools, primary actions (`#8B5CF6`, `#9333EA`)
- **Cyan/Teal**: Region capture, Quick Save (`#14B8A6`)
- **Pink/Rose**: Window capture, Delete (`#EC4899`, `#F43F5E`)
- **Green/Emerald**: Clipboard, success states (`#10B981`)
- **Amber/Orange**: Import (`#F59E0B`)

### UI Colors
- **Background**: `#1E1E2E` (dark)
- **Glass**: Backdrop blur with white overlay
- **Text**: `#94A3B8` (inactive), `#FFFFFF` (active)
- **Borders**: `#FFFFFF1A` (10% white)

### Status Colors
- **Info**: Cyan (`#06B6D4`)
- **Success**: Emerald (`#10B981`)
- **Error**: Red (`#EF4444`)

## Implementation Notes

### Current Design (To Keep)
✅ Dark theme (#1E1E2E)  
✅ Modern gradient buttons  
✅ Custom window controls

### High Priority Changes
1. **Add Settings Panel** (right side, 288px, only when screenshot exists)
2. **Reorganize Toolbars**: Two separate rows (capture + annotation)
3. **Add Export Toolbar** (bottom, format + actions)
4. **Move Drawing Tools** from current location to annotation toolbar
5. **Implement Contextual Controls** (text size/style, arrow curve)
6. **Add 24 Gradient Presets** to settings panel

### Medium Priority
1. Status bar with image dimensions
2. Keyboard shortcut system
3. Undo/Redo UI + implementation
4. Crop toolbar (conditional)

### Low Priority
1. Drag & drop overlay
2. Window picker modal
3. Settings modal
4. Update notification
