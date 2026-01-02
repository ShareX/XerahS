# CP05: History Settings Tab - Application Settings

## Priority
**MEDIUM** - Enables user configuration of history behavior

## Assignee
**Copilot** (Surface Laptop 7, VS 2026)

## Branch
`feature/history-settings`

## Instructions
**CRITICAL**: Create the `feature/history-settings` branch first before starting work.

```powershell
git checkout master
git pull origin master
git checkout -b feature/history-settings
```

## Objective
Implement the History tab in Application Settings to allow users to configure history saving behavior and recent tasks display options.

## Background
ShareX.Avalonia has a placeholder "History" tab in `ApplicationSettingsView.axaml` (line 92-94) that currently just shows "History settings placeholder". The `ApplicationConfig.cs` already has the necessary settings properties, but there's no UI to configure them.

**Reference**: ShareX `ApplicationSettingsForm.cs` lines 1022-1059 contains the History settings region.

## Scope

### 1. Update History Tab in ApplicationSettingsView

**File**: `src/ShareX.Avalonia.UI/Views/ApplicationSettingsView.axaml`

Replace the placeholder with the actual settings UI:

```xml
<!-- History Tab -->
<TabItem Header="History">
    <ScrollViewer>
        <StackPanel Margin="20" Spacing="20">
            <!-- History Settings -->
            <StackPanel Spacing="10">
                <TextBlock Text="History" FontWeight="SemiBold"/>
                <Border BorderBrush="{DynamicResource SystemControlForegroundBaseLowBrush}" BorderThickness="1" CornerRadius="4" Padding="10">
                    <StackPanel Spacing="10">
                        <CheckBox IsChecked="{Binding HistorySaveTasks}" Content="Save captured/uploaded items to history"/>
                        <CheckBox IsChecked="{Binding HistoryCheckURL}" Content="Only save items with URL"/>
                    </StackPanel>
                </Border>
            </StackPanel>

            <!-- Recent Tasks Settings -->
            <StackPanel Spacing="10">
                <TextBlock Text="Recent Tasks" FontWeight="SemiBold"/>
                <Border BorderBrush="{DynamicResource SystemControlForegroundBaseLowBrush}" BorderThickness="1" CornerRadius="4" Padding="10">
                    <StackPanel Spacing="10">
                        <CheckBox IsChecked="{Binding RecentTasksSave}" Content="Save recent tasks"/>
                        
                        <Grid ColumnDefinitions="Auto,Auto,*" RowDefinitions="Auto">
                            <TextBlock Grid.Column="0" Text="Maximum recent tasks:" VerticalAlignment="Center"/>
                            <NumericUpDown Grid.Column="1" 
                                           Value="{Binding RecentTasksMaxCount}" 
                                           Minimum="1" Maximum="100" 
                                           Width="100" Margin="10,0,0,0"/>
                        </Grid>
                        
                        <Separator Margin="0,5"/>
                        <TextBlock Text="Display Options" FontWeight="SemiBold" FontSize="12"/>
                        
                        <CheckBox IsChecked="{Binding RecentTasksShowInMainWindow}" Content="Show recent tasks in main window"/>
                        <CheckBox IsChecked="{Binding RecentTasksShowInTrayMenu}" Content="Show recent tasks in tray menu"/>
                        <CheckBox IsChecked="{Binding RecentTasksTrayMenuMostRecentFirst}" 
                                  Content="Show most recent first in tray menu" 
                                  IsEnabled="{Binding RecentTasksShowInTrayMenu}"/>
                    </StackPanel>
                </Border>
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

### 2. Add Properties to SettingsViewModel

**File**: `src/ShareX.Avalonia.UI/ViewModels/SettingsViewModel.cs`

Add these properties that bind to `ApplicationConfig`:

```csharp
// History Settings
public bool HistorySaveTasks
{
    get => SettingManager.Settings.HistorySaveTasks;
    set
    {
        SettingManager.Settings.HistorySaveTasks = value;
        OnPropertyChanged();
    }
}

public bool HistoryCheckURL
{
    get => SettingManager.Settings.HistoryCheckURL;
    set
    {
        SettingManager.Settings.HistoryCheckURL = value;
        OnPropertyChanged();
    }
}

// Recent Tasks Settings
public bool RecentTasksSave
{
    get => SettingManager.Settings.RecentTasksSave;
    set
    {
        SettingManager.Settings.RecentTasksSave = value;
        OnPropertyChanged();
    }
}

public int RecentTasksMaxCount
{
    get => SettingManager.Settings.RecentTasksMaxCount;
    set
    {
        SettingManager.Settings.RecentTasksMaxCount = value;
        OnPropertyChanged();
    }
}

public bool RecentTasksShowInMainWindow
{
    get => SettingManager.Settings.RecentTasksShowInMainWindow;
    set
    {
        SettingManager.Settings.RecentTasksShowInMainWindow = value;
        OnPropertyChanged();
    }
}

public bool RecentTasksShowInTrayMenu
{
    get => SettingManager.Settings.RecentTasksShowInTrayMenu;
    set
    {
        SettingManager.Settings.RecentTasksShowInTrayMenu = value;
        OnPropertyChanged();
    }
}

public bool RecentTasksTrayMenuMostRecentFirst
{
    get => SettingManager.Settings.RecentTasksTrayMenuMostRecentFirst;
    set
    {
        SettingManager.Settings.RecentTasksTrayMenuMostRecentFirst = value;
        OnPropertyChanged();
    }
}
```

### 3. Ensure ApplicationConfig Has HistoryCheckURL

**File**: `src/ShareX.Avalonia.Core/Models/ApplicationConfig.cs`

Verify/add this property (may already exist):

```csharp
public bool HistoryCheckURL = true;
```

## ShareX History Settings Reference

| Setting | Description | Default |
|---------|-------------|---------|
| HistorySaveTasks | Save tasks (captures/uploads) to history | `true` |
| HistoryCheckURL | Only save items that have a URL | `true` |
| RecentTasksSave | Persist recent tasks across sessions | `false` |
| RecentTasksMaxCount | Maximum number of recent tasks to remember | `10` |
| RecentTasksShowInMainWindow | Display recent tasks in main window | `true` |
| RecentTasksShowInTrayMenu | Display recent tasks in system tray menu | `true` |
| RecentTasksTrayMenuMostRecentFirst | Order tray menu with newest first | `false` |

## Guidelines
- **Follow existing ApplicationSettingsView patterns** (bordered sections, spacing)
- **Properties should auto-save** via SettingManager when changed (already implemented for other settings)
- **Disable dependent options** (e.g., tray order only enabled when tray menu is enabled)

## Deliverables
- ✅ History tab in ApplicationSettingsView.axaml with full UI
- ✅ SettingsViewModel.cs has all history-related properties
- ✅ Settings persist when changed
- ✅ Build succeeds on `feature/history-settings`
- ✅ Commit and push changes

## Testing

### Visual Test
1. **Run app** and navigate to Settings → Application Settings → History tab
2. **Verify** all checkboxes and numeric input display correctly
3. **Toggle checkboxes** - confirm they update
4. **Change max count** - verify it accepts values

### Functional Test
1. **Disable "Save to history"** → capture an image → verify it doesn't appear in History view
2. **Enable "Save to history"** → capture an image → verify it appears in History view
3. **Set max recent tasks** to 5 → capture 6 images → verify only 5 appear in recent tasks
4. **Restart app** → verify settings persist

## Estimated Effort
**Low** - 1-2 hours

## Related Tasks
- **CP04**: History UI (different scope - the main History browsing view)
