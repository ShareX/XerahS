# CP04: History UI - Replicate ShareX History Controls

## Priority
**HIGH** - Essential for browsing and managing captured content history

## Assignee
**Copilot** (Surface Laptop 7, VS 2026)

## Branch
`feature/history-ui`

## Status
Complete - Verified on 2026-01-08

## Assessment
100% Complete. `HistoryView.axaml` has enhanced toolbar, search, and context menu.

## Instructions
**CRITICAL**: Create the `feature/history-ui` branch first before starting work.

```powershell
git checkout master
git pull origin master
git checkout -b feature/history-ui
```

## Objective
Replicate the ShareX History UI controls in ShareX.Avalonia, providing feature parity for browsing, searching, filtering, and managing capture history.

## Background
ShareX.Avalonia has a basic History view (`HistoryView.axaml`) with:
- Refresh button
- Grid/List view toggle
- Basic item count display
- Simple thumbnail grid with minimal context menu

The original ShareX `HistoryForm` provides comprehensive functionality including:
- **Toolbar**: Search, Advanced Search toggle, Favorites filter, Show Stats, Import Folder, Settings
- **List View**: Virtual mode ListView with columns (Icon, DateTime, Filename, URL)
- **Advanced Search Panel**: Filter by filename, URL, date range, type, and host
- **Preview Panel**: Thumbnail preview with image size label (SplitContainer layout)
- **Context Menu**: Extensive actions for Open, Copy, Edit, Delete, Upload, etc.

**Your job**: Build the UI to match ShareX functionality using Avalonia controls.

## Scope

### 1. Enhanced Toolbar Section

**File**: `src/ShareX.Avalonia.UI/Views/HistoryView.axaml`

Replace the basic toolbar with a comprehensive toolbar:

```xml
<!-- Enhanced Toolbar -->
<Border Grid.Row="0" Padding="12,8">
    <Grid ColumnDefinitions="Auto,*,Auto">
        <!-- Left: Search -->
        <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="8">
            <TextBlock Text="🔍 Search:" VerticalAlignment="Center" Foreground="{DynamicResource SystemControlForegroundBaseMediumBrush}"/>
            <TextBox Text="{Binding SearchText}" 
                     Watermark="Search by filename..."
                     Width="200"
                     CornerRadius="6"
                     Padding="8,6"/>
            <Button Content="🔎" Command="{Binding SearchCommand}" 
                    ToolTip.Tip="Search" Padding="8" CornerRadius="6"/>
            <ToggleButton Content="⚙️" IsChecked="{Binding IsAdvancedSearchVisible}" 
                          ToolTip.Tip="Advanced Search" Padding="8" CornerRadius="6"/>
        </StackPanel>
        
        <!-- Center: View Controls -->
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center">
            <ToggleButton Content="⭐" IsChecked="{Binding ShowFavoritesOnly}" 
                          ToolTip.Tip="Show Favorites Only" Padding="8" CornerRadius="6"/>
            <Button Content="📊" Command="{Binding ShowStatsCommand}" 
                    ToolTip.Tip="Show Statistics" Padding="8" CornerRadius="6"/>
            <Button Content="📁" Command="{Binding ImportFolderCommand}" 
                    ToolTip.Tip="Import Folder" Padding="8" CornerRadius="6"/>
        </StackPanel>
        
        <!-- Right: Actions -->
        <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="8">
            <Button Content="🔄" Command="{Binding RefreshHistoryCommand}" 
                    ToolTip.Tip="Refresh (F5)" Padding="8" CornerRadius="6"/>
            <Button Content="{Binding IsGridView, Converter={x:Static vm:HistoryViewModel.ViewToggleConverter}}" 
                    Command="{Binding ToggleViewCommand}"
                    ToolTip.Tip="Toggle View" Padding="8" CornerRadius="6"/>
        </StackPanel>
    </Grid>
</Border>
```

### 2. Advanced Search Panel (Collapsible)

Add an advanced search panel that appears when the toggle is checked:

