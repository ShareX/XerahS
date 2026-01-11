# CX05: Plugin Packaging System - .sxadp Installer

## Priority
**MEDIUM** - Enables user-friendly plugin installation workflow

## Assignee
**Codex** (Surface Laptop 5, VS Code)

## Branch
`feature/plugin-packaging`

## Instructions
**CRITICAL**: Create the `feature/plugin-packaging` branch first before starting work.

```bash
git checkout master
git pull origin master
git checkout -b feature/plugin-packaging
```

## Objective
Implement the `.sxadp` (ShareX Avalonia Plugin) packaging system to allow users to install plugins via file picker or drag & drop, eliminating manual extraction to `Plugins/` folder.

## Background
Currently, plugins must be **manually extracted** to `Plugins/PluginName/` directory. This task implements:
- **Phase 1**: `PluginPackager` backend (ZIP compression/extraction)
- **Phase 2**: `PluginInstallerDialog` UI (file picker, metadata preview, install button)

Reference: `docs/plugin_packaging_system.md` (comprehensive implementation plan)

## Scope

### Phase 1: PluginPackager Backend

**File**: `src/ShareX.Avalonia.Uploaders/PluginSystem/PluginPackager.cs` (NEW)

```csharp
using System.IO.Compression;
using Newtonsoft.Json;
using ShareX.Avalonia.Common;

namespace ShareX.Avalonia.Uploaders.PluginSystem;

/// <summary>
/// Handles packaging and installation of .sxadp plugin files
/// </summary>
public static class PluginPackager
{
    private const string ManifestFileName = "plugin.json";
    private const long MaxPackageSize = 100_000_000; // 100MB
    
    /// <summary>
    /// Package a plugin directory into .sxadp file
    /// </summary>
    public static string Package(string pluginDirectory, string outputFilePath)
    {
        // 1. Validate plugin directory has plugin.json
        string manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"{ManifestFileName} not found in {pluginDirectory}");
        
        // 2. Load and validate manifest
        var manifest = LoadAndValidateManifest(manifestPath);
        
        // 3. Create ZIP archive
        if (File.Exists(outputFilePath))
            File.Delete(outputFilePath);
            
        ZipFile.CreateFromDirectory(pluginDirectory, outputFilePath);
        
        DebugHelper.WriteLine($"Plugin packaged: {outputFilePath}");
        return outputFilePath;
    }
    
    /// <summary>
    /// Extract and install .sxadp package to Plugins directory
    /// </summary>
    public static PluginMetadata? InstallPackage(string packageFilePath, string pluginsDirectory)
    {
        // 1. Validate package file
        if (!File.Exists(packageFilePath))
            throw new FileNotFoundException("Package file not found", packageFilePath);
        
        var fileInfo = new FileInfo(packageFilePath);
        if (fileInfo.Length > MaxPackageSize)
            throw new InvalidDataException($"Package exceeds maximum size of {MaxPackageSize / 1_000_000}MB");
        
        // 2. Extract to temp directory first
        string tempDir = Path.Combine(Path.GetTempPath(), $"sxadp_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            ZipFile.ExtractToDirectory(packageFilePath, tempDir);
            
            // 3. Validate manifest exists
            string manifestPath = Path.Combine(tempDir, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new InvalidDataException($"Package does not contain {ManifestFileName}");
            
            // 4. Load and validate manifest
            var manifest = LoadAndValidateManifest(manifestPath);
            
            // 5. Check if plugin already exists
            string targetDir = Path.Combine(pluginsDirectory, manifest.PluginId);
            if (Directory.Exists(targetDir))
            {
                throw new InvalidOperationException(
                    $"Plugin '{manifest.PluginId}' (v{manifest.Version}) is already installed. " +
                    $"Please uninstall it first or use a different plugin ID.");
            }
            
            // 6. Validate assembly exists
            string assemblyFileName = manifest.GetAssemblyFileName();
            string assemblyPath = Path.Combine(tempDir, assemblyFileName);
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly not found: {assemblyFileName}");
            
            // 7. Move from temp to Plugins directory
            Directory.Move(tempDir, targetDir);
            
            // 8. Create metadata
            string finalAssemblyPath = Path.Combine(targetDir, assemblyFileName);
            var metadata = new PluginMetadata(manifest, targetDir, finalAssemblyPath);
            
            DebugHelper.WriteLine($"Plugin installed: {manifest.Name} v{manifest.Version} to {targetDir}");
            return metadata;
        }
        catch
        {
            // Cleanup temp directory on failure
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); }
                catch { /* Ignore cleanup errors */ }
            }
            throw;
        }
    }
    
    /// <summary>
    /// Load manifest from .sxadp file without installing
    /// </summary>
    public static PluginManifest? PreviewPackage(string packageFilePath)
    {
        if (!File.Exists(packageFilePath))
            return null;
        
        using (var archive = ZipFile.OpenRead(packageFilePath))
        {
            var manifestEntry = archive.GetEntry(ManifestFileName);
            if (manifestEntry == null)
                return null;
            
            using (var stream = manifestEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<PluginManifest>(json);
            }
        }
    }
    
    private static PluginManifest LoadAndValidateManifest(string manifestPath)
    {
        string json = File.ReadAllText(manifestPath);
        var manifest = JsonConvert.DeserializeObject<PluginManifest>(json);
        
        if (manifest == null)
            throw new InvalidDataException("Failed to deserialize manifest");
        
        if (!manifest.IsValid(out string? error))
            throw new InvalidDataException($"Invalid manifest: {error}");
        
        return manifest;
    }
}
```

