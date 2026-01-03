# .sxadp File Association Implementation Plan

**Status**: 📋 Ready for Implementation  
**Priority**: HIGH - Enables double-click plugin installation  
**Estimated Effort**: 2-3 hours

---

## 1. Analysis: ShareX .sxie Implementation

### Registry Structure
ShareX registers `.sxie` files with the following registry keys:

```
HKEY_CURRENT_USER\Software\Classes\.sxie
  (Default) = "ShareX.sxie"

HKEY_CURRENT_USER\Software\Classes\ShareX.sxie
  (Default) = "ShareX image effect"
  
HKEY_CURRENT_USER\Software\Classes\ShareX.sxie\DefaultIcon
  (Default) = "C:\Path\To\ShareX_File_Icon.ico"
  
HKEY_CURRENT_USER\Software\Classes\ShareX.sxie\shell\open\command
  (Default) = "C:\Path\To\ShareX.exe" -ImageEffect "%1"
```

### Command-Line Flow
1. User double-clicks `MyEffect.sxie`
2. Windows executes: `ShareX.exe -ImageEffect "C:\Path\To\MyEffect.sxie"`
3. `ShareXCLIManager.CheckImageEffect()` detects `-ImageEffect` command
4. Calls `TaskHelpers.ImportImageEffect(filePath)`
5. Extracts package and imports into ImageEffects UI

---

## 2. ShareX.Avalonia Implementation

### A. IntegrationHelpers (NEW FILE)

**Location**: `src/ShareX.Avalonia.Core/Integration/IntegrationHelpers.cs`

```csharp
using Microsoft.Win32;
using ShareX.Avalonia.Common;
using System.Runtime.InteropServices;

namespace ShareX.Avalonia.Core.Integration;

/// <summary>
/// Handles Windows integration (file associations, registry)
/// </summary>
public static class IntegrationHelpers
{
    private static readonly string ApplicationPath = $"\"{Environment.ProcessPath}\"";
    private static readonly string FileIconPath = $"\"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ShareX_File_Icon.ico")}\"";
    
    // .sxadp (ShareX Avalonia Destination Plugin) file association
    private static readonly string ShellPluginExtensionPath = @"Software\Classes\.sxadp";
    private static readonly string ShellPluginExtensionValue = "ShareX.Avalonia.sxadp";
    private static readonly string ShellPluginAssociatePath = $@"Software\Classes\{ShellPluginExtensionValue}";
    private static readonly string ShellPluginAssociateValue = "ShareX Avalonia plugin";
    private static readonly string ShellPluginIconPath = $@"{ShellPluginAssociatePath}\DefaultIcon";
    private static readonly string ShellPluginIconValue = FileIconPath;
    private static readonly string ShellPluginCommandPath = $@"{ShellPluginAssociatePath}\shell\open\command";
    private static readonly string ShellPluginCommandValue = $"{ApplicationPath} -InstallPlugin \"%1\"";
    
    /// <summary>
    /// Check if .sxadp file association is registered
    /// </summary>
    public static bool CheckPluginExtension()
    {
        try
        {
            return RegistryHelpers.CheckStringValue(ShellPluginExtensionPath, null, ShellPluginExtensionValue) &&
                   RegistryHelpers.CheckStringValue(ShellPluginCommandPath, null, ShellPluginCommandValue);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
        
        return false;
    }
    
    /// <summary>
    /// Register or unregister .sxadp file association
    /// </summary>
    public static void CreatePluginExtension(bool create)
    {
        try
        {
            if (create)
            {
                UnregisterPluginExtension();
                RegisterPluginExtension();
            }
            else
            {
                UnregisterPluginExtension();
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }
    
    private static void RegisterPluginExtension()
    {
        RegistryHelpers.CreateRegistry(ShellPluginExtensionPath, ShellPluginExtensionValue);
        RegistryHelpers.CreateRegistry(ShellPluginAssociatePath, ShellPluginAssociateValue);
        RegistryHelpers.CreateRegistry(ShellPluginIconPath, ShellPluginIconValue);
        RegistryHelpers.CreateRegistry(ShellPluginCommandPath, ShellPluginCommandValue);
        
        // Notify Windows shell of file association change
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        
        DebugHelper.WriteLine("Registered .sxadp file association");
    }
    
    private static void UnregisterPluginExtension()
    {
        RegistryHelpers.RemoveRegistry(ShellPluginExtensionPath);
        RegistryHelpers.RemoveRegistry(ShellPluginAssociatePath);
        
        DebugHelper.WriteLine("Unregistered .sxadp file association");
    }
    
    // P/Invoke for shell notification
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
```

### B. RegistryHelpers (NEW FILE)

**Location**: `src/ShareX.Avalonia.Core/Integration/RegistryHelpers.cs`

