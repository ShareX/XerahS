# Task Settings UI Gap Analysis

**Source:** `ShareX\Forms\TaskSettingsForm.cs` (WinForms)
**Target:** 
1. `src\XerahS.UI\Views\WorkflowEditorView.axaml` (Container / Task & Destinations)
2. `src\XerahS.UI\Views\TaskSettingsPanel.axaml` (Inner Settings Panel)

## Executive Summary
The Avalonia implementation splits the functionality of the single WinForms `TaskSettingsForm` into two views: `WorkflowEditorView` (acting as the container/wizard) and `TaskSettingsPanel` (embedded within the "Settings" tab of the editor). 

- **WorkflowEditorView**: Handles the "Task" description, Job selection, Hotkey recording, and "Destinations" selection.
- **TaskSettingsPanel**: Handles the granular settings for General, Capture, Image, Upload, etc.

Significant gaps remain in `TaskSettingsPanel`, particularly for Region Capture, Screen Recording, OCR, and Watch Folders.

---

## 1. Task Tab (WinForms)
*Avalonia Status: **Split / Partially Implemented***

In WinForms, this tab handles Task selection, Description, Folder paths, and Destinations. In Avalonia, this responsibility is moved to `WorkflowEditorView`.

| Control (WinForms) | Avalonia Equivalent | Status | Location | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Description** | `tbDescription` | :white_check_mark: MATCH | `WorkflowEditorView` | Top of the "Task" tab. |
| **Task Selection** | `cmsTask` (MenuButton) | :white_check_mark: MATCH | `WorkflowEditorView` | Implemented as "Category" and "Task" ComboBoxes. |
| **Destinations** | `btnDestinations` | :white_check_mark: MATCH | `WorkflowEditorView` | Dedicated "Destinations" tab with ListBox selector. |
| **Screenshots Folder** | `txtScreenshotsFolder` | :x: MISSING | - | Custom folder path override is implementation missing. |
| **Custom Uploaders** | `cbCustomUploaders` | :x: MISSING | - | |
| **FTP Accounts** | `cbFTPAccounts` | :x: MISSING | - | |
| **Override Settings** | Checkboxes | :x: MISSING | - | "Override screenshots folder", "Override custom uploader", etc. are missing. |
| **After Capture Tasks** | `btnAfterCapture` | :twisted_rightwards_arrows: MOVED | `TaskSettingsPanel` | Moved to "Upload" tab > "After capture" group as checkboxes. |
| **After Upload Tasks** | `btnAfterUpload` | :twisted_rightwards_arrows: MOVED | `TaskSettingsPanel` | Moved to "Upload" tab > "After upload" group as checkboxes. |

## 2. General Tab (WinForms)
*Avalonia Status: **Partial*** (in `TaskSettingsPanel`)

Avalonia implements basic notification toggles but lacks the deep customization of the WinForms version.

### Main & Notifications
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **Override General Settings** | None | :x: MISSING | |
| **Play Sound After Capture** | General > Notifications | :white_check_mark: MATCH | |
| **Show Toast Notification** | General > Notifications | :white_check_mark: MATCH | |
| **Custom Sound Paths** | None | :x: MISSING | Controls for custom paths for Capture, Task Completed, Error, Action sounds. |
| **Toast Window Settings** | None | :x: MISSING | Size, Duration, Fade, Placement, Click Actions, Auto-hide, Disable on fullscreen. |
| **Play Sound After Action** | None | :x: MISSING | |
| **Play Sound After Upload** | None | :x: MISSING | |

## 3. Image Tab (WinForms)
*Avalonia Status: **Significant Differences*** (in `TaskSettingsPanel`)

Avalonia focuses on Image Effects configuration here, missing standard quality/thumbnail settings.

