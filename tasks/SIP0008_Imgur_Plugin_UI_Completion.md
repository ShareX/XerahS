# AG04: Imgur Plugin - Complete UI Controls Replication

## Priority
**HIGH** - Achieves feature parity with ShareX WinForms Imgur configuration

## Assignee
**Antigravity** (Architecture & Plugin Integration)

## Branch
`feature/imgur-plugin-ui`

## Instructions
**CRITICAL**: Create the `feature/imgur-plugin-ui` branch first before starting work.

```powershell
git checkout master
git pull origin master
git checkout -b feature/imgur-plugin-ui
```

## Objective
Replicate all Imgur UI controls currently in `ShareX.UploadersLib` to work in `ShareX.Imgur.Plugin` as an Avalonia-based plugin, achieving feature parity with the WinForms implementation.

## Background
The current `ShareX.Imgur.Plugin` has basic UI functionality but is missing several features present in the ShareX WinForms implementation. Based on analysis of `ShareX.UploadersLib\Forms\UploadersConfigForm.Designer.cs` (lines 517-529), the WinForms version includes these controls:
- `cbImgurUseGIFV` - CheckBox
- `cbImgurUploadSelectedAlbum` - CheckBox
- `cbImgurDirectLink` - CheckBox ✅ (Already implemented)
- `atcImgurAccountType` - AccountTypeControl ✅ (Already implemented as ComboBox)
- `oauth2Imgur` - OAuthControl ✅ (Already implemented)
- `lvImgurAlbumList` - ListView (Albums) 🔴 (Missing - critical!)
- `btnImgurRefreshAlbumList` - Button ✅ (Implemented as "Fetch Albums")
- `cbImgurThumbnailType` - ComboBox ✅ (Already implemented)

**Current Status**: 
- ✅ Basic OAuth workflow implemented
- ✅ Album ID manual entry supported
- 🔴 **Missing**: Album selection ListView
- 🔴 **Missing**: "Upload to Selected Album" checkbox
- 🔴 **Missing**: "Use GIFV" checkbox
- 🔴 **Missing**: Visual album browsing UI

## Scope

### 1. Add Missing UI Controls to ImgurConfigView.axaml

**File**: `src/Plugins/ShareX.Imgur.Plugin/Views/ImgurConfigView.axaml`

#### 1.1 Add "Use GIFV" CheckBox
Insert after the "Use direct link" checkbox (line 97):

```xml
<!-- Use GIFV for GIF images -->
<CheckBox Content="Use GIFV for GIF images (convert to MP4)"
          IsChecked="{Binding UseGifv}"  
          Margin="0,5,0,0">
    <ToolTip.Tip>
        <TextBlock Text="Convert GIF images to GIFV (MP4 video) for better performance and smaller file sizes"
                   TextWrapping="Wrap"
                   MaxWidth="300"/>
    </ToolTip.Tip>
</CheckBox>
```

#### 1.2 Replace Manual Album ID Entry with Album ListView
Replace the current "Album ID (Optional)" section (lines 72-79) with:

```xml
<!-- Album Selection (Only visible for User Account mode) -->
<StackPanel Spacing="10" IsVisible="{Binding AccountTypeIndex}">
    <Border Background="{DynamicResource SystemFillColorSecondaryBackground}"
            Padding="15"
            CornerRadius="4">
        <StackPanel Spacing="10">
            <TextBlock Text="Album Selection" FontWeight="SemiBold"/>
            
            <!-- Upload to Album Checkbox -->
            <CheckBox Content="Upload to selected album"
                      IsChecked="{Binding UploadToSelectedAlbum}"/>
            
            <!-- Album List -->
            <StackPanel Spacing="5" IsVisible="{Binding UploadToSelectedAlbum}">
                <StackPanel Orientation="Horizontal" Spacing="10">
                    <TextBlock Text="Available Albums" 
                               FontWeight="Medium"
                               VerticalAlignment="Center"/>
                    <Button Content="Refresh"
                            Command="{Binding FetchAlbumsCommand}"
                            IsEnabled="{Binding IsLoggedIn}"/>
                </StackPanel>
                
                <!-- Album DataGrid -->
                <DataGrid Items="{Binding Albums}"
                          SelectedItem="{Binding SelectedAlbum}"
                          Height="200"
                          AutoGenerateColumns="False"
                          IsReadOnly="True"
                          GridLinesVisibility="All">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="ID" 
                                            Binding="{Binding id}" 
                                            Width="120"/>
                        <DataGridTextColumn Header="Title" 
                                            Binding="{Binding title}" 
                                            Width="*"/>
                        <DataGridTextColumn Header="Description" 
                                            Binding="{Binding description}" 
                                            Width="2*"/>
                    </DataGrid.Columns>
                </DataGrid>
                
                <TextBlock Text="{Binding AlbumStatusMessage}"
                           FontSize="11"
                           Foreground="{DynamicResource SystemAccentColor}"
                           IsVisible="{Binding !!AlbumStatusMessage}"/>
            </StackPanel>
        </StackPanel>
    </Border>
</StackPanel>
```

