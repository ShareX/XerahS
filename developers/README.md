# XerahS Developer Guide

## 🤖 AI-First Development Philosophy

**XerahS is engineered for the agentic coding era.** This project embraces bleeding-edge technologies (.NET 10, Avalonia 11.3+, SkiaSharp 2.88.9) and is architected with AI-assisted development as a first-class concern. We actively seek developers proficient in **agentic coding workflows**—leveraging AI agents like GitHub Copilot, Claude, and Codex to accelerate feature development, refactoring, and code quality. Our codebase prioritizes clarity, consistency, and comprehensive documentation to maximize AI comprehension and generation capabilities. Strict nullability, exhaustive inline documentation, and standardized patterns (MVVM, dependency injection, plugin architecture) ensure that both human developers and AI agents can navigate, understand, and extend the system efficiently. **As AI capabilities rapidly advance, agentic coding is the future—like it or not** and XerahS is built to evolve alongside these tools. If you're experienced with AI-powered development tools and ready to push the boundaries of cross-platform screenshot tooling, XerahS is your platform.

**Community-Driven Development:** XerahS is built collaboratively by developers and contributors from around the world, united by a shared commitment to open-source innovation. This is a project created by the community, for the community—where every contribution, whether code, documentation, or feedback, helps shape a tool that serves users across all platforms.

## Getting Started for Developers