### Quality & Thumbnail
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **JPEG/PNG/GIF Quality** | None | :x: MISSING | Quality sliders, Bit depth, Auto-use JPEG threshold. |
| **Thumbnail Settings** | None | :x: MISSING | Width, Height, Name pattern, "Save thumbnail if smaller". |
| **Image Effects** | Image Tab (Full Editor) | :wrench: MODIFIED | WinForms has simple checkboxes. Avalonia embeds the full Effects Editor (Presets, List, PropertyGrid, Preview) directly into the tab. |

## 4. Capture Tab (WinForms)
*Avalonia Status: **Partial*** (in `TaskSettingsPanel`)

Basic "General" capture settings are implemented. Major features (Region Capture options, Screen Recorder, OCR) are completely absent.

### General
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **Use Modern Capture** | Capture > General | :white_check_mark: MATCH | (Direct3D11) |
| **Show Cursor** | Capture > General | :white_check_mark: MATCH | |
| **Screenshot Delay** | Capture > General | :white_check_mark: MATCH | |
| **Transparent Capture** | Capture > General | :white_check_mark: MATCH | |
| **Window Shadow** | Capture > General | :white_check_mark: MATCH | |
| **Client Area Only** | Capture > General | :white_check_mark: MATCH | |
| **Auto Hide Icons/Taskbar** | None | :x: MISSING | Desktop icons and Taskbar toggles. |

### Region Capture
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **Region Capture Settings** | None | :x: MISSING | Background dim, Fixed size, FPS limit, Crosshair, Magnifier settings, Detect controls/windows, Click actions. |

### Screen Recorder
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **Screen Recorder Settings** | None | :x: MISSING | FPS, Duration, Start delay, Codec options (FFmpeg), GIF FPS, Auto-start. |

### OCR
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **OCR Settings** | None | :x: MISSING | Auto-copy, Silent mode, Default language. |

## 5. Upload Tab (WinForms)
*Avalonia Status: **Partial*** (in `TaskSettingsPanel`)

File naming is mostly implemented. Advanced upload filters and clipboard upload settings are missing.

### File Naming
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **Name Pattern** | Upload > File naming | :white_check_mark: MATCH | `NameFormatPattern` |
| **Active Window Pattern** | Upload > File naming | :white_check_mark: MATCH | `NameFormatPatternActiveWindow` |
| **Use Name Pattern for Upload** | Upload > File naming | :white_check_mark: MATCH | |
| **Replace Patterns** | Upload > File naming | :white_check_mark: MATCH | Replace problematic characters. |
| **URL Regex Replace** | Upload > File naming | :white_check_mark: MATCH | Pattern & Replacement fields. |
| **Auto Increment Number** | None | :x: MISSING | |
| **Time Zone Settings** | None | :x: MISSING | |

### Other Sections
| Control (WinForms) | Avalonia Equivalent | Status | Notes |
| :--- | :--- | :--- | :--- |
| **Clipboard Upload** | None | :x: MISSING | Specific settings for "Upload from Clipboard" tool (Share URL, URL contents, etc). |
| **Uploader Filters** | None | :x: MISSING | Filename/Extension based destination routing. |

## 6. Actions Tab
*Status: **Pending***
*   **WinForms**: Fully functional list of custom actions (external programs) with Add/Edit/Duplicate buttons.
*   **Avalonia**: `TaskSettingsPanel` > `Actions` tab exists but contains only a placeholder.

## 7. Tools Tab
*Status: **Pending***
*   **WinForms**: Settings for tools like Color Picker (hex format, etc).
*   **Avalonia**: `TaskSettingsPanel` > `Tools` tab exists but contains only a placeholder.

## 8. Watch Folders Tab
*Status: **Missing***
*   **WinForms**: Configuration for folder monitoring.
*   **Avalonia**: Tab does not exist in either view.

## 9. Advanced Tab
*Status: **Pending***
*   **WinForms**: PropertyGrid exposing all settings.
*   **Avalonia**: `TaskSettingsPanel` > `Advanced` tab exists but contains only a placeholder.