```xml
<!-- Advanced Search Panel -->
<Border Grid.Row="1" 
        IsVisible="{Binding IsAdvancedSearchVisible}"
        Background="{DynamicResource SystemControlBackgroundAltHighBrush}"
        Padding="16" Margin="12,0,12,8" CornerRadius="8">
    <Grid ColumnDefinitions="Auto,*,Auto,*,Auto,*" RowDefinitions="Auto,Auto" RowSpacing="8" ColumnSpacing="12">
        <!-- Row 1: Filename and URL -->
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Filename:" VerticalAlignment="Center"/>
        <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding FilterFilename}" CornerRadius="4"/>
        
        <TextBlock Grid.Row="0" Grid.Column="2" Text="URL:" VerticalAlignment="Center"/>
        <TextBox Grid.Row="0" Grid.Column="3" Text="{Binding FilterURL}" CornerRadius="4"/>
        
        <Button Grid.Row="0" Grid.Column="4" Content="Reset" Command="{Binding ResetFiltersCommand}" Padding="12,6" CornerRadius="4"/>
        <Button Grid.Row="0" Grid.Column="5" Content="✕ Close" Command="{Binding CloseAdvancedSearchCommand}" Padding="12,6" CornerRadius="4"/>
        
        <!-- Row 2: Date, Type, Host -->
        <CheckBox Grid.Row="1" Grid.Column="0" Content="Date:" IsChecked="{Binding FilterByDate}"/>
        <StackPanel Grid.Row="1" Grid.Column="1" Orientation="Horizontal" Spacing="8">
            <DatePicker SelectedDate="{Binding FilterFromDate}" IsEnabled="{Binding FilterByDate}"/>
            <TextBlock Text="to" VerticalAlignment="Center"/>
            <DatePicker SelectedDate="{Binding FilterToDate}" IsEnabled="{Binding FilterByDate}"/>
        </StackPanel>
        
        <CheckBox Grid.Row="1" Grid.Column="2" Content="Type:" IsChecked="{Binding FilterByType}"/>
        <ComboBox Grid.Row="1" Grid.Column="3" ItemsSource="{Binding AvailableTypes}" 
                  SelectedItem="{Binding FilterType}" IsEnabled="{Binding FilterByType}"/>
        
        <CheckBox Grid.Row="1" Grid.Column="4" Content="Host:" IsChecked="{Binding FilterByHost}"/>
        <ComboBox Grid.Row="1" Grid.Column="5" ItemsSource="{Binding AvailableHosts}" 
                  SelectedItem="{Binding FilterHost}" IsEnabled="{Binding FilterByHost}"/>
    </Grid>
</Border>
```

### 3. Split Container Layout (List + Preview)

Replace the simple ScrollViewer with a SplitView or Grid with splitter:

```xml
<!-- Main Content with Split View -->
<Grid Grid.Row="2" ColumnDefinitions="*,8,300">
    <!-- Left: History List/Grid -->
    <ScrollViewer Grid.Column="0">
        <!-- Existing ItemsControl or new DataGrid/ListBox -->
    </ScrollViewer>
    
    <!-- Splitter -->
    <GridSplitter Grid.Column="1" Width="8" Background="Transparent"/>
    
    <!-- Right: Preview Panel -->
    <Border Grid.Column="2" Margin="8" CornerRadius="8">
        <Grid RowDefinitions="*,Auto">
            <!-- Thumbnail Preview -->
            <Border Grid.Row="0" Background="#1a1a2e" CornerRadius="8" ClipToBounds="True">
                <Image Source="{Binding SelectedItem.FilePath}" Stretch="Uniform" 
                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            
            <!-- Image Info -->
            <StackPanel Grid.Row="1" Margin="8,8,8,0" Spacing="4">
                <TextBlock Text="{Binding SelectedItem.FileName}" FontWeight="SemiBold" 
                           TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding PreviewImageSize}" FontSize="11" Foreground="#6B7280"/>
            </StackPanel>
        </Grid>
    </Border>
</Grid>
```

### 4. Enhanced Context Menu

Update the context menu with full ShareX functionality:

```xml
<Border.ContextMenu>
    <ContextMenu>
        <!-- Open Section -->
        <MenuItem Header="Open">
            <MenuItem Header="🌐 URL" Command="{Binding $parent[UserControl].((vm:HistoryViewModel)DataContext).OpenURLCommand}"
                      CommandParameter="{Binding}" InputGesture="Enter"/>
            <MenuItem Header="🔗 Shortened URL" Command="{Binding ...OpenShortenedURLCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="🖼️ Thumbnail URL" Command="{Binding ...OpenThumbnailURLCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="🗑️ Deletion URL" Command="{Binding ...OpenDeletionURLCommand}" CommandParameter="{Binding}"/>
            <Separator/>
            <MenuItem Header="📄 File" Command="{Binding ...OpenFileCommand}" CommandParameter="{Binding}" InputGesture="Ctrl+Enter"/>
            <MenuItem Header="📂 Folder" Command="{Binding ...OpenFolderCommand}" CommandParameter="{Binding}" InputGesture="Shift+Enter"/>
        </MenuItem>
        
        <!-- Copy Section -->
        <MenuItem Header="Copy">
            <MenuItem Header="🌐 URL" Command="{Binding ...CopyURLCommand}" CommandParameter="{Binding}" InputGesture="Ctrl+C"/>
            <MenuItem Header="🔗 Shortened URL" Command="{Binding ...CopyShortenedURLCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="🖼️ Thumbnail URL" Command="{Binding ...CopyThumbnailURLCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="🗑️ Deletion URL" Command="{Binding ...CopyDeletionURLCommand}" CommandParameter="{Binding}"/>
            <Separator/>
            <MenuItem Header="📄 File" Command="{Binding ...CopyFileCommand}" CommandParameter="{Binding}" InputGesture="Shift+C"/>
            <MenuItem Header="🖼️ Image" Command="{Binding ...CopyImageCommand}" CommandParameter="{Binding}" InputGesture="Alt+C"/>
            <MenuItem Header="📝 Text" Command="{Binding ...CopyTextCommand}" CommandParameter="{Binding}"/>
            <Separator/>
            <MenuItem Header="HTML link" Command="{Binding ...CopyHTMLLinkCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="HTML image" Command="{Binding ...CopyHTMLImageCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="HTML linked image" Command="{Binding ...CopyHTMLLinkedImageCommand}" CommandParameter="{Binding}"/>
            <Separator/>
            <MenuItem Header="Forum (BBCode) link" Command="{Binding ...CopyForumLinkCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="Forum (BBCode) image" Command="{Binding ...CopyForumImageCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="Forum (BBCode) linked image" Command="{Binding ...CopyForumLinkedImageCommand}" CommandParameter="{Binding}"/>
            <Separator/>
            <MenuItem Header="Markdown link" Command="{Binding ...CopyMarkdownLinkCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="Markdown image" Command="{Binding ...CopyMarkdownImageCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="Markdown linked image" Command="{Binding ...CopyMarkdownLinkedImageCommand}" CommandParameter="{Binding}"/>
            <Separator/>
            <MenuItem Header="📁 File path" Command="{Binding ...CopyFilePathCommand}" CommandParameter="{Binding}" InputGesture="Ctrl+Shift+C"/>
            <MenuItem Header="📝 File name" Command="{Binding ...CopyFileNameCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="📝 File name with extension" Command="{Binding ...CopyFileNameWithExtensionCommand}" CommandParameter="{Binding}"/>
            <MenuItem Header="📂 Folder" Command="{Binding ...CopyFolderCommand}" CommandParameter="{Binding}"/>
        </MenuItem>
        
        <Separator/>
        
        <!-- Actions Section -->
        <MenuItem Header="⭐ Favorite" Command="{Binding ...ToggleFavoriteCommand}" CommandParameter="{Binding}"/>
        <MenuItem Header="🏷️ Edit tag..." Command="{Binding ...EditTagCommand}" CommandParameter="{Binding}"/>
        <MenuItem Header="✏️ Edit item..." Command="{Binding ...EditItemCommand}" CommandParameter="{Binding}"/>
        <MenuItem Header="📝 Rename file..." Command="{Binding ...RenameFileCommand}" CommandParameter="{Binding}"/>
        <MenuItem Header="🗑️ Delete item..." Command="{Binding ...DeleteItemCommand}" CommandParameter="{Binding}" InputGesture="Delete"/>
        <MenuItem Header="🗑️ Delete file &amp;&amp; item..." Command="{Binding ...DeleteFileAndItemCommand}" CommandParameter="{Binding}" InputGesture="Shift+Delete"/>
        
        <Separator/>
        
        <!-- Image Actions -->
        <MenuItem Header="🖼️ Image preview" Command="{Binding ...ShowImagePreviewCommand}" CommandParameter="{Binding}"/>
        <MenuItem Header="⬆️ Upload file" Command="{Binding ...UploadFileCommand}" CommandParameter="{Binding}" InputGesture="Ctrl+U"/>
        <MenuItem Header="✏️ Edit image" Command="{Binding ...EditImageCommand}" CommandParameter="{Binding}" InputGesture="Ctrl+E"/>
        <MenuItem Header="📌 Pin to screen" Command="{Binding ...PinToScreenCommand}" CommandParameter="{Binding}" InputGesture="Ctrl+P"/>
        <MenuItem Header="🤖 Analyze image..." Command="{Binding ...AnalyzeImageCommand}" CommandParameter="{Binding}"/>
    </ContextMenu>
</Border.ContextMenu>
```

