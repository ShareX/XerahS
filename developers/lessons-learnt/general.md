# XerahS Lessons Learnt

This document serves as a centralized knowledge base for technical challenges, architectural decisions, and platform-specific quirks encountered during the development of XerahS.

## table of Contents

1.  [UI & FluentAvalonia](#ui--fluentavalonia)
2.  [Build & Configuration](#build--configuration)
3.  [Plugin System](#plugin-system)
4.  [Android / Avalonia](#android--avalonia)

---

## UI & FluentAvalonia

### ContextMenu vs. ContextFlyout

**Issue**: Standard `ContextMenu` controls do not render correctly with `FluentAvaloniaTheme`. They utilize legacy Popup windows which are not fully styled by the theme and may appear unstyled or invisible.

**Solution**: Always use `ContextFlyout` with `MenuFlyout` instead of `ContextMenu`.

**❌ Incorrect**:
```xml
<!-- Standard ContextMenu (may be invisible) -->
<Border.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Action" Command="{Binding MyCommand}"/>
    </ContextMenu>
</Border.ContextMenu>
```

**✅ Correct**:
```xml
<!-- Use ContextFlyout with MenuFlyout -->
<Border.ContextFlyout>
    <MenuFlyout>
        <MenuItem Header="Action" Command="{Binding MyCommand}"/>
    </MenuFlyout>
</Border.ContextFlyout>
```

### Binding in DataTemplates with Flyouts

**Issue**: When using `ContextFlyout` or `ContextMenu` inside a `DataTemplate`, bindings to the parent logic (ViewModel) fail because Popups/Flyouts exist in a separate visual tree, detached from the `DataTemplate`'s hierarchy.

**Solution**: Use the `$parent[UserControl]` reflection binding syntax to reach the main view's DataContext.

```xml
<DataTemplate x:DataType="local:MyItem">
    <Border>
        <Border.ContextFlyout>
            <MenuFlyout>
                <!-- Bind to parent UserControl's DataContext -->
                <MenuItem Header="Edit" 
                          Command="{Binding $parent[UserControl].DataContext.EditCommand}"
                          CommandParameter="{Binding}"/>
            </MenuFlyout>
        </Border.ContextFlyout>
    </Border>
</DataTemplate>
```

**Key Points**:
- Use `$parent[UserControl].DataContext` to access the View's ViewModel from within a flyout.
- `CommandParameter="{Binding}"` passes the current data item (the DataTemplate's DataContext).
- For shared flyouts, define them in `UserControl.Resources` and reference via `{StaticResource}`.

### WebView Helper

**Context**: Rendering HTML content within the application (e.g., for Indexer previews).

**Issue**: The standard `WebView.Avalonia` package is insufficient on its own for desktop applications. It provides the controls but may lack the necessary desktop-specific native bindings or initialization logic required for Windows/Linux/macOS.

**Solution**: You must reference **`WebView.Avalonia.Desktop`** in addition to the base package.

**❌ Incorrect**:
```xml
<PackageReference Include="WebView.Avalonia" Version="11.0.0.1" />
```

**✅ Correct**:
```xml
<PackageReference Include="WebView.Avalonia" Version="11.0.0.1" />
<PackageReference Include="WebView.Avalonia.Desktop" Version="11.0.0.1" />
```

Without the `.Desktop` package, the `WebView` control may fail to initialize or render, often silently or with generic "type not found" errors when using reflection to locate it.


---

## Build & Configuration

### Windows TFM & CsWinRT Behavior (Net10.0-windows)

**Context**: When implementing modern Windows features using `Microsoft.Windows.CsWinRT` in a project targeting .NET 8/9/10.

**Issue**: Using the generic `net10.0-windows` TFM combined with a separate `<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>` property works for **individual** project builds but fails during **full solution** builds with "Windows Metadata not provided" errors. This is due to a transitive dependency resolution issue in the CsWinRT targets file.

**Solution**: Use the **explicit TFM** string which combines the framework and the platform version.

**❌ Incorrect configuration for solution builds**:
```xml
<TargetFramework>net10.0-windows</TargetFramework>
<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>
```

**✅ Correct configuration**:
```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

This forces the build system to include the correct Windows SDK reference assemblies natively, avoiding the metadata resolution failure. This is required for reliable solution-wide builds when using WinRT APIs like `Windows.Graphics.Capture`.

---

## Plugin System

### Pure Dynamic Loading

**Context**: Implementing a plugin architecture where extensions are loaded at runtime without compile-time references.

**Lessons Learned**:
1.  **Don't mix paradigms**: Attempts to mix static compilation (direct project references) with dynamic loading (`AssemblyLoadContext`) cause type identity conflicts. Types loaded via ALC are distinct from the "same" types loaded via normal reference, even if the DLL is identical.
2.  **Keep contexts alive**: The `PluginLoader` must maintain a static reference to the created `AssemblyLoadContexts`. If these are garbage collected, the plugin assemblies will be unloaded, causing crashes or missing functionality.
3.  **Share framework dependencies**: Plugins must not ship with their own copies of framework assemblies (e.g., `Avalonia.dll`, `CommunityToolkit.Mvvm`). The `PluginLoadContext` must be configured to return `null` for these shared assemblies, forcing the runtime to resolve them from the Host application's context. This ensures that `Plugin.Button` is compatible with `Host.Button`.
4.  **Templating limitations**: In Avalonia, overriding `ControlTemplate` in a plugin requires careful Command wiring, as standard resource lookup chains may specific to the load context.
5.  **Plugin TFM must match Host TFM**: Plugin projects must use the **exact same Target Framework Moniker (TFM)** as the host application. If the host targets `net10.0-windows10.0.19041.0` on Windows, plugins must also use conditional TFM matching:

    ```xml
    <!-- Plugins must match host TFM exactly -->
    <TargetFramework Condition="'$(OS)' == 'Windows_NT'">net10.0-windows10.0.19041.0</TargetFramework>
    <TargetFramework Condition="'$(OS)' != 'Windows_NT'">net10.0</TargetFramework>
    ```

    **Why**: Plugin build targets that copy outputs to the host's bin folder (e.g., `$(TargetFramework)\Plugins\`) will use the plugin's TFM in the path. If the plugin targets `net10.0` but the host outputs to `net10.0-windows10.0.19041.0`, plugins end up in the wrong folder and fail to load at runtime. This causes provider settings UI to not appear.

---

## Android / Avalonia

### Avalonia Android: App Stuck at "Initializing..." or Blank Screen

**Context**: XerahS.Mobile.Ava (Avalonia UI on Android) showed a perpetual loading screen or blank screen even though initialization and navigation logic ran correctly.

**Root cause**: In `MainActivity.OnCreate`, code was setting `parent.Content = null` where `parent` was the host `ContentControl` that contains Avalonia's `MainView`. That removed the entire Avalonia UI from the visual tree, so nothing (loading view or main view) was visible.

**Lesson**: Do **not** clear the content of the control that hosts `ISingleViewApplicationLifetime.MainView`. If the app seems stuck on loading or blank but logs show init and navigation completing, look for platform code (e.g. in the Activity) that modifies the host's `Content`.

**MAUI**: MAUI has no equivalent host-Content bug. For MAUI white screen / loading not visible, defer starting `InitializeCoreAsync` by ~150 ms in `MainActivity.OnCreate` so the loading page can render before background init runs. See [android_avalonia_init_fix.md](android_avalonia_init_fix.md#maui-equivalent-no-host-content-bug).