### 2. Update ImgurConfigViewModel

**File**: `src/Plugins/ShareX.Imgur.Plugin/ViewModels/ImgurConfigViewModel.cs`

#### 2.1 Add Missing Properties

Add these observable properties to support the new controls:

```csharp
[ObservableProperty]
private bool _useGifv = false;

[ObservableProperty]
private bool _uploadToSelectedAlbum = false;

[ObservableProperty]
private ObservableCollection<ImgurAlbumData> _albums = new();

[ObservableProperty]
private ImgurAlbumData? _selectedAlbum;

[ObservableProperty]
private string? _albumStatusMessage;
```

#### 2.2 Update FetchAlbums Command

Replace the current `FetchAlbums` method (lines 87-105) with:

```csharp
[RelayCommand]
private void FetchAlbums()
{
    if (_uploader == null || !IsLoggedIn)
    {
        AlbumStatusMessage = "You must be logged in to fetch albums";
        return;
    }

    try
    {
        var albumList = _uploader.GetAlbums();
        if (albumList != null && albumList.Count > 0)
        {
            Albums.Clear();
            foreach (var album in albumList)
            {
                Albums.Add(album);
            }
            AlbumStatusMessage = $"Loaded {albumList.Count} albums";
        }
        else
        {
            Albums.Clear();
            AlbumStatusMessage = "No albums found or failed to fetch";
        }
    }
    catch (Exception ex)
    {
        AlbumStatusMessage = $"Error fetching albums: {ex.Message}";
    }
}
```

#### 2.3 Update LoadFromJson

Update the `LoadFromJson` method (lines 107-129) to include new properties:

```csharp
public void LoadFromJson(string json)
{
    try
    {
        var config = JsonConvert.DeserializeObject<ImgurConfigModel>(json);
        if (config != null)
        {
            _config = config;
            _uploader = new ImgurUploader(_config);
            
            ClientId = _config.ClientId ?? "30d41ft9z9r8jtt";
            AccountTypeIndex = (int)_config.AccountType;
            ThumbnailTypeIndex = (int)_config.ThumbnailType;
            UseDirectLink = _config.DirectLink;
            UseGifv = _config.UseGIFV;  // NEW
            UploadToSelectedAlbum = _config.UploadToSelectedAlbum;  // NEW
            IsLoggedIn = OAuth2Info.CheckOAuth(_config.OAuth2Info);
            
            // Load selected album if exists
            if (_config.SelectedAlbum != null)
            {
                SelectedAlbum = _config.SelectedAlbum;
            }
        }
    }
    catch
    {
        StatusMessage = "Failed to load configuration";
    }
}
```

#### 2.4 Update ToJson

Update the `ToJson` method (lines 131-145) to save new properties:

```csharp
public string ToJson()
{
    _config.ClientId = ClientId;
    _config.AccountType = (AccountType)AccountTypeIndex;
    _config.ThumbnailType = (ImgurThumbnailType)ThumbnailTypeIndex;
    _config.DirectLink = UseDirectLink;
    _config.UseGIFV = UseGifv;  // NEW
    _config.UploadToSelectedAlbum = UploadToSelectedAlbum;  // NEW

    // Save selected album
    if (UploadToSelectedAlbum && SelectedAlbum != null)
    {
        _config.SelectedAlbum = SelectedAlbum;
    }
    else
    {
        _config.SelectedAlbum = null;
    }

    return JsonConvert.SerializeObject(_config, Formatting.Indented);
}
```

### 3. Update ImgurConfigModel

**File**: `src/Plugins/ShareX.Imgur.Plugin/ImgurConfigModel.cs`

Ensure the model includes all necessary properties:

```csharp
public class ImgurConfigModel
{
    public string? ClientId { get; set; }
    public AccountType AccountType { get; set; }
    public OAuth2Info OAuth2Info { get; set; } = new();
    public ImgurAlbumData? SelectedAlbum { get; set; }
    public bool UploadToSelectedAlbum { get; set; }
    public ImgurThumbnailType ThumbnailType { get; set; }
    public bool DirectLink { get; set; } = true;
    public bool UseGIFV { get; set; }  // NEW - Add if missing
}
```

### 4. Update ImgurUploader.cs

**File**: `src/Plugins/ShareX.Imgur.Plugin/ImgurUploader.cs`

Ensure the upload logic respects the new settings:

#### 4.1 Apply UseGIFV Setting
In the `Upload` method, check for GIF files and apply GIFV conversion:

```csharp
public override UploadResult Upload(Stream stream, string fileName)
{
    // Check if GIFV should be used for GIF images
    if (_config.UseGIFV && fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
    {
        // Pass prefer_video=true parameter to Imgur API
        // This will be handled in InternalUpload
    }
    
    return InternalUpload(stream, fileName);
}
```