### 5. Add ViewModel Properties and Commands

**File**: `src/ShareX.Avalonia.UI/ViewModels/HistoryViewModel.cs`

Add these properties and commands:

```csharp
// Search & Filter Properties
[ObservableProperty] private string _searchText = "";
[ObservableProperty] private bool _isAdvancedSearchVisible = false;
[ObservableProperty] private bool _showFavoritesOnly = false;

// Advanced Filter Properties
[ObservableProperty] private string _filterFilename = "";
[ObservableProperty] private string _filterURL = "";
[ObservableProperty] private bool _filterByDate = false;
[ObservableProperty] private DateTimeOffset? _filterFromDate;
[ObservableProperty] private DateTimeOffset? _filterToDate;
[ObservableProperty] private bool _filterByType = false;
[ObservableProperty] private string? _filterType;
[ObservableProperty] private bool _filterByHost = false;
[ObservableProperty] private string? _filterHost;

// Available filter options (populated from history data)
[ObservableProperty] private ObservableCollection<string> _availableTypes = new();
[ObservableProperty] private ObservableCollection<string> _availableHosts = new();

// Selection
[ObservableProperty] private HistoryItem? _selectedItem;
[ObservableProperty] private string _previewImageSize = "";

// Commands (partial methods for RelayCommand)
[RelayCommand] private void Search() { /* Apply filters */ }
[RelayCommand] private void ResetFilters() { /* Reset all filter values */ }
[RelayCommand] private void CloseAdvancedSearch() => IsAdvancedSearchVisible = false;
[RelayCommand] private void ShowStats() { /* Show statistics dialog */ }
[RelayCommand] private void ImportFolder() { /* Import folder dialog */ }

// Context Menu Commands
[RelayCommand] private void OpenURL(HistoryItem? item) { /* Open URL in browser */ }
[RelayCommand] private void OpenFile(HistoryItem? item) { /* Open file with default app */ }
[RelayCommand] private void OpenFolder(HistoryItem? item) { /* Open containing folder */ }
[RelayCommand] private void CopyURL(HistoryItem? item) { /* Copy URL to clipboard */ }
[RelayCommand] private void CopyFilePath(HistoryItem? item) { /* Copy file path */ }
[RelayCommand] private void CopyImage(HistoryItem? item) { /* Copy image to clipboard */ }
[RelayCommand] private void ToggleFavorite(HistoryItem? item) { /* Toggle favorite status */ }
[RelayCommand] private void DeleteItem(HistoryItem? item) { /* Delete from history */ }
[RelayCommand] private void DeleteFileAndItem(HistoryItem? item) { /* Delete file and history entry */ }
// ... additional commands for all context menu actions
```

### 6. Status Bar with Item Counts

Add a status bar at the bottom showing totals:

```xml
<!-- Status Bar -->
<Border Grid.Row="3" Padding="12,6" Background="{DynamicResource SystemControlBackgroundAltHighBrush}">
    <TextBlock Text="{Binding StatusText}" FontSize="12" Foreground="{DynamicResource SystemControlForegroundBaseMediumBrush}"/>
</Border>
```

ViewModel property:
```csharp
public string StatusText => $"Total: {_allHistoryItems?.Count ?? 0:N0} - Filtered: {HistoryItems.Count:N0}";
```