### Phase 2: PluginInstallerDialog UI

**File 1**: `src/ShareX.Avalonia.UI/Views/PluginInstallerDialog.axaml` (NEW)

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:ShareX.Avalonia.UI.ViewModels"
        x:Class="ShareX.Avalonia.UI.Views.PluginInstallerDialog"
        x:DataType="vm:PluginInstallerViewModel"
        Title="Install Plugin"
        Width="500" Height="400"
        CanResize="False"
        WindowStartupLocation="CenterOwner">
    
    <Grid Margin="20" RowDefinitions="Auto,*,Auto,Auto">
        
        <!-- File Selection -->
        <StackPanel Grid.Row="0" Spacing="10">
            <TextBlock Text="Plugin Package (.sxadp)" FontWeight="SemiBold"/>
            <Grid ColumnDefinitions="*,Auto">
                <TextBox Grid.Column="0" 
                         Text="{Binding PackageFilePath}" 
                         IsReadOnly="True"
                         Watermark="Select a .sxadp file..."/>
                <Button Grid.Column="1" 
                        Content="Browse..." 
                        Command="{Binding BrowsePackageCommand}"
                        Margin="5,0,0,0"/>
            </Grid>
        </StackPanel>
        
        <!-- Plugin Info Preview -->
        <Border Grid.Row="1" 
                Margin="0,20,0,0"
                Padding="15"
                Background="{DynamicResource SystemControlBackgroundAltHighBrush}"
                CornerRadius="4"
                IsVisible="{Binding Manifest, Converter={x:Static ObjectConverters.IsNotNull}}">
            <StackPanel Spacing="8">
                <TextBlock Text="{Binding Manifest.Name}" 
                           FontSize="18" 
                           FontWeight="SemiBold"/>
                <TextBlock Text="{Binding ManifestVersionAuthor}" 
                           Foreground="{DynamicResource SystemControlForegroundBaseMediumBrush}"/>
                <TextBlock Text="{Binding Manifest.Description}" 
                           TextWrapping="Wrap"
                           Margin="0,5,0,0"/>
                <Separator Margin="0,10"/>
                <StackPanel Spacing="4">
                    <TextBlock>
                        <Run Text="Plugin ID: "/>
                        <Run Text="{Binding Manifest.PluginId}" FontFamily="Consolas"/>
                    </TextBlock>
                    <TextBlock>
                        <Run Text="API Version: "/>
                        <Run Text="{Binding Manifest.ApiVersion}"/>
                    </TextBlock>
                    <TextBlock Text="{Binding ManifestCategories}"/>
                </StackPanel>
            </StackPanel>
        </Border>
        
        <!-- Error Message -->
        <TextBlock Grid.Row="2"
                   Text="{Binding ErrorMessage}"
                   Foreground="Red"
                   TextWrapping="Wrap"
                   Margin="0,10,0,0"
                   IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        
        <!-- Action Buttons -->
        <StackPanel Grid.Row="3" 
                    Orientation="Horizontal" 
                    HorizontalAlignment="Right"
                    Spacing="10"
                    Margin="0,20,0,0">
            <Button Content="Install" 
                    Command="{Binding InstallCommand}"
                    IsEnabled="{Binding CanInstall}"
                    Classes="accent"/>
            <Button Content="Cancel" 
                    Command="{Binding CancelCommand}"/>
        </StackPanel>
        
    </Grid>
