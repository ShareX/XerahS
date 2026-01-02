# Plugin Packaging System Implementation Plan

**Status**: 📋 Planned  
**Extension**: `.sxap` (ShareX Avalonia Plugin)  
**Inspired by**: ShareX `.sxie` (ShareX Image Effect) format

---

## 1. Analysis Summary

### ShareX .sxie Format
ShareX uses `.sxie` files to package ImageEffects with their assets:

**Structure**:
```
MyEffect.sxie (ZIP archive)
├── Config.json          # ImageEffectPreset serialized as JSON
└── MyEffect/            # Assets folder (images, fonts, etc.)
    ├── background.png
    └── overlay.png
```

**Key Implementation** ([ImageEffectPackager.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX/ShareX.ImageEffectsLib/ImageEffectPackager.cs)):
- Uses `ZipManager.Compress()` to create archive
- Embeds `Config.json` (UTF-8 encoded JSON)
- Includes assets from a designated folder
- Extraction validates file types (images only)
- 100MB size limit for security

### ShareX.Avalonia Current Plugin System
**Discovery**: `PluginDiscovery` scans `Plugins/` directory for subdirectories containing `plugin.json`  
**Loading**: `PluginLoader` uses `AssemblyLoadContext` for isolation  
**Registration**: `ProviderCatalog` maintains registry of loaded providers

**Current Limitation**: Plugins must be **manually extracted** to `Plugins/PluginName/` folder

---

## 2. Proposed Solution: `.sxap` Format

### Extension Name Recommendation
**`.sxap`** = **S**hare**X** **A**valonia **P**lugin

**Rationale**:
- Follows ShareX naming convention (`.sxie` for Image Effects)
- Clear association with ShareX.Avalonia
- Short, memorable, unique extension
- Avoids conflicts with existing formats

**Alternative considered**: `.sxplugin` (too verbose)

---

## 3. Package Structure

### Archive Format
`.sxap` files are **ZIP archives** with the following structure:

```
ImgurUploader.sxap (ZIP archive)
├── plugin.json                          # Manifest (REQUIRED)
├── ShareX.Uploader.Imgur.dll            # Main assembly (REQUIRED)
├── Newtonsoft.Json.dll                  # Dependencies (OPTIONAL)
├── README.md                            # Documentation (OPTIONAL)
├── LICENSE.txt                          # License (OPTIONAL)
└── Assets/                              # Assets folder (OPTIONAL)
    └── icon.png
```

### Manifest Example (`plugin.json`)
```json
{
  "id": "imgur",
  "name": "Imgur Uploader",
  "version": "1.2.0",
  "author": "ShareX Team",
  "description": "Upload images to Imgur anonymously or with account",
  "apiVersion": "1.0",
  "entryPoint": "ShareX.Uploader.Imgur.ImgurProvider",
  "assemblyFileName": "ShareX.Uploader.Imgur.dll",
  "supportedCategories": ["Image"],
  "homepageUrl": "https://github.com/ShareX/ShareX.Avalonia",
  "dependencies": []
}
```

---

## 4. Implementation Components

### A. PluginPackager (Packaging Tool)

**Location**: `ShareX.Avalonia.Uploaders/PluginSystem/PluginPackager.cs`

```csharp
public static class PluginPackager
{
    private const string ManifestFileName = "plugin.json";
    
    /// <summary>
    /// Package a plugin directory into .sxap file
    /// </summary>
    public static string Package(string pluginDirectory, string outputFilePath)
    {
        // Validate plugin directory has plugin.json
        string manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("plugin.json not found");
        
        // Validate manifest
        var manifest = LoadAndValidateManifest(manifestPath);
        
        // Create ZIP archive
        using (var archive = ZipFile.Open(outputFilePath, ZipArchiveMode.Create))
        {
            // Add all files from plugin directory
            foreach (var file in Directory.GetFiles(pluginDirectory, "*.*", SearchOption.AllDirectories))
            {
                string entryName = Path.GetRelativePath(pluginDirectory, file);
                archive.CreateEntryFromFile(file, entryName);
            }
        }
        
        return outputFilePath;
    }
    
    /// <summary>
    /// Extract and install .sxap package to Plugins directory
    /// </summary>
    public static PluginMetadata? InstallPackage(string packageFilePath, string pluginsDirectory)
    {
        if (!File.Exists(packageFilePath))
            throw new FileNotFoundException("Package file not found");
        
        // Extract to temp directory first
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        ZipFile.ExtractToDirectory(packageFilePath, tempDir);
        
        try
        {
            // Validate manifest exists
            string manifestPath = Path.Combine(tempDir, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new InvalidDataException("Package does not contain plugin.json");
            
            // Load and validate manifest
            var manifest = LoadAndValidateManifest(manifestPath);
            
            // Check if plugin already exists
            string targetDir = Path.Combine(pluginsDirectory, manifest.PluginId);
            if (Directory.Exists(targetDir))
            {
                // Prompt user for overwrite or version check
                // For now, throw exception
                throw new InvalidOperationException($"Plugin '{manifest.PluginId}' already installed");
            }
            
            // Move from temp to Plugins directory
            Directory.Move(tempDir, targetDir);
            
            // Create metadata
            string assemblyPath = Path.Combine(targetDir, manifest.GetAssemblyFileName());
            return new PluginMetadata(manifest, targetDir, assemblyPath);
        }
        catch
        {
            // Cleanup temp directory on failure
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            throw;
        }
    }
}
```

