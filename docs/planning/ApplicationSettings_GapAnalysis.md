# Application Settings Interface Inventory & Gap Analysis

## 1. WinForms Inventory (`ApplicationSettingsForm.cs`)

### General Tab
- **System**:
    - `cbShowTray` (CheckBox): Show tray icon.
    - `cbSilentRun` (CheckBox): Minimize to tray on start.
    - `cbStartWithWindows` (CheckBox): Run ShareX when Windows starts.
    - `cbCheckPreReleaseUpdates` (CheckBox): Check for pre-release updates.
    - `cbAutoCheckUpdate` (CheckBox): Automatically check for updates.
- **Tray Icon**:
    - `cbTrayIconProgressEnabled` (CheckBox): Show task progress on tray icon.
    - `cbTraySingleClickAction` (ComboBox): Left click action.
    - `cbTrayDoubleClickAction` (ComboBox): Double click action.
    - `cbTrayMiddleClickAction` (ComboBox): Middle click action.
- **Taskbar**:
    - `cbTaskbarProgressEnabled` (CheckBox): Show task progress on taskbar.
- **Language**:
    - `btnLanguages` (MenuButton): Select language.

### Paths Tab
- **Settings**:
    - `cbUseCustomScreenshotsPath` (CheckBox): Use custom screenshots folder.
    - `txtCustomScreenshotsPath` (TextBox): Custom path string.
    - `btnBrowseCustomScreenshotsPath` (Button): Browse dialog.
    - `txtSaveImageSubFolderPattern` (TextBox): Subfolder pattern (e.g. `%y-%mo`).

### Integration Tab
- **Windows**:
    - `cbShellContextMenu` (CheckBox): Show "Upload with ShareX" in Context Menu.
    - `cbSendToMenu` (CheckBox): Show ShareX in "Send to" menu.
    - `cbChromeExtensionSupport` (CheckBox): Chrome extension support.
    - `cbFirefoxAddonSupport` (CheckBox): Firefox addon support.

### History Tab
- **History**:
    - `cbHistorySaveTasks` (CheckBox): Save tasks to history.
    - `cbHistoryCheckURL` (CheckBox): Only save tasks with URL.
- **Recent Tasks**:
    - `cbRecentTasksSave` (CheckBox): Save recent tasks.
    - `nudRecentTasksMaxCount` (NumericUpDown): Max count.
    - `cbRecentTasksShowInMainWindow` (CheckBox).
    - `cbRecentTasksShowInTrayMenu` (CheckBox).
    - `cbRecentTasksTrayMenuMostRecentFirst` (CheckBox).

### Theme Tab
- `cbThemes` (ComboBox): Select theme.
- `pgTheme` (PropertyGrid): Advanced theme properties.

## 2. Avalonia Inventory (`ApplicationSettingsView.axaml`)

### General Tab
- `ShowTray` (CheckBox): Bound to `SettingManager.Settings.ShowTray`.
- `SilentRun` (CheckBox): Bound to `SettingManager.Settings.SilentRun`.
- **MISSING**: Tray click actions, update settings, taskbar settings.

### Theme Tab
- `SelectedTheme` (ComboBox): Hardcoded "Default" / "Dark".
- **MISSING**: PropertyGrid for advanced theme editing.

### Paths Tab
- `UseCustomScreenshotsPath` (CheckBox).
- `ScreenshotsFolder` (TextBox).
- `BrowseFolderCommand` (Button).
- `SaveImageSubFolderPattern` (TextBox).
- **STATUS**: Good alignment with WinForms context.

### Integration Tab
- `IsPluginExtensionRegistered` (CheckBox): .xsdp file association.
- **MISSING**: Start with Windows (often in General in WinForms, here intended for Integration?), Shell Context Menu, Send To.

### History Tab
- `HistorySaveTasks` (CheckBox).
- `HistoryCheckURL` (CheckBox).
- `RecentTasksSave` (CheckBox).
- `RecentTasksMaxCount` (NumericUpDown).
- `RecentTasksShowInMainWindow` (CheckBox).
- `RecentTasksShowInTrayMenu` (CheckBox).
- `RecentTasksTrayMenuMostRecentFirst` (CheckBox).
- **STATUS**: Good alignment.

## 3. Gap Analysis

### Critical Gaps (Priority)
These controls are present in WinForms and are high value but missing in Avalonia:

1.  **General / System**:
    - `TrayIconProgressEnabled`
    - `TaskbarProgressEnabled`
    - `AutoCheckUpdate` / `UpdateChannel`
2.  **General / Tray Actions**:
    - Left/Double/Middle click actions (Infrastructure exists in `ApplicationConfig`, UI missing).
    - *Mapping*: `ApplicationConfig.TrayLeftClickAction` etc.
3.  **Integration**:
    - `RunAtStartup`: WinForms `StartWithWindows`. Standard user expectation.
    - `EnableContextMenuIntegration` & `EnableSendToIntegration`: WinForms `ShellContextMenu`, `SendToMenu`.

### Controls Avoided / Low Priority
- `cbUseWhiteShareXIcon`: Windows-specific styling preference.
- `btnCheckDevBuild`: Can be part of update logic later.
- `Chrome/Firefox Extension Support`: Platform specific native messaging host setup. complicated.
- `Theme PropertyGrid`: Avalonia styling is different, a property grid might not map 1:1.

## 4. Implementation Plan

### Goal
Update `ApplicationSettingsView.axaml` and `SettingsViewModel.cs` to fill the critical gaps.

### Changes to `ApplicationSettingsView.axaml`

#### General Tab
- Add `TrayIconProgressEnabled` checkbox.
- Add `TaskbarProgressEnabled` checkbox.
- Add `UpdateSettings` group:
    - Checkbox `AutoCheckUpdate`.
    - ComboBox `UpdateChannel` (Release, Pre-Release, Dev).
- Add `TrayActions` group:
    - ComboBox `TrayLeftClickAction`.
    - ComboBox `TrayLeftDoubleClickAction`.
    - ComboBox `TrayMiddleClickAction`.

#### Integration Tab
- Add `StartWithWindows` checkbox.
- Add `ShellContextMenu` checkbox.
- Add `SendToMenu` checkbox.

### Persistence
- All controls bind to `SettingsViewModel`, which proxies `SettingManager.Settings`.
- Ensure `SettingsViewModel` has properties for all the above.

### Staged Delivery
1.  **Stage 1: General & Updates**: Add Tray/Taskbar progress, Update settings to General tab.
2.  **Stage 2: Tray Actions**: Add Tray click action configuration (requires Enum binding helper).
3.  **Stage 3: Integration**: Add Start with Windows and Shell integration toggles.