### Cloning with Submodules
XerahS depends on [ShareX/ImageEditor](https://github.com/ShareX/ImageEditor), which is included as a Git submodule. To clone the repository with all dependencies:

```bash
git clone --recursive https://github.com/ShareX/XerahS.git
```

If you've already cloned the repository without the `--recursive` flag:
```bash
cd XerahS
git submodule update --init --recursive
```

### Building the Project
After cloning with submodules:
```bash
cd XerahS
dotnet restore
dotnet build
```

### Updating Submodules
To pull the latest changes for the ImageEditor submodule:
```bash
git submodule update --remote --merge
```

## Target Framework Reference

All projects standardize on Windows SDK **10.0.26100.0** (Windows 11 24H2). Plugins use plain `net10.0` for cross-platform compatibility.

| Project | Target Framework(s) | Notes |
|---------|---------------------|-------|
| **Desktop App** | | |
| XerahS.App | `net10.0-windows10.0.26100.0` / `net10.0` | Entry point; Windows TFM on Windows, plain on others |
| XerahS.UI | `net10.0-windows10.0.26100.0` / `net10.0` | Avalonia UI layer |
| XerahS.Bootstrap | `net10.0-windows10.0.26100.0` / `net10.0` | DI bootstrap |
| XerahS.CLI | `net10.0-windows10.0.26100.0` / `net10.0` | CLI tool |
| **Core Libraries** | | |
| XerahS.Core | `net10.0` + `net10.0-windows10.0.26100.0` | Multi-target; Windows variant uses CsWin32 |
| XerahS.RegionCapture | `net10.0` + `net10.0-windows10.0.26100.0` | Multi-target; CsWin32 for DWM/D3D11 |
| XerahS.Common | `net10.0` | Cross-platform |
| XerahS.Uploaders | `net10.0` | Cross-platform |
| XerahS.Media | `net10.0` | Cross-platform |
| XerahS.History | `net10.0` | Cross-platform |
| XerahS.Indexer | `net10.0` | Cross-platform |
| XerahS.ViewModels | `net10.0` | Cross-platform |
| XerahS.Services | `net10.0` | Cross-platform |
| XerahS.Services.Abstractions | `net10.0` | Cross-platform |
| **Platform** | | |
| XerahS.Platform.Abstractions | `net10.0` | Interfaces only |
| XerahS.Platform.Windows | `net10.0-windows10.0.26100.0` | D3D11, CsWinRT, native APIs |
| XerahS.Platform.MacOS | `net10.0` | SharpHook for hotkeys |
| XerahS.Platform.Linux | `net10.0` | DBus integration |
| XerahS.Platform.Mobile | `net10.0` | Shared mobile abstractions |
| **Mobile** | | |
| XerahS.Mobile.Maui | `net10.0-android` / `net10.0-ios` | MAUI entry point |
| XerahS.Mobile.Android | `net10.0-android` | Android platform services |
| XerahS.Mobile.iOS | `net10.0-ios` | iOS platform services |
| XerahS.Mobile.iOS.ShareExtension | `net10.0-ios` | iOS Share Extension |
| XerahS.Mobile.UI | `net10.0` | Cross-platform mobile UI |
| **Plugins** (all `net10.0`) | | Dynamic loading; `ExcludeAssets=runtime` for shared deps |
| XerahS.Auto.Plugin | `net10.0` | |
| XerahS.Imgur.Plugin | `net10.0` | |
| XerahS.AmazonS3.Plugin | `net10.0` | |
| XerahS.Paste2.Plugin | `net10.0` | |
| XerahS.GitHubGist.Plugin | `net10.0` | |
| **Tools** | | |
| XerahS.PluginExporter | `net10.0` | Plugin packager |
| XerahS.Audits.Tool | `net10.0` | Dev tool |
| XerahS.Tests | `net10.0-windows10.0.26100.0` | NUnit tests |
| **Submodule (ImageEditor)** | | |
| ShareX.ImageEditor | `net9.0` / `net9.0-windows10.0.26100.0` / `net10.0` / `net10.0-windows10.0.26100.0` | Multi-target for ShareX compat; only net10.0 variants build from XerahS |
| ShareX.ImageEditor.Loader | `net10.0-windows10.0.26100.0` | Standalone demo app |

## Architecture Overview

This project follows the **MVVM (Model-View-ViewModel)** pattern using the `CommunityToolkit.Mvvm` library.

### Key Projects
*   **XerahS.App**: Main application entry point
*   **XerahS.UI**: UI layer using Avalonia. Contains Views, ViewModels, and shared UI resources
*   **XerahS.Core**: Core application logic, task management (`WorkerTask`), and business models
*   **XerahS.Annotations**: Annotation system with 16+ annotation types and serialization support
*   **XerahS.ImageEffects**: 50+ image effects (filters, adjustments, manipulations) using SkiaSharp
*   **XerahS.Platform.***: Platform-specific implementations (e.g., `WindowsScreenCaptureService`, `MacOSScreenshotService`, macOS SharpHook hotkeys)
*   **XerahS.ScreenCapture**: Screen capture logic and region selection
*   **XerahS.Uploaders**: Upload providers (Imgur, Amazon S3, etc.)
*   **XerahS.History**: Capture history management
*   **XerahS.Mobile.***: Mobile implementation using .NET MAUI (Android/iOS)
    *   `XerahS.Mobile.Maui`: Main MAUI application (Android/iOS targets)
    *   `XerahS.Mobile.UI`: Shared mobile UI components and view models
    *   `XerahS.Mobile.Android`: Android-specific platform services
    *   `XerahS.Mobile.iOS`: iOS-specific platform services
    *   `XerahS.Mobile.iOS.ShareExtension`: iOS Share Extension for receiving shared content

### Services & Dependency Injection
Services are initialized in `Program.cs` and `App.axaml.cs`. We use a Service Locator pattern via `PlatformServices` static class for easy access in ViewModels (though Constructor Injection is preferred where possible).

### UI Theme & FluentAvalonia

This project uses **FluentAvaloniaUI** (v2.4.1) which provides a modern Fluent Design System for Avalonia applications.

#### ⚠️ Important: ContextMenu vs ContextFlyout

**Issue**: Standard `ContextMenu` controls may not render correctly with FluentAvaloniaTheme. They use legacy Popup windows which are not fully styled by the theme.

**Solution**: Use `ContextFlyout` with `MenuFlyout` instead:

```xml
<!-- ❌ DON'T: Standard ContextMenu (may be invisible) -->
<Border.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Action" Command="{Binding MyCommand}"/>
    </ContextMenu>
</Border.ContextMenu>

<!-- ✅ DO: Use ContextFlyout with MenuFlyout -->
<Border.ContextFlyout>
    <MenuFlyout>
        <MenuItem Header="Action" Command="{Binding MyCommand}"/>
    </MenuFlyout>
</Border.ContextFlyout>
```

#### Binding in DataTemplates with Flyouts/Popups

When using `ContextFlyout` or `ContextMenu` inside a `DataTemplate`, bindings to the parent ViewModel require special syntax because popups/flyouts exist outside the normal visual tree:

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
- Use `$parent[UserControl].DataContext` to access the View's ViewModel from within a flyout
- `CommandParameter="{Binding}"` passes the current item (DataTemplate's DataContext)
- Avalonia 11.x properly resolves `$parent` in flyouts/overlays
- For shared flyouts, define them in `UserControl.Resources` and reference via `{StaticResource}`

### Annotation System
The annotation system is built on a polymorphic model architecture with UI integration via `EditorView`:
*   **Models**: Located in `XerahS.Annotations/Models/`, inheriting from base `Annotation` class
*   **Types**: 17 annotation types including Rectangle, Ellipse, Arrow, Line, Text, Number, Blur, Pixelate, Magnify, Highlight, Freehand, SpeechBalloon, Image, Spotlight, SmartEraser, Crop, plus BaseEffectAnnotation
*   **Drawing**: Handled in `EditorView.axaml.cs` for performance and direct pointer manipulation
*   **State**: `MainViewModel` manages tool state (`ActiveTool`, `SelectedColor`, `StrokeWidth`, etc.)
*   **Undo/Redo**: Implemented using `Stack<Control>` to manage visual elements on canvas
*   **Serialization**: JSON-based using `System.Text.Json` with `[JsonDerivedType]` attributes for polymorphism
*   **Keyboard Shortcuts**: V(Select), R(Rectangle), E(Ellipse), A(Arrow), L(Line), P(Pen), H(Highlighter), T(Text), B(Balloon), N(Number), C(Crop), M(Magnify), S(Spotlight), F(Effects Panel)

### Image Effects System
*   **Auto-Discovery**: Effects are discovered via reflection from `XerahS.ImageEffects` assembly
*   **Effect Count**: 40+ effects (13 Adjustments, 17 Filters, 10 Manipulations, 6 Drawings)
*   **Categories**: Filters, Adjustments, Manipulations, Drawings
*   **Parameter Binding**: Dynamic UI generation for effect parameters
*   **Integration**: `EffectsPanelView` provides UI, `MainViewModel` handles application logic

### Uploader Plugin System

The uploader system uses a plugin architecture where each uploader (Imgur, Amazon S3, etc.) is a separate plugin with its own configuration UI.

#### ⚠️ Important: Plugin Configuration Loading

**Issue**: Plugin configuration UIs may not load their settings from `UploadersConfig` on initialization, causing saved settings to appear blank when reopening the settings view.

**Root Cause**: Plugins are dynamically loaded and their views are created fresh each time. If the plugin's ViewModel doesn't explicitly load configuration in its constructor or `OnNavigatedTo`, settings won't populate.

**Solution Pattern**:

```csharp
// In Plugin ViewModel (e.g., ImgurConfigViewModel.cs)
public class ImgurConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    public ImgurConfigViewModel()
    {
        // ✅ CRITICAL: Load config on construction
        LoadConfiguration();
    }
    
    private void LoadConfiguration()
    {
        var config = SettingManager.UploadersConfig;
        
        // Load plugin-specific settings
        ClientId = config.ImgurClientID ?? "";
        ClientSecret = config.ImgurClientSecret ?? "";
        RefreshToken = config.ImgurRefreshToken ?? "";
        // ... load other settings
    }
    
    // When settings change, notify host to save
    partial void OnClientIdChanged(string value)
    {
        SettingManager.UploadersConfig.ImgurClientID = value;
        RequestConfigSave?.Invoke(); // Trigger save event
    }
}
```

**Event-Driven Save Pattern**:

Plugins should notify the host application when configuration changes:

```csharp
// In Plugin ViewModel
public event Action? RequestConfigSave;

// In Host (DestinationSettingsViewModel.cs)
private void OnPluginViewChanged(object? sender, EventArgs e)
{
    if (SelectedPlugin?.ViewModel is ViewModelBase vm)
    {
        // Subscribe to save requests
        if (vm is IConfigurablePlugin configurable)
        {
            configurable.RequestConfigSave += () => 
            {
                SettingManager.SaveUploadersConfig();
            };
        }
    }
}
```

**Best Practices**:
- **Always load config in constructor**: Don't rely on external initialization
- **Save on property change**: Use `OnPropertyChanged` partial methods to trigger saves
- **Implement safety net**: Save config when view is unloaded (`OnNavigatedFrom` or `Unloaded` event)
- **Test persistence**: Restart app and verify settings are retained

### Region Capture
Located in `Views/RegionCapture/`:
*   `RegionCaptureWindow`: Spans **all monitors** (Virtual Screen) with crosshair cursor
*   Multi-monitor DPI handling
*   Uses `System.Drawing.Graphics.CopyFromScreen` (GDI+) for pixel capture on Windows
*   macOS MVP capture uses `screencapture` via `MacOSScreenshotService`

## Plugin System

### ⚠️ Critical: Plugin Config Views Not Loading

**Problem**: Plugin configuration UI doesn't appear in Destination Settings even though the plugin is loaded.

**Root Cause**: Duplicate framework DLLs in the plugin output folder. When plugins include Avalonia, CommunityToolkit.Mvvm, or Newtonsoft.Json assemblies, the dynamic loading system may fail to initialize config views due to type identity mismatches.

**Solution**: **Always use `ExcludeAssets=runtime`** on NuGet package references for shared dependencies:

```xml
<!-- ✅ CORRECT: Shared framework dependencies -->
<ItemGroup>
  <PackageReference Include="Avalonia" Version="11.2.2">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.2">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.0">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
  <PackageReference Include="Newtonsoft.Json" Version="13.0.4">
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
</ItemGroup>
```

**Verification**: A properly configured plugin folder should contain **4-5 files**:
```
Plugins/myplugin/
├── ShareX.MyPlugin.Plugin.dll
├── XerahS.Platform.Abstractions.dll (if needed)
├── plugin.json
├── ShareX.MyPlugin.Plugin.runtimeconfig.json
└── runtimes/ (platform-specific natives only)
```

**Bad Sign**: If you see 20+ files including `Avalonia.*.dll`, `CommunityToolkit.Mvvm.dll`, etc., the config view will **not load**.

**Debugging**:
1. Check plugin folder file count - should be ~4 files, not 20+
2. Enable debug logging in `UploaderInstanceViewModel.InitializeConfigViewModel()`
3. Look for `ConfigView created: null` or type loading errors

See also: [Plugin Development Guide](plugins/guide.md) for complete plugin setup instructions.

## Contribution
1.  Review project documentation and existing code patterns
2.  Ensure code compiles with `dotnet build` (0 errors)
3.  Follow MVVM separation: UI logic in Views, business logic in ViewModels
4.  Add XML documentation for public APIs
5.  Test on multiple platforms when possible

## Building and Running

### Prerequisites
Ensure you have the following installed:
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- ImageEditor submodule (initialized as described above)

### Clone Repositories
```bash
# Clone the main repository with submodules
git clone --recursive https://github.com/ShareX/XerahS.git
```

### macOS Native Library (ScreenCaptureKit)
On macOS, build the native ScreenCaptureKit bridge library before building the .NET solution:
```bash
cd native/macos
make
ls -l libscreencapturekit_bridge.dylib
```

### Build the Solution
```bash
cd XerahS
dotnet build XerahS.sln
```

### Run the Application
```bash
# From the XerahS directory
dotnet run --project src/XerahS.App/XerahS.App.csproj
```

## Testing
```bash
dotnet test
```

## Mobile Development

XerahS includes an experimental **.NET MAUI** mobile implementation for Android and iOS. This extends ShareX's upload capabilities to mobile devices.

### Mobile Architecture

The mobile implementation follows a layered architecture:

```
┌─────────────────────────────────────┐
│  XerahS.Mobile.Maui (Entry Point)   │
│  - App.xaml / AppShell.xaml         │
│  - Platform-specific initialization │
├─────────────────────────────────────┤
│  XerahS.Mobile.UI                   │
│  - Shared Views (MobileUploadPage)  │
│  - ViewModels (MobileUploadVM)      │
├─────────────────────────────────────┤
│  XerahS.Mobile.Android / iOS        │
│  - Platform services                │
│  - Native file pickers              │
├─────────────────────────────────────┤
│  XerahS.Core / Common / Uploaders   │
│  - Shared business logic            │
└─────────────────────────────────────┘
```

### Building Mobile

#### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Android**: Android SDK API 21+ (included with .NET MAUI workload)
- **iOS**: Xcode 16+ (macOS required), iOS 15.0+ deployment target

#### Install .NET MAUI Workload
```bash
dotnet workload install maui
```

#### Build Android
```bash
dotnet build src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-android
```

#### Run Android (emulator or device)
```bash
dotnet run --project src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-android
```

#### Build iOS (macOS only)
```bash
dotnet build src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-ios
```

#### Run iOS Simulator (macOS only)
```bash
dotnet run --project src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-ios
```

### Mobile Features

| Feature | Android | iOS | Notes |
|---------|---------|-----|-------|
| File Upload | ✅ | ✅ | Images, videos, documents |
| Share Extension | ❌ | ✅ | iOS Share Sheet integration |
| Amazon S3 | ✅ | ✅ | Full S3 uploader support |
| Custom Uploaders | ✅ | ✅ | HTTP-based uploaders |
| Imgur | ✅ | ✅ | Image hosting |
| Settings | ✅ | ✅ | Mobile-optimized UI |

### iOS Share Extension

The iOS Share Extension (`XerahS.Mobile.iOS.ShareExtension`) allows users to share content from other apps directly to XerahS:

1. Built as an app extension embedded in the main iOS app
2. Receives shared images, videos, URLs, and text
3. Hands off to the main app for upload processing

**Build Configuration:**
The Share Extension is referenced in the main MAUI project with `IsAppExtension=true`:
```xml
<ProjectReference Include="..\XerahS.Mobile.iOS.ShareExtension\XerahS.Mobile.iOS.ShareExtension.csproj">
  <IsAppExtension>true</IsAppExtension>
</ProjectReference>
```

### Code Sharing

Mobile projects share code with desktop via:
- **XerahS.Core**: Business logic, uploaders, settings
- **XerahS.Common**: Utilities, helpers, extensions
- **XerahS.Uploaders**: Uploader implementations
- **XerahS.Platform.Abstractions**: Platform interfaces

Platform-specific implementations are in:
- `XerahS.Mobile.Android`: Android file picker, permissions
- `XerahS.Mobile.iOS`: iOS file picker, Share Extension bridge
- `XerahS.Platform.Mobile`: Shared mobile platform abstractions

### Mobile Native Theming

Avalonia mobile UI now supports runtime adaptive theming for iOS and Android.

Reference docs:

- Architecture: `../docs/architecture/MOBILE_THEMING.md`
- Styling guide: `guidelines/MOBILE_STYLING_GUIDE.md`
- Validation report: `../docs/reports/MOBILE_NATIVE_THEMING_VALIDATION_2026-02-17.md`