### B. PluginInstaller UI

**Location**: `ShareX.Avalonia.UI/Views/PluginInstallerDialog.axaml`

**Features**:
- File picker for `.sxap` files
- Display plugin metadata (name, version, author, description)
- Show installation location
- Install button
- Progress indicator
- Success/error feedback

**ViewModel**: `PluginInstallerViewModel.cs`
```csharp
public class PluginInstallerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _packageFilePath = string.Empty;
    
    [ObservableProperty]
    private PluginManifest? _manifest;
    
    [ObservableProperty]
    private bool _isInstalling;
    
    [RelayCommand]
    private async Task BrowsePackage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Plugin Package",
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "ShareX Avalonia Plugin", Extensions = { "sxap" } }
            }
        };
        
        var result = await dialog.ShowAsync(App.MainWindow);
        if (result != null && result.Length > 0)
        {
            PackageFilePath = result[0];
            await LoadManifestPreview();
        }
    }
    
    [RelayCommand]
    private async Task Install()
    {
        IsInstalling = true;
        try
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            var metadata = PluginPackager.InstallPackage(PackageFilePath, pluginsDir);
            
            if (metadata != null)
            {
                // Reload plugins
                ProviderCatalog.LoadPlugins(pluginsDir);
                
                // Show success message
                await ShowSuccessDialog($"Plugin '{metadata.Manifest.Name}' installed successfully!");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Installation failed: {ex.Message}");
        }
        finally
        {
            IsInstalling = false;
        }
    }
}
```

### C. Integration Points

**1. Settings UI** - Add "Install Plugin" button to Uploaders settings page

**Location**: `ApplicationSettingsView.axaml` (Uploaders tab)

```xml
<Button Content="Install Plugin..."
        Command="{Binding OpenPluginInstallerCommand}"
        HorizontalAlignment="Right"
        Margin="0,0,0,10"/>
```

**2. Drag & Drop Support** - Allow dragging `.sxap` files onto main window

**Location**: `MainWindow.axaml.cs`

```csharp
private async void OnDrop(object? sender, DragEventArgs e)
{
    if (e.Data.Contains(DataFormats.Files))
    {
        var files = e.Data.GetFiles();
        var sxapFiles = files?.Where(f => f.Path.LocalPath.EndsWith(".sxap", StringComparison.OrdinalIgnoreCase));
        
        if (sxapFiles?.Any() == true)
        {
            foreach (var file in sxapFiles)
            {
                await InstallPlugin(file.Path.LocalPath);
            }
        }
    }
}
```

**3. File Association** - Register `.sxap` extension with Windows