## Key ShareX Features to Replicate

| Feature | ShareX Control | Avalonia Equivalent |
|---------|---------------|---------------------|
| Search box | ToolStripTextBox | TextBox with Watermark |
| Search button | ToolStripButton | Button |
| Advanced Search toggle | ToolStripButton (checked) | ToggleButton |
| Favorites filter | ToolStripButton (checked) | ToggleButton |
| Show Stats | ToolStripButton | Button → Dialog |
| Import Folder | ToolStripButton | Button → FolderPicker |
| Settings | ToolStripButton | Button → Settings Dialog |
| List View | MyListView (VirtualMode) | DataGrid or ListBox |
| Split container | SplitContainerCustomSplitter | Grid + GridSplitter |
| Thumbnail preview | MyPictureBox | Image control |
| Context menu | ContextMenuStrip | ContextMenu |
| Advanced filters | TextBox, DateTimePicker, ComboBox | TextBox, DatePicker, ComboBox |

## Guidelines
- **Follow ShareX.Avalonia UI patterns** (dark theme, rounded corners, spacing)
- **Use existing HistoryManager** from `ShareX.Avalonia.History` project
- **Apply filters reactively** - filter as user types or changes filter options
- **Virtual scrolling** for large history lists (if possible with ItemsControl)
- **Keyboard shortcuts** - F5 (refresh), Enter (open), Ctrl+C (copy URL), Delete (delete item)
- **Ensure multi-select support** for batch operations

## Integration Notes

This task focuses on the **UI layer only**. The `ShareX.Avalonia.History` project already contains:
- `HistoryItem`, `HistoryFilter`, `HistorySettings`
- `HistoryManager`, `HistoryManagerXML`, `HistoryManagerSQLite`
- `HistoryHelpers` for statistics

**Don't worry about**:
- Creating new History backend classes
- Image upload integration (handled elsewhere)

## Deliverables
- ✅ `HistoryView.axaml` updated with enhanced toolbar, advanced search, split view
- ✅ `HistoryViewModel.cs` has all filter properties and commands
- ✅ Context menu provides full Open/Copy/Edit/Delete functionality
- ✅ Preview panel shows selected item thumbnail
- ✅ Status bar displays total/filtered counts
- ✅ Build succeeds on `feature/history-ui`
- ✅ Commit and push changes

## Testing

### Visual Test
1. **Run app** and navigate to History view
2. **Verify toolbar** displays Search, Advanced Search toggle, Favorites, Stats, Import, Refresh, View toggle
3. **Toggle Advanced Search** - verify filter panel appears/disappears
4. **Enter search text** - verify list filters
5. **Select an item** - verify thumbnail preview updates
6. **Test context menu** - right-click should show all options

### Functional Test
1. **Search** - type partial filename, verify results filter
2. **Filter by date** - enable date filter, set range, verify filtering
3. **Favorites** - toggle favorites filter, verify only favorites show
4. **Copy URL** - right-click → Copy → URL, paste to verify
5. **Open folder** - right-click → Open → Folder, verify explorer opens
6. **Delete item** - right-click → Delete item, confirm dialog, verify removal
7. **Keyboard shortcuts** - F5 refreshes, Enter opens, Delete prompts deletion

### Integration Test
After capture workflow integration:
1. **Capture screenshot** (Ctrl+PrintScreen)
2. **Navigate to History** - verify new item appears
3. **Click new item** - verify preview shows captured image
4. **Edit image** - verify opens in Editor

## Estimated Effort
**Medium-High** - 4-6 hours (due to extensive context menu and filter implementation)

## Reference Files (ShareX)
- `ShareX.HistoryLib/Forms/HistoryForm.cs` - Main history form logic
- `ShareX.HistoryLib/Forms/HistoryForm.Designer.cs` - UI layout
- `ShareX.HistoryLib/HistoryItemManager.cs` - Item selection and actions
- `ShareX.HistoryLib/HistoryItemManager_ContextMenu.cs` - Context menu building
- `ShareX.HistoryLib/HistoryFilter.cs` - Filter logic
- `ShareX.HistoryLib/HistorySettings.cs` - Settings persistence
- `ShareX.HistoryLib/Forms/ImageHistoryForm.cs` - Grid-based image view (alternative)