```csharp
using Microsoft.Win32;
using ShareX.Avalonia.Common;

namespace ShareX.Avalonia.Core.Integration;

/// <summary>
/// Helper methods for Windows Registry operations
/// </summary>
public static class RegistryHelpers
{
    public static void CreateRegistry(string path, string value)
    {
        CreateRegistry(path, null, value);
    }
    
    public static void CreateRegistry(string path, string name, string value)
    {
        try
        {
            using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(path))
            {
                if (rk != null)
                {
                    rk.SetValue(name ?? string.Empty, value, RegistryValueKind.String);
                }
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }
    
    public static void RemoveRegistry(string path)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }
    
    public static bool CheckStringValue(string path, string name, string value)
    {
        try
        {
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(path))
            {
                if (rk != null)
                {
                    string registryValue = rk.GetValue(name ?? string.Empty) as string;
                    return registryValue != null && registryValue.Equals(value, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
        
        return false;
    }
}
```

### C. CLI Manager Integration

**Location**: `src/ShareX.Avalonia.App/Program.cs`

Add command-line argument handling:

```csharp
public static async Task Main(string[] args)
{
    // Handle command-line arguments before starting UI
    if (args.Length > 0)
    {
        if (await HandleCommandLineArgs(args))
        {
            // Command handled, exit
            return;
        }
    }
    
    // Continue with normal app startup
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}

private static async Task<bool> HandleCommandLineArgs(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        
        // Handle -InstallPlugin command
        if (arg.Equals("-InstallPlugin", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            string packagePath = args[i + 1];
            
            if (File.Exists(packagePath) && packagePath.EndsWith(".sxadp", StringComparison.OrdinalIgnoreCase))
            {
                await InstallPluginFromCommandLine(packagePath);
                return true; // Command handled
            }
        }
    }
    
    return false; // No command handled, continue normal startup
}

private static async Task InstallPluginFromCommandLine(string packagePath)
{
    try
    {
        DebugHelper.WriteLine($"Installing plugin from command line: {packagePath}");
        
        // Start the app with the installer dialog
        var app = BuildAvaloniaApp();
        
        app.AfterSetup(async (appBuilder) =>
        {
            // Show installer dialog
            var dialog = new PluginInstallerDialog();
            
            // Pre-load the package
            if (dialog.DataContext is PluginInstallerViewModel vm)
            {
                vm.PackageFilePath = packagePath;
                await vm.LoadManifestPreview();
            }
            
            await dialog.ShowDialog(App.MainWindow);
        });
        
        app.StartWithClassicDesktopLifetime(Array.Empty<string>());
    }
    catch (Exception ex)
    {
        DebugHelper.WriteException(ex);
        
        // Show error message box
        MessageBox.Show($"Failed to install plugin: {ex.Message}", "ShareX Avalonia", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

### D. Settings UI Integration

**Location**: `src/ShareX.Avalonia.UI/Views/ApplicationSettingsView.axaml`

Add checkbox to Integration/Advanced settings tab:

```xml
<CheckBox Content="Associate .sxadp files with ShareX.Avalonia"
          IsChecked="{Binding IsPluginExtensionRegistered}"
          Command="{Binding TogglePluginExtensionCommand}"
          ToolTip.Tip="Allows double-clicking .sxadp files to install plugins"/>
```

**ViewModel**: `ApplicationSettingsViewModel.cs`

```csharp
private readonly IIntegrationService _integrationService;

public ApplicationSettingsViewModel(IIntegrationService integrationService)
{
    _integrationService = integrationService;
    LoadIntegrationSettings();
}

[ObservableProperty]
private bool _isPluginExtensionRegistered;

[ObservableProperty]
private bool _supportsFileAssociations;

partial void OnIsPluginExtensionRegisteredChanged(bool value)
{
    _integrationService.SetPluginExtensionRegistration(value);
}

private void LoadIntegrationSettings()
{
    SupportsFileAssociations = _integrationService.SupportsFileAssociations();
    IsPluginExtensionRegistered = _integrationService.IsPluginExtensionRegistered();
}
```

**XAML Update**: Show/hide checkbox based on platform support

```xml
<CheckBox Content="Associate .sxadp files with ShareX.Avalonia"
          IsChecked="{Binding IsPluginExtensionRegistered}"
          IsVisible="{Binding SupportsFileAssociations}"
          ToolTip.Tip="Allows double-clicking .sxadp files to install plugins"/>
