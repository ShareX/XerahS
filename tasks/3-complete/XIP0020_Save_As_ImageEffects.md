# XIP0020: Save As Image Effects

## Priority
**Medium** - Feature Parity / Enhancement

## Assignee
**Codex**

## Branch
`develop`

## Status
Complete - Updated on 2026-01-19

## Assessment
The current Image Effects web editor (and desktop port) allows applying effects, but not saving/loading them as presets. ShareX supports `.sxie` files (ZIP containing `Config.json`). XerahS should support a native format (`.xsie`) which uses the same ZIP container structure but with modern JSON schema, and the legacy ShareX format (`.sxie`) for compatibility.

## Objective
Implement "Save As" functionality in the Image Effects editor to allow users to save their current effect configuration. Support both native XerahS format (`.xsie`) and legacy ShareX format (`.sxie`). Both formats will use a ZIP container holding a `Config.json` file.

## Scope
1.  **UI Updates**:
    *   Add "Save Preset" / "Load Preset" buttons to the Image Effects editor UI.
    *   Implement a Save File Dialog with filters for `*.xsie` and `*.sxie`.
2.  **Native Format (`.xsie`)**:
    *   Implement serialization of the current effects list to a cleaner, modern JSON format.
    *   Package the result into a ZIP file containing `Config.json`, similar to the legacy `.sxie` format.
3.  **Legacy Format (`.sxie`) Export**:
    *   Implement `LegacyImageEffectExporter` to convert XerahS `ImageEffect` objects back to the legacy ShareX schema.
    *   Package the result into a ZIP file containing `Config.json`.

## Detailed Design

### 1. Legacy Image Effect Exporter
Create `XerahS.Common.Helpers.LegacyImageEffectExporter` to reverse the logic of `LegacyImageEffectImporter`.

**Responsibilities:**
*   Map XerahS effect types (e.g., `BrightnessImageEffect`) back to legacy types (e.g., `ShareX.ImageEffectsLib.Brightness`).
*   Map properties back to legacy names (e.g., `Amount` -> `Value`).
*   Handle color conversions (SKColor -> "A, R, G, B").
*   Construct the JSON structure matching the legacy `Config.json`.
*   Create a ZIP archive with the JSON.

**Mapping Table (Reverse):**
| XerahS Type | Legacy Type | Property Mapping (XerahS -> Legacy) |
| :--- | :--- | :--- |
| `BrightnessImageEffect` | `Brightness` | `Amount` -> `Value` |
| `ContrastImageEffect` | `Contrast` | `Amount` -> `Value` |
| `HueImageEffect` | `Hue` | `Amount` -> `Value` |
| `ColorizeImageEffect` | `Colorize` | `Color` -> `Color`, `Strength` -> `Strength` |
| ... (and so on for all supported effects) | | |

### 2. Native Format (`.xsie`)
A direct JSON serialization of the `ObservableCollection<ImageEffect>`.
*   Should include type discriminators (polymorphic serialization).
*   Should be version-resilient if possible.
*   **Container**: ZIP archive.
*   **Content**: `Config.json` inside the ZIP.

### 3. User Interface
*   **Location**: To the right of the current "Upload" button in the Image Effects editor toolbar.
*   **Actions**:
    *   **Save As...**: Opens save dialog. Extension determines the format.
    *   **Load...**: Opens open dialog. Supports both `.xsie` and `.sxie` (via existing Importer).

## Deliverables
*   [x] `LegacyImageEffectExporter` implemented and tested.
*   [x] UI updated with Save/Load buttons.
*   [x] Save/Load buttons placed in ShareX.Editor bottom bar; Import/Export remain in TaskSettings.
*   [x] "Save As" dialog integrated with format selection.
*   [x] Verification: `.xsie` and `.sxie` round-trip validated via automated tests.
*   [x] Workflow pipeline applies image effects when `AfterCaptureTasks.AddImageEffects` is set.