**Location**: `IntegrationHelpers.cs` (similar to ShareX's `.sxie` registration)

```csharp
private static readonly string ShellPluginExtensionPath = @"Software\Classes\.sxap";
private static readonly string ShellPluginExtensionValue = "ShareX.Avalonia.sxap";

public static void RegisterSxapExtension()
{
    // Register .sxap extension
    // Associate with ShareX.Avalonia.exe
    // Add "Install Plugin" context menu
}
```

---

## 5. Security Considerations

### Validation
1. **Manifest Validation**: Verify `plugin.json` schema before extraction
2. **Assembly Verification**: Optional code signing check
3. **Size Limit**: 100MB maximum package size (configurable)
4. **File Type Whitelist**: Only allow `.dll`, `.json`, `.txt`, `.md`, `.png`, `.jpg` files

### Sandboxing
- Plugins already run in isolated `AssemblyLoadContext`
- Consider adding permission system in future (network access, file system access)

---

## 6. Developer Workflow

### Creating a Plugin Package

**Option A: Manual (for developers)**
```bash
# 1. Create plugin directory structure
mkdir ImgurUploader
cd ImgurUploader

# 2. Add files
# - plugin.json
# - ShareX.Uploader.Imgur.dll
# - Dependencies

# 3. Create .sxap package (using 7-Zip or similar)
7z a ImgurUploader.sxap *
```

**Option B: CLI Tool (future enhancement)**
```bash
sxap-pack --input ./ImgurUploader --output ImgurUploader.sxap
```

**Option C: GUI Packager (future enhancement)**
- Similar to ShareX's `ImageEffectPackagerForm`
- Browse for plugin directory
- Auto-validate manifest
- Generate `.sxap` file

---

## 7. Implementation Phases

### Phase 1: Core Packaging (2-3 hours)
- [x] Create `PluginPackager.cs` with `Package()` and `InstallPackage()` methods
- [x] Add ZIP compression/extraction logic
- [x] Add manifest validation
- [x] Unit tests for packaging/extraction

### Phase 2: UI Integration (2-3 hours)
- [ ] Create `PluginInstallerDialog.axaml` + ViewModel
- [ ] Add "Install Plugin" button to Settings
- [ ] Add drag & drop support to MainWindow
- [ ] Success/error notifications

### Phase 3: Polish & Security (1-2 hours)
- [ ] Add file type whitelist
- [ ] Add size limit enforcement
- [ ] Add version conflict detection
- [ ] Add update/overwrite logic

### Phase 4: Developer Tools (Optional, 2-3 hours)
- [ ] CLI tool for packaging
- [ ] GUI packager form
- [ ] Plugin development guide documentation

---

## 8. File Locations

```
ShareX.Avalonia/
├── src/
│   ├── ShareX.Avalonia.Uploaders/
│   │   └── PluginSystem/
│   │       ├── PluginPackager.cs          (NEW)
│   │       ├── PluginInstaller.cs         (NEW)
│   │       └── PluginManifest.cs          (EXISTING)
│   └── ShareX.Avalonia.UI/
│       ├── Views/
│       │   └── PluginInstallerDialog.axaml (NEW)
│       └── ViewModels/
│           └── PluginInstallerViewModel.cs (NEW)
└── docs/
    └── plugin_development_guide.md        (NEW)
```

---

## 9. Example Usage

### End User Workflow
1. Download `ImgurUploader.sxap` from plugin repository
2. Open ShareX.Avalonia → Settings → Uploaders
3. Click "Install Plugin..." button
4. Select `ImgurUploader.sxap` file
5. Review plugin details
6. Click "Install"
7. Plugin appears in Uploaders list

### Alternative: Drag & Drop
1. Drag `ImgurUploader.sxap` onto ShareX.Avalonia window
2. Confirm installation prompt
3. Plugin installed automatically

---

## 10. Future Enhancements

- **Plugin Repository**: Online marketplace for plugins
- **Auto-Updates**: Check for plugin updates
- **Plugin Manager UI**: View installed plugins, uninstall, enable/disable
- **Code Signing**: Verify plugin authenticity
- **Dependency Resolution**: Auto-download required dependencies
- **Multi-Plugin Packages**: Bundle multiple related plugins

---

## 11. Comparison with ShareX

| Feature | ShareX (.sxie) | ShareX.Avalonia (.sxap) |
|---------|----------------|-------------------------|
| Format | ZIP archive | ZIP archive |
| Manifest | Config.json (ImageEffectPreset) | plugin.json (PluginManifest) |
| Assets | Images only | Any file type (validated) |
| Installation | Drag & drop to form | Drag & drop + Install dialog |
| Validation | File type check | Manifest + file type + size |
| Isolation | N/A (built-in effects) | AssemblyLoadContext |

---

## 12. Success Criteria

✅ Users can install plugins by double-clicking `.sxap` files  
✅ Users can drag & drop `.sxap` files onto app window  
✅ Invalid packages are rejected with clear error messages  
✅ Installed plugins appear in Uploaders list immediately  
✅ Plugins are isolated and don't conflict with each other  
✅ Package size is limited to prevent abuse  
✅ Documentation exists for plugin developers