</Window>
```

**File 2**: `src/ShareX.Avalonia.UI/ViewModels/PluginInstallerViewModel.cs` (NEW)

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Uploaders.PluginSystem;
using Avalonia.Platform.Storage;

namespace ShareX.Avalonia.UI.ViewModels;

public partial class PluginInstallerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _packageFilePath = string.Empty;
    
    [ObservableProperty]
    private PluginManifest? _manifest;
    
    [ObservableProperty]
    private string? _errorMessage;
    
    [ObservableProperty]
    private bool _isInstalling;
    
    public bool CanInstall => Manifest != null && !IsInstalling;
    
    public string ManifestVersionAuthor => 
        Manifest != null ? $"Version {Manifest.Version} by {Manifest.Author}" : string.Empty;
    
    public string ManifestCategories =>
        Manifest != null ? $"Categories: {string.Join(", ", Manifest.SupportedCategories)}" : string.Empty;
    
    [RelayCommand]
    private async Task BrowsePackage()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is 
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;
        
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Plugin Package",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("ShareX Avalonia Plugin")
                {
                    Patterns = new[] { "*.sxadp" }
                }
            }
        });
        
        if (files.Count > 0)
        {
            PackageFilePath = files[0].Path.LocalPath;
            await LoadManifestPreview();
        }
    }
    
    private async Task LoadManifestPreview()
    {
        ErrorMessage = null;
        Manifest = null;
        
        try
        {
            Manifest = PluginPackager.PreviewPackage(PackageFilePath);
            if (Manifest == null)
            {
                ErrorMessage = "Invalid package: plugin.json not found";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load package: {ex.Message}";
        }
        
        OnPropertyChanged(nameof(CanInstall));
        await Task.CompletedTask;
    }
    
    [RelayCommand]
    private async Task Install()
    {
        if (Manifest == null) return;
        
        IsInstalling = true;
        ErrorMessage = null;
        
        try
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            Directory.CreateDirectory(pluginsDir);
            
            var metadata = PluginPackager.InstallPackage(PackageFilePath, pluginsDir);
            
            if (metadata != null)
            {
                // Reload plugins
                ProviderCatalog.LoadPlugins(pluginsDir);
                
                // Close dialog with success
                // (In code-behind, set DialogResult = true)
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Installation failed: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            OnPropertyChanged(nameof(CanInstall));
        }
    }
    
    [RelayCommand]
    private void Cancel()
    {
        // Close dialog
        // (In code-behind, set DialogResult = false)
    }
}
```

**File 3**: `src/ShareX.Avalonia.UI/Views/PluginInstallerDialog.axaml.cs` (NEW)