#### 4.2 Ensure Album Upload Works
Verify that `UploadToSelectedAlbum` and `SelectedAlbum` are correctly used in the upload logic.

## Guidelines
- **Follow Plugin Architecture**: Reference `plugin_development_guide.md` and `plugin_implementation_plan.md`
- **Match ShareX Behavior**: Ensure checkbox logic matches WinForms implementation
- **DataGrid Best Practices**: Use Avalonia DataGrid for album list (consistent with Avalonia UI patterns)
- **Settings Persistence**: All settings must persist via `ToJson()`/`LoadFromJson()`
- **User Feedback**: Provide clear status messages for album operations

## Integration Notes

This task builds upon the existing Imgur plugin infrastructure. Key dependencies:
- ✅ `ImgurUploader.cs` - Already has `GetAlbums()` method
- ✅ `ImgurAlbumData.cs` - Album model exists
- ✅ OAuth workflow - Complete
- 🔴 **Missing**: `UseGIFV` property in config model
- 🔴 **Missing**: Album list UI binding

## Deliverables

- [ ] `ImgurConfigView.axaml` updated with:
  - [ ] "Use GIFV" checkbox
  - [ ] "Upload to selected album" checkbox  
  - [ ] Album DataGrid with ID/Title/Description columns
  - [ ] Refresh button integrated
- [ ] `ImgurConfigViewModel.cs` updated with:
  - [ ] `UseGifv` property
  - [ ] `UploadToSelectedAlbum` property
  - [ ] `Albums` ObservableCollection
  - [ ] `SelectedAlbum` property
  - [ ] Enhanced `FetchAlbums` command
  - [ ] Updated `LoadFromJson`/`ToJson` methods
- [ ] `ImgurConfigModel.cs` includes `UseGIFV` property
- [ ] `ImgurUploader.cs` respects GIFV setting during upload
- [ ] Build succeeds on `feature/imgur-plugin-ui`
- [ ] All settings persist correctly
- [ ] Plugin loads dynamically in Destination Settings
- [ ] Commit and push changes

## Testing

### Visual Test
1. **Run app** and navigate to Destinations → Destination Settings
2. **Add Imgur instance** from catalog
3. **Select User Account** mode
4. **Complete OAuth login**
5. **Click "Fetch Albums"** - verify DataGrid populates
6. **Select an album** from the list - verify selection highlighting
7. **Check "Upload to selected album"** - verify setting saves
8. **Check "Use GIFV for GIF images"** - verify setting saves
9. **Toggle "Use direct link"** and thumbnail type - verify all options work
10. **Restart app** - verify all settings persist

### Functional Test
1. **Configure Imgur** with album selection enabled
2. **Upload a GIF** - verify GIFV conversion if enabled
3. **Upload to selected album** - verify image appears in correct album on Imgur
4. **Upload without album selection** - verify upload to account root
5. **Test Anonymous mode** - verify album controls hidden

### Integration Test
1. **Compare with ShareX WinForms** - verify UI parity
2. **Test all thumbnail types** - ensure URLs generated correctly
3. **Test OAuth refresh** - verify token persistence
4. **Test with multiple instances** - verify instance isolation

## Comparison: ShareX WinForms vs ShareX.Avalonia Plugin

| Feature | WinForms (`UploadersConfigForm`) | Avalonia Plugin (Current) | After AG04 |
|---------|----------------------------------|---------------------------|------------|
| OAuth Login | ✅ OAuthControl | ✅ Step 1/2 workflow | ✅ Same |
| Account Type | ✅ AccountTypeControl | ✅ ComboBox | ✅ Same |
| Direct Link | ✅ CheckBox | ✅ CheckBox | ✅ Same |
| Thumbnail Type | ✅ ComboBox | ✅ ComboBox | ✅ Same |
| Use GIFV | ✅ CheckBox | ❌ Missing | ✅ CheckBox |
| Upload to Album | ✅ CheckBox | ❌ Implied by AlbumId | ✅ CheckBox |
| Album Selection | ✅ ListView (3 cols) | 🟡 Manual ID entry | ✅ DataGrid (3 cols) |
| Refresh Albums | ✅ Button | ✅ Button | ✅ Button (integrated) |

## Estimated Effort
**Medium-High** - 4-6 hours
- UI layout changes: 1-2 hours
- ViewModel logic: 2-3 hours  
- Testing & polish: 1-2 hours

## Dependencies
- Must reference `ImgurAlbumData.cs` for DataGrid binding
- Requires `CommunityToolkit.Mvvm` for `ObservableCollection`
- Relies on existing `ImgurUploader.GetAlbums()` implementation

## Notes
- The WinForms version uses `MyListView` (custom control), Avalonia uses standard `DataGrid`
- Album descriptions may be null - UI should handle gracefully
- DataGrid selection should update `SelectedAlbum` property for bi-directional binding
- Consider adding column sorting in future enhancement
