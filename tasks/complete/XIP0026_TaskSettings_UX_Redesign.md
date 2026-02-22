# XIP0026: Task Settings UX Redesign

## Goal Description
Redesign the "Task Settings" UI in `WorkflowEditorView` to improve usability by categorizing settings more logically.
Current Single "Settings" tab (implemented as `TaskSettingsPanel`) is overloaded.
We will separate "Image Settings" and "Video Settings" into their own top-level tabs, visible only when relevant.
We will also restructure handling of Destinations, File Naming, and Upload settings into a more cohesive flow.

## User Review Required
> [!NOTE]
> **Dynamic Tab Visibility**: Top level "Image Settings" tab will only appear for **Screen Capture** jobs. "Video Settings" tab will only appear for **Screen Record** jobs.
> **Destinations Moved**: The top-level "Destinations" tab is **removed**. Its functionality is moved into `TaskSettingsPanel` under the "Upload" sub-tab.

## Proposed Changes

### XerahS.UI
#### [MODIFY] [WorkflowEditorView.axaml](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/app/XerahS.UI/Views/WorkflowEditorView.axaml)
- **Rename** "Settings" TabItem header to "Task Settings".
- **Remove** "Destinations" TabItem completely (merged into `TaskSettingsPanel`).
- **Add** "Image Settings" TabItem (index 1):
    - Header: "Image Settings"
    - Visibility: Bound to `IsScreenCaptureJob` (ViewModel property).
    - Content: `views:TaskImageSettingsPanel`
- **Add** "Video Settings" TabItem (index 2):
    - Header: "Video Settings"
    - Visibility: Bound to `IsScreenRecordJob` (ViewModel property).
    - Content: `views:TaskVideoSettingsPanel`

#### [MODIFY] [WorkflowEditorViewModel.cs](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/app/XerahS.UI/ViewModels/WorkflowEditorViewModel.cs)
- `AvailableDestinations` and `SelectedDestination` logic remains here, but we need to ensure `TaskSettingsViewModel` can access or bind to it if we move the UI.
- *Alternatively*, we can pass `WorkflowEditorViewModel` itself to `TaskSettingsPanel` as a parent context, or move the logic properties to `TaskSettingsViewModel` (which might be cleaner but harder refactor).
- **Decision**: Keep logic in `WorkflowEditorViewModel`, but in `TaskSettingsPanel.axaml`, we will bind to `$parent[Window].DataContext.AvailableDestinations` or similar relative binding since `TaskSettingsPanel` is a child of the Window.

#### [MODIFY] [TaskSettingsViewModel.cs](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/app/XerahS.UI/ViewModels/TaskSettingsViewModel.cs)
- Add boolean helper properties:
    - `public bool IsScreenCaptureJob`
    - `public bool IsScreenRecordJob`
- Ensure property change notifications for job type changes.

#### [NEW] `TaskImageSettingsPanel.axaml` / `.cs`
- Create new UserControl.
- **Move** content from `TaskSettingsPanel` -> "Image" Tab (lines ~185-228) to this new control.
- Bind to existing `TaskSettingsViewModel`.

#### [NEW] `TaskVideoSettingsPanel.axaml` / `.cs`
- Create new UserControl.
- **Move** content from `TaskSettingsPanel` -> "Capture" Tab -> "Screen Recorder" section (lines ~136-160) to this new control.
- Bind to existing `TaskSettingsViewModel`.
- **Add Codec Selection**: explicit dropdown for Video Codec (AV1, H.264, GIF, etc.).

#### [MODIFY] [TaskSettingsPanel.axaml](file:///C:/Users/liveu/source/repos/ShareX%20Team/XerahS/src/desktop/app/XerahS.UI/Views/TaskSettingsPanel.axaml)
- **Reorder & Refactor Tabs**:
    1.  **Capture** (Header="Capture"): Move to Index 0.
        - Contains: Cursor, Delay, Window Title, Client Area, etc.
    2.  **File** (Header="File"): **NEW Tab**.
        - Move "File naming" section (Name pattern, Regex replace) from old "Upload" tab to here.
    3.  **Upload** (Header="Upload"):
        - **Add**: "Destinations" combobox section (moved from `WorkflowEditorView`).
        - **Keep**: Clipboard upload settings.
        - **Keep**: After Upload tasks.
    4.  **Notifications** (Header="Notifications"): Renamed from "General".
        - Contains: Sound, Toast settings.
    5.  **Advanced** (Header="Advanced"): Keep as last.
- **Remove**: "Image", "Effects", "Screen Recorder" sections (moved to new controls).

## Verification Plan

### Manual Verification
1.  **Open Workflow Editor** (e.g., "Capture Region").
2.  **Check Layout**:
    -   Verify top tabs: Task -> Image Settings -> Task Settings. (No "Destinations" tab).
    -   Verify "Image Settings" only shows for Capture jobs.
    -   Verify "Video Settings" only shows for Screen Record jobs.
3.  **Check Task Settings Panel**:
    -   Verify Tabs: Capture -> File -> Upload -> Notifications -> Advanced.
    -   **File Tab**: Check Name pattern inputs are present and working.
    -   **Upload Tab**: Check "Select Destination" dropdown is present and populated. Verify changing it updates the workflow.
    -   **Notifications**: Check sound/toast settings are present.
4.  **Functionality**:
    -   Config a workflow with specific File Naming and Destination.
    -   Run it.
    -   Verify file is saved with correct name patterns.
    -   Verify file is uploaded to the selected destination.