```csharp
using Avalonia.Controls;
using ShareX.Avalonia.UI.ViewModels;

namespace ShareX.Avalonia.UI.Views;

public partial class PluginInstallerDialog : Window
{
    public PluginInstallerDialog()
    {
        InitializeComponent();
        DataContext = new PluginInstallerViewModel();
        
        // Wire up commands to close dialog
        if (DataContext is PluginInstallerViewModel vm)
        {
            vm.InstallCommand.ExecuteAsync(null).ContinueWith(t =>
            {
                if (vm.ErrorMessage == null)
                {
                    Dispatcher.UIThread.Post(() => Close(true));
                }
            });
            
            vm.CancelCommand.Execute(null);
        }
    }
}
```

### Phase 3: Integration with Settings UI

**File**: `src/ShareX.Avalonia.UI/ViewModels/ApplicationSettingsViewModel.cs`

Add command to open installer dialog:

```csharp
[RelayCommand]
private async Task OpenPluginInstaller()
{
    var dialog = new PluginInstallerDialog();
    var result = await dialog.ShowDialog<bool>(App.MainWindow);
    
    if (result)
    {
        // Refresh uploader providers list
        // TODO: Add method to refresh UI after plugin installation
    }
}
```

**File**: `src/ShareX.Avalonia.UI/Views/ApplicationSettingsView.axaml`

Add button to Uploaders tab (find the appropriate location):

```xml
<Button Content="Install Plugin..."
        Command="{Binding OpenPluginInstallerCommand}"
        HorizontalAlignment="Right"
        Margin="0,0,0,10"/>
```

## Guidelines
- **Use System.IO.Compression** for ZIP operations (built-in .NET)
- **Validate manifest** before extraction to prevent malformed packages
- **Size limit**: Enforce 100MB maximum package size
- **Error handling**: Clear error messages for users
- **Debug logging**: Log all package operations
- **XML doc comments** for public methods

## Don't Worry About
- Drag & drop support (Phase 3, future work)
- File association with .sxadp (Phase 3, future work)
- Plugin uninstall UI (future work)
- Code signing verification (future work)

## Deliverables
- ✅ `PluginPackager.cs` with Package(), InstallPackage(), PreviewPackage()
- ✅ `PluginInstallerDialog.axaml` + ViewModel + code-behind
- ✅ Integration with ApplicationSettingsView
- ✅ Build succeeds on `feature/plugin-packaging`
- ✅ Manual test: Install a .sxadp package successfully
- ✅ Commit and push changes

## Testing

### Manual Test

1. **Create test plugin package**:
   - Use existing Imgur plugin folder
   - ZIP it manually: `Compress-Archive -Path "Plugins\imgur" -DestinationPath "imgur.sxadp"`

2. **Test installer UI**:
   - Run app → Settings → Uploaders
   - Click "Install Plugin..." button
   - Browse to `imgur.sxadp`
   - Verify manifest preview shows correctly
   - Click "Install"
   - Check `Plugins/imgur/` folder created

3. **Verify plugin loads**:
   - Restart app
   - Check Debug output for "Loaded plugin: Imgur"
   - Verify Imgur appears in uploader providers list

4. **Test error cases**:
   - Try installing same plugin twice (should show error)
   - Try installing invalid ZIP file (should show error)
   - Try installing package > 100MB (should show error)

### Expected Debug Output
```
[PluginPackager] Plugin packaged: imgur.sxadp
[PluginPackager] Plugin installed: Imgur Uploader v1.0.0 to Plugins\imgur
[ProviderCatalog] Loading plugins from: Plugins
[ProviderCatalog] ✓ Loaded: Imgur Uploader
```

## Estimated Effort
**Medium-High** - 4-5 hours
- Phase 1 (Backend): 2 hours
- Phase 2 (UI): 2-3 hours

## Reference
See `docs/plugin_packaging_system.md` for complete implementation plan.
