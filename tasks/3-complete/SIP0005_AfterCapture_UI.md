# CP03: AfterCaptureJobs - Task Settings UI

## Priority
**HIGH** - Enables user configuration of automatic workflows

## Assignee
**Copilot** (Surface Laptop 7, VS 2026)

## Branch
`feature/after-capture-ui`

## Instructions
**CRITICAL**: Create the `feature/after-capture-ui` branch first before starting work.

```powershell
git checkout master
git pull origin master
git checkout -b feature/after-capture-ui
```

## Objective
Design and implement UI in Task Settings for users to configure AfterCapture tasks (checkboxes for SaveImageToFile, CopyToClipboard, UploadImageToHost, etc.).

## Background
ShareX.Avalonia has 21 defined AfterCaptureTasks in the enum, but no UI to configure them. Currently, users cannot enable/disable tasks like "Upload image to host" without editing code.

**Your job**: Build the UI so users can check/uncheck tasks in Settings → Task Settings.

## Scope

### 1. Add "After Capture" Section to TaskSettingsView

**File**: `src/ShareX.Avalonia.UI/Views/TaskSettingsView.axaml`

Add a new section after the "File Naming" section:

```xml
<!-- After Capture Tasks Section -->
<Border Background="#2A2A3E" Padding="20" CornerRadius="12" Margin="0,0,0,16">
    <StackPanel Spacing="16">
        <TextBlock Text="After Capture" 
                   FontSize="16" 
                   FontWeight="SemiBold" 
                   Foreground="White"/>
        
        <TextBlock Text="Choose actions to perform automatically after capturing" 
                   FontSize="12" 
                   Foreground="#94A3B8" 
                   Margin="0,0,0,8"/>
        
        <!-- Checkboxes -->
        <StackPanel Spacing="8">
            <CheckBox Content="Save image to file" 
                      IsChecked="{Binding SaveImageToFile}"/>
            <CheckBox Content="Copy image to clipboard" 
                      IsChecked="{Binding CopyImageToClipboard}"/>
            <CheckBox Content="Upload image to host" 
                      IsChecked="{Binding UploadImageToHost}"
                      FontWeight="SemiBold"/>
            <CheckBox Content="Annotate image" 
                      IsChecked="{Binding AnnotateImage}"/>
            <CheckBox Content="Show after capture window" 
                      IsChecked="{Binding ShowAfterCaptureWindow}"/>
        </StackPanel>
    </StackPanel>
</Border>
```

**Design Notes**:
- Use similar styling to existing File Naming section
- Highlight "Upload image to host" (primary feature)
- Group related tasks together
- Add tooltips if helpful

### 2. Add Properties to SettingsViewModel

**File**: `src/ShareX.Avalonia.UI/ViewModels/SettingsViewModel.cs`

Add bool properties for each checkbox that map to `AfterCaptureTasks` flags:

```csharp
public bool SaveImageToFile
{
    get => DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile);
    set
    {
        if (value)
            DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.SaveImageToFile;
        else
            DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.SaveImageToFile;
        OnPropertyChanged();
    }
}

public bool CopyImageToClipboard
{
    get => DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
    set
    {
        if (value)
            DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.CopyImageToClipboard;
        else
            DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.CopyImageToClipboard;
        OnPropertyChanged();
    }
}

public bool UploadImageToHost
{
    get => DefaultTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost);
    set
    {
        if (value)
            DefaultTaskSettings.AfterCaptureJob |= AfterCaptureTasks.UploadImageToHost;
        else
            DefaultTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.UploadImageToHost;
        OnPropertyChanged();
    }
}

// Add similar properties for AnnotateImage, ShowAfterCaptureWindow, etc.
```

## Guidelines
- **Follow ShareX.Avalonia UI patterns** (dark theme, rounded corners, spacing)
- **Make "Upload image to host" prominent** (it's the main feature)
- **Use icons if available** (💾 for save, 📋 for clipboard, ⬆️ for upload)
- **Ensure settings persist** via SettingManager.Settings.DefaultTaskSettings

## Integration Notes

This task coordinates with **CX04** (Codex's backend task). When users check "Upload image to host", it sets the flag that Codex's code reads.

**Don't worry about**:
- Upload implementation (Codex handles in CX04)
- AfterUpload tasks UI (future work)

## Deliverables
- ✅ TaskSettingsView.axaml updated with "After Capture" section
- ✅ SettingsViewModel.cs has checkbox binding properties
- ✅ Build succeeds on `feature/after-capture-ui`
- ✅ Settings persist when toggled
- ✅ Commit and push changes

## Testing

### Visual Test
1. **Run app** and navigate to Settings → Task Settings
2. **Verify** "After Capture" section displays
3. **Toggle checkboxes** - confirm they update
4. **Restart app** - verify settings persist

### Integration Test (after CX04 is merged)
1. **Enable "Upload image to host"** checkbox
2. **Trigger hotkey** (Ctrl+PrintScreen)
3. **Expected**: Image captures, saves, and uploads automatically
4. **Check Debug view** for upload logs

## Estimated Effort
**Medium** - 2-3 hours