```

---

## 3. User Experience Flow

### Scenario 1: First-Time Setup
1. User opens Settings → Integration
2. Checks "Associate .sxadp files with ShareX.Avalonia"
3. Registry keys are created
4. `.sxadp` files now show ShareX icon in Explorer

### Scenario 2: Installing Plugin
1. User downloads `ImgurUploader.sxadp`
2. Double-clicks the file
3. ShareX.Avalonia launches with `-InstallPlugin "C:\...\ImgurUploader.sxadp"`
4. `PluginInstallerDialog` opens with package pre-loaded
5. User reviews metadata and clicks "Install"
6. Plugin is extracted to `Plugins/imgur/`
7. Success message shown

### Scenario 3: Uninstall Association
1. User unchecks "Associate .sxadp files"
2. Registry keys are removed
3. `.sxadp` files revert to default ZIP icon

---

## 4. Security Considerations

### Validation Before Installation
- Package size limit (100MB)
- Manifest validation
- File type whitelist
- User confirmation required

### Registry Permissions
- Uses `HKEY_CURRENT_USER` (no admin required)
- Only affects current user
- Can be easily reverted

---

## 5. Implementation Checklist

### Phase 1: Core Infrastructure (1 hour)
- [ ] Create `IntegrationHelpers.cs` with registry methods
- [ ] Create `RegistryHelpers.cs` utility class
- [ ] Add P/Invoke for `SHChangeNotify`
- [ ] Test registry read/write operations

### Phase 2: CLI Integration (1 hour)
- [ ] Add `-InstallPlugin` argument handling to `Program.cs`
- [ ] Implement `InstallPluginFromCommandLine()` method
- [ ] Test command-line invocation manually
- [ ] Verify dialog opens with pre-loaded package

### Phase 3: UI Integration (30 min)
- [ ] Add checkbox to ApplicationSettingsView
- [ ] Implement ViewModel binding
- [ ] Test checkbox toggle
- [ ] Verify registry changes on toggle

### Phase 4: Testing (30 min)
- [ ] Create test `.sxadp` package
- [ ] Register file association
- [ ] Double-click test package
- [ ] Verify installer dialog opens
- [ ] Install plugin successfully
- [ ] Unregister file association
- [ ] Verify registry cleanup

---

## 6. Testing Steps

### Manual Test

1. **Build and run app**
   ```bash
   dotnet build
   dotnet run --project src/ShareX.Avalonia.App
   ```

2. **Register file association**
   - Open Settings → Integration
   - Check "Associate .sxadp files"
   - Verify no errors in Debug output

3. **Verify registry keys**
   ```powershell
   reg query "HKCU\Software\Classes\.sxadp"
   reg query "HKCU\Software\Classes\ShareX.Avalonia.sxadp\shell\open\command"
   ```

4. **Create test package**
   ```powershell
   Compress-Archive -Path "Plugins\imgur" -DestinationPath "test.sxadp"
   ```

5. **Test double-click**
   - Double-click `test.sxadp` in Explorer
   - Verify ShareX.Avalonia launches
   - Verify PluginInstallerDialog opens
   - Verify package metadata displays
   - Click "Install"
   - Verify plugin installs successfully

6. **Test unregister**
   - Uncheck "Associate .sxadp files"
   - Verify registry keys removed
   - Double-click `.sxadp` file
   - Verify Windows prompts for app selection

---

## 7. Platform Support

### Windows
✅ Full support via Windows Registry

### macOS
⚠️ Requires different approach:
- Use `Info.plist` for file associations
- Register UTI (Uniform Type Identifier)
- Handle `application:openFile:` delegate

### Linux
⚠️ Requires different approach:
- Use `.desktop` file
- Register MIME type
- Use `xdg-mime` for associations

**Recommendation**: Implement Windows first, add macOS/Linux support in future iterations.

---

## 8. Alternative: Always Handle .sxadp

Instead of requiring user to enable file association, automatically handle `.sxadp` files passed as arguments:

```csharp
// In Program.cs Main()
if (args.Length > 0 && args[0].EndsWith(".sxadp"))
{
    await InstallPluginFromCommandLine(args[0]);
    return;
}
```

Then user can:
- Right-click `.sxadp` → Open With → ShareX.Avalonia
- Or manually register association via Windows Settings

---

## 9. Future Enhancements

- [ ] **Auto-update plugins**: Check for updates on startup
- [ ] **Plugin repository**: Browse and install from online catalog
- [ ] **Drag & drop**: Drop `.sxadp` onto main window
- [ ] **Batch install**: Install multiple plugins at once
- [ ] **Uninstall UI**: Manage installed plugins
- [ ] **macOS/Linux support**: Platform-specific file associations

---

## 10. Success Criteria

✅ User can check "Associate .sxadp files" in Settings  
✅ Double-clicking `.sxadp` file launches ShareX.Avalonia  
✅ PluginInstallerDialog opens with package pre-loaded  
✅ Plugin installs successfully  
✅ Registry keys are created/removed correctly  
✅ No admin privileges required  
✅ Works on Windows 10/11
