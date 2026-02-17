# XerahS Architecture Map

**Date**: 2026-01-18
**Reviewer**: Senior C# Solution Reviewer
**Branch**: feature/SIP0016-modern-capture
**Version**: 0.1.0

---

## Executive Summary

XerahS is a cross-platform screen capture and upload application built on Avalonia UI, following a **platform abstraction architecture** with clear separation between cross-platform business logic and OS-specific implementations.

### Key Architectural Principles
1. **Platform Abstraction First**: All OS-specific code isolated behind interfaces
2. **Plugin-Based Extensibility**: Dynamic uploader plugin system with assembly isolation
3. **MVVM UI Architecture**: Clear separation of concerns using Avalonia + ReactiveUI
4. **Service Locator Pattern**: Centralized platform service registration
5. **Cross-Platform Core**: Business logic targets `net10.0`, platform code targets Windows-specific TFM

---

## 1. Entry Points & Responsibilities

### 1.1 XerahS.App (Desktop Application)
- **File**: [Program.cs](../../src/XerahS.App/Program.cs)
- **Purpose**: Main GUI application with full Avalonia UI
- **Target Framework**: `net10.0-windows10.0.26100.0`
- **Initialization Flow**:
  1. Initialize logging to `Documents/XerahS/Logs/`
  2. Detect and initialize platform services (Windows/macOS/Linux)
  3. Configure capture backend (DXGI Modern vs GDI+ fallback)
  4. Bootstrap async recording initialization (non-blocking)
  5. Launch Avalonia UI
- **Key Dependencies**: XerahS.UI, XerahS.Core, Platform implementations

### 1.2 XerahS.CLI (Command-Line Interface)
- **File**: [Program.cs](../../src/XerahS.CLI/Program.cs)
- **Purpose**: Headless CLI for automation and scripting
- **Target Framework**: `net10.0-windows10.0.26100.0`
- **Commands**:
  - `workflow run <id>` - Execute workflow by ID
  - `record` - Screen recording
  - `capture` - Screenshot capture
  - `list workflows` - Enumerate workflows
  - `config` - Configuration management
  - `backup-settings` - Create settings backup
  - `verify-region-capture` - Test region selector
  - `compare-capture` - Compare capture methods
- **Key Dependencies**: XerahS.Bootstrap, XerahS.Core, System.CommandLine

### 1.3 XerahS.PluginExporter (Utility)
- **File**: [Program.cs](../../src/XerahS.PluginExporter/Program.cs)
- **Purpose**: Package uploader plugins into `.xsdp` archives
- **Usage**: Internal development tool

---

## 2. Project Dependency Graph

```
┌─────────────────────────────────────────────────────────────────┐
│                        FOUNDATION LAYER                          │
├─────────────────────────────────────────────────────────────────┤
│  XerahS.Services.Abstractions (Service interfaces)              │
│  XerahS.Platform.Abstractions (OS abstraction contracts)        │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌─────────────────────────────────────────────────────────────────┐
│                         CORE LIBRARIES                           │
├─────────────────────────────────────────────────────────────────┤
│  XerahS.Common (Cross-platform utilities)                       │
│  XerahS.History (SQLite persistence)                            │
│  XerahS.Uploaders (Upload abstraction + plugins)                │
│  XerahS.Media (FFmpeg wrapper)                                  │
│  XerahS.Indexer (File indexing)                                 │
│  XerahS.RegionCapture (Screen selector UI + recording)          │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌─────────────────────────────────────────────────────────────────┐
│                       BUSINESS LOGIC                             │
├─────────────────────────────────────────────────────────────────┤
│  XerahS.Core (Orchestration, managers, workflow engine)         │
│    - TaskManager, ScreenRecordingManager                        │
│    - Workflow execution, settings management                    │
│    - References ShareX.Editor (external)                        │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌─────────────────────────────────────────────────────────────────┐
│                    PLATFORM IMPLEMENTATIONS                      │
├─────────────────────────────────────────────────────────────────┤
│  XerahS.Platform.Windows (Win32, DXGI, WGC, Media Foundation)   │
│  XerahS.Platform.MacOS (screencapture, native hotkeys)          │
│  XerahS.Platform.Linux (stub implementations)                   │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌─────────────────────────────────────────────────────────────────┐
│                          UI LAYER                                │
├─────────────────────────────────────────────────────────────────┤
│  XerahS.ViewModels (UI-agnostic ViewModels + ReactiveUI)        │
│  XerahS.UI (Avalonia AXAML views + services)                    │
│    - Fluent Design theme                                        │
│    - Tab navigation (History, Workflows, Settings, Editor)      │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │
┌─────────────────────────────────────────────────────────────────┐
│                    BOOTSTRAP & ENTRY POINTS                      │
├─────────────────────────────────────────────────────────────────┤
│  XerahS.Bootstrap (Shared initialization)                       │
│  XerahS.App (Desktop GUI)                                       │
│  XerahS.CLI (Headless automation)                               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                         PLUGIN SYSTEM                            │
├─────────────────────────────────────────────────────────────────┤
│  XerahS.Imgur.Plugin (Imgur uploader)                           │
│  XerahS.AmazonS3.Plugin (S3 uploader)                           │
│  Third-party *.xsdp plugins (dynamically loaded)               │
└─────────────────────────────────────────────────────────────────┘
```

### Dependency Rules
1. **Platform.Abstractions** defines contracts, no implementation details
2. **Common** contains zero OS-specific code (pure cross-platform utilities)
3. **Core** orchestrates but delegates platform calls to abstractions
4. **Platform implementations** are conditionally compiled and loaded at runtime
5. **UI** depends on Core but not directly on platform implementations

---

## 3. Platform Abstraction Layer

### 3.1 Central Service Locator
**File**: [PlatformServices.cs](../../src/XerahS.Platform.Abstractions/PlatformServices.cs)

**Pattern**: Static service locator with lazy initialization validation

**Registered Services**:

| Service Property | Interface | Responsibility |
|-----------------|-----------|----------------|
| PlatformInfo | IPlatformInfo | OS detection, version info, architecture |
| Screen | IScreenService | Monitor enumeration, DPI handling, bounds |
| Clipboard | IClipboardService | Cross-platform clipboard read/write |
| Window | IWindowService | Window enumeration, focus management |
| ScreenCapture | IScreenCaptureService | Screenshot capture (region/full/window) |
| Hotkey | IHotkeyService | Global hotkey registration |
| Input | IInputService | Mouse/keyboard simulation |
| Fonts | IFontService | System font enumeration |
| Startup | IStartupService | Launch-on-startup configuration |
| System | ISystemService | System operations (shutdown, sleep, etc.) |
| Diagnostic | IDiagnosticService | Platform diagnostics and troubleshooting |
| ShellIntegration | IShellIntegrationService | File associations, context menus (optional) |
| Notification | INotificationService | Native OS notifications (optional) |
| UI | IUIService | Dialogs, message boxes (registered post-bootstrap) |
| Toast | IToastService | Custom toast windows (registered by UI layer) |

### 3.2 Platform Implementations

#### Windows ([XerahS.Platform.Windows](../../src/XerahS.Platform.Windows/))
**Initializer**: `WindowsPlatform.Initialize()`

**Key Services**:
- **WindowsScreenCaptureService**: GDI+ based capture (fallback)
- **WindowsModernCaptureService**: Direct3D11/DXGI capture (Windows 8+, hardware-accelerated)
- **WindowsGraphicsCaptureSource**: Windows.Graphics.Capture API (Windows 10 1803+)
- **MediaFoundationEncoder**: Native H264/H265 encoding via Media Foundation
- **WindowsHotkeyService**: Win32 global hotkey hooks
- **WindowsShellIntegrationService**: File association registration
- **WindowsNotificationService**: UWP toast notifications with AUMID

**Dependencies**:
- Vortice.Direct3D11 3.8.2 (Direct3D interop)
- Vortice.DXGI 3.8.2 (DXGI capture)
- Microsoft.Windows.CsWinRT 2.2.0 (WinRT projections)

**Recording Strategy**: Native WGC + Media Foundation, fallback to FFmpeg if unavailable

#### macOS ([XerahS.Platform.MacOS](../../src/XerahS.Platform.MacOS/))
**Initializer**: `MacOSPlatform.Initialize()`

**Key Services**:
- **MacOSScreenshotService**: Wraps `screencapture` CLI utility
- **MacOSHotkeyService**: Native Carbon/Cocoa hotkey APIs

**Recording Strategy**: FFmpeg-based (native AVFoundation API pending)

#### Linux ([XerahS.Platform.Linux](../../src/XerahS.Platform.Linux/))
**Initializer**: `LinuxPlatform.Initialize()`

**Status**: Stub implementations, planned for future expansion

### 3.3 Platform-Specific Considerations
- **Conditional Compilation**: `#if WINDOWS`, `#if MACOS`, `#if LINUX` preprocessor directives
- **TFM Strategy**: `net10.0-windows10.0.26100.0` for Windows projects, `net10.0` for cross-platform
- **P/Invoke Isolation**: All native Win32 calls confined to Platform.Windows
- **Unsafe Code**: Allowed in Platform.Windows and Common (performance-critical bitmap operations)

---

## 4. Configuration Architecture

### 4.1 Configuration Files
**Storage Location**: `%USERPROFILE%\Documents\XerahS\Settings\`

| File | Class | Purpose |
|------|-------|---------|
| ApplicationConfig.json | ApplicationConfig | Main app settings (UI, paths, behavior) |
| UploadersConfig.json | UploadersConfig | Uploader instances, credentials (DPAPI-encrypted) |
| WorkflowsConfig.json | WorkflowsConfig | Hotkey workflows, task settings |

**Machine-Specific Override**: Optional `UploadersConfig-<HOSTNAME>.json` when `UseMachineSpecificUploadersConfig` enabled

### 4.2 Settings Manager
**File**: [SettingsManager.cs](../../src/XerahS.Core/Managers/SettingsManager.cs)

**Responsibilities**:
- Centralized configuration loading/saving
- Exposes static properties: `Settings`, `UploadersConfig`, `WorkflowsConfig`
- Manages backup folder for weekly/daily snapshots
- Graceful fallback for corrupted JSON
- JSON serialization via Newtonsoft.Json

**Key Paths** (from `PathsManager`):
- `PersonalFolder`: `%USERPROFILE%\Documents\XerahS\`
- `SettingsFolder`: `PersonalFolder\Settings\`
- `HistoryFolder`: `PersonalFolder\History\`
- `ScreenshotsFolder`: `PersonalFolder\Screenshots\`
- `ScreencastsFolder`: `PersonalFolder\Screencasts\`
- `BackupFolder`: `PersonalFolder\Backup\`

### 4.3 Security & Encryption
- **DPAPI Encryption**: Used for sensitive uploader credentials (Windows-only)
- **File**: [Encryption.cs](../../src/XerahS.Common/Encryption.cs)
- **Fallback**: Plain JSON on non-Windows platforms (future: cross-platform encryption)

---

## 5. Logging Pipeline

### 5.1 Initialization
**File**: [DebugHelper.cs](../../src/XerahS.Common/DebugHelper.cs)

**Setup**: `DebugHelper.Init(logPath)` creates singleton `Logger` instance

**Default Path**: `Documents\XerahS\Logs\yyyy-MM\XerahS-yyyyMMdd.log`

### 5.2 Logger Implementation
**File**: [Logger.cs](../../src/XerahS.Common/Logger.cs)

**Features**:
- Async write queue for non-blocking I/O (configurable: `Logger.AsyncWrite`)
- Synchronous mode during startup for critical diagnostics
- Auto-creates log directory structure (`yyyy-MM/XerahS-yyyyMMdd.log`)
- Thread-safe message queue with background processing

### 5.3 Log Destinations
1. **File**: Timestamped daily logs in year-month folder hierarchy
2. **Debug Output**: `System.Diagnostics.Debug.WriteLine` fallback if logger uninitialized
3. **Troubleshooting Logs**: Specialized `TroubleshootingHelper.Log()` for screen recording diagnostics

### 5.4 Log Conventions
- **No formal levels**: Uses prefixes like "ERROR:", "WARNING:", "✓", "✗"
- **Exception Logging**: `DebugHelper.WriteException(ex)` includes stack traces
- **Contextual Markers**: Workflow IDs, task types embedded in messages

---

## 6. Key Namespace Responsibilities

### XerahS.Common
**Purpose**: Cross-platform foundation utilities

**Key Types**:
- `DebugHelper`, `Logger` - Logging infrastructure
- `FileHelpers`, `PathsManager` - File/path operations
- `SettingsBase<T>` - JSON serialization base class
- `Encryption`, `Cryptographic` - DPAPI wrapper
- `ColorBgra`, `HSB`, `CMYK` - Color manipulation
- `HttpClientFactory` - Shared HTTP client management
- `NativeMethods` - Platform detection stubs

**Dependencies**: Platform.Abstractions only

### XerahS.Core
**Purpose**: Business logic orchestration

**Structure**:
- `Managers/` - SettingsManager, TaskManager, ScreenRecordingManager, CleanupManager
- `Tasks/` - WorkerTask, WorkflowTask, IJobProcessor, job processors
- `Helpers/` - TaskHelpers (workflow execution), TroubleshootingHelper
- `Hotkeys/` - WorkflowManager, HotkeyInfo
- `Models/` - ApplicationConfig, WorkflowsConfig, TaskSettings
- `Services/` - EditorService, QRCodeService

**Dependencies**: Common, Platform.Abstractions, History, Uploaders, RegionCapture, ShareX.Editor

### XerahS.Platform.Abstractions
**Purpose**: OS abstraction contracts

**Key Interfaces**:
- `IScreenCaptureService` - Capture API (region, full, window)
- `IScreenService` - Monitor enumeration, DPI queries
- `IHotkeyService` - Global hotkey registration
- `IClipboardService` - Clipboard operations
- `IWindowService` - Window management (enumerate, focus)
- `IRegionCaptureBackend` - Region selector UI contract

**Models**: `HotkeyInfo`, `MonitorInfo`, `CaptureOptions`, `ToastConfig`

### XerahS.Uploaders
**Purpose**: Upload abstraction and plugin system

**Structure**:
- `BaseUploaders/` - Abstract classes (Uploader, FileUploader, ImageUploader)
- `PluginSystem/` - PluginLoader, PluginDiscovery, IUploaderProvider, PluginManifest
- `FileUploaders/`, `ImageUploaders/`, `TextUploaders/`, `URLShorteners/` - Built-in providers
- `CustomUploader/` - User-defined uploader scripting engine
- `OAuth/` - OAuth1/OAuth2 helpers

**Plugin Pattern**: Dynamic assembly loading via `AssemblyLoadContext` with isolated contexts

### XerahS.RegionCapture
**Purpose**: Interactive region selector and screen recording

**Structure**:
- `UI/` - Avalonia-based region selector window
- `Shapes/` - Annotation shapes (rectangle, ellipse, arrow, text)
- `ScreenRecording/` - ScreenRecorderService, ICaptureSource, IEncoder
- `Platform/` - Platform-specific capture backends
- `Animations/` - Fade/zoom effects

**Dependencies**: Avalonia, SkiaSharp 2.88.9, XerahS.Media (FFmpeg)

### XerahS.History
**Purpose**: Task history persistence

**Storage**: SQLite database (`XerahS.History.db` in PersonalFolder)

**Features**: Thumbnail caching, upload metadata, result tracking

### XerahS.UI
**Purpose**: Avalonia MVVM presentation layer

**Structure**:
- `Views/` - AXAML views (MainWindow, SettingsView, HistoryView, WorkflowsView)
- `ViewModels/` - View models (MainViewModel, SettingsViewModel)
- `Services/` - AvaloniaUIService, AvaloniaToastService, EditorClipboardAdapter, ScreenCaptureService (UI wrapper)
- `Controls/` - Reusable custom controls

**Theme**: FluentAvaloniaUI 2.4.1 (Windows 11 Fluent Design)

### XerahS.ViewModels
**Purpose**: UI-agnostic view models

**Pattern**: ReactiveUI for property change notifications

**Dependencies**: Services.Abstractions, Common (no platform-specific code)

### XerahS.Bootstrap
**Purpose**: Shared initialization logic for App and CLI

**File**: [ShareXBootstrap.cs](../../src/XerahS.Bootstrap/ShareXBootstrap.cs)

**Operations**:
1. Platform service initialization (OS detection + registration)
2. Configuration loading (SettingsManager)
3. Async recording initialization (background)
4. Logging setup

---

## 7. Plugin System Architecture

### 7.1 Design Pattern
**Pattern**: Dynamic provider-based plugin system with assembly isolation

**Interface**: [IUploaderProvider](../../src/XerahS.Uploaders/PluginSystem/IUploaderProvider.cs)

### 7.2 Plugin Discovery
**File**: [PluginDiscovery.cs](../../src/XerahS.Uploaders/PluginSystem/PluginDiscovery.cs)

**Search Path**: `Documents\XerahS\Plugins\*.xsdp`

**Manifest Format**: `plugin.manifest.json`
```json
{
  "pluginId": "unique-id",
  "name": "Display Name",
  "version": "1.0.0",
  "author": "Author Name",
  "entryPoint": "Namespace.ClassName, AssemblyName",
  "dependencies": ["HelperLib.dll"],
  "supportedCategories": ["ImageUploader", "FileUploader"]
}
```

### 7.3 Plugin Loading
**File**: [PluginLoader.cs](../../src/XerahS.Uploaders/PluginSystem/PluginLoader.cs)

**Mechanism**: `AssemblyLoadContext` per plugin for isolation

**Process**:
1. Scan plugin directories for manifests
2. Validate manifest JSON schema
3. Create isolated `PluginLoadContext` per plugin
4. Load assembly + dependencies from plugin folder
5. Instantiate provider type via reflection
6. Register in `ProviderCatalog` by category

### 7.4 Plugin Instances
**Multi-Instance Support**: Each provider can have multiple named instances
- Example: "Work S3", "Personal S3" from same Amazon S3 plugin
- Configuration: Per-instance JSON settings in UploadersConfig
- UI: ProviderCatalogDialog for browsing and adding instances

### 7.5 Built-in Providers
Treated as internal plugins (no separate assemblies):
- Registered via `UploaderFactory.CreateBuiltInProviders()`
- Examples: Imgur, Dropbox, Google Drive, FTP, SFTP

### 7.6 Plugin Packaging
**Tool**: XerahS.PluginExporter

**Format**: `.xsdp` archive (ZIP-based) containing:
- Plugin DLL + dependencies
- `plugin.manifest.json`
- Optional README, license

---

## 8. UI Architecture (MVVM)

### 8.1 Pattern: Model-View-ViewModel
**Framework**: Avalonia UI 11.3.10 + ReactiveUI 22.3.1

**Dependency Injection**: Manual service locator via `PlatformServices`

### 8.2 Views (AXAML)
Located in [XerahS.UI/Views](../../src/XerahS.UI/Views/)

| View | Purpose |
|------|---------|
| MainWindow.axaml | Primary window with tab navigation |
| SettingsView.axaml | Application settings tabs |
| HistoryView.axaml | Task history browser with thumbnail grid |
| WorkflowsView.axaml | Hotkey workflow configuration |
| HotkeySettingsView.axaml | Individual workflow editor |
| DestinationSettingsView.axaml | Upload destination configuration |
| AfterCaptureWindow.axaml | Post-capture action selector |
| ToastWindow.axaml | Custom toast notification window |
| EditorWindow.axaml | Embedded ShareX.Editor host |
| RecordingView.axaml | Screen recording controls |
| ProviderCatalogDialog.axaml | Plugin instance manager |

### 8.3 ViewModels
Located in [XerahS.UI/ViewModels](../../src/XerahS.UI/ViewModels/)

| ViewModel | Responsibilities |
|-----------|-----------------|
| MainViewModel | Navigation, command routing, preview image |
| SettingsViewModel | ApplicationConfig two-way binding |
| WorkflowsViewModel | Workflow list CRUD operations |
| WorkflowItemViewModel | Individual workflow representation |
| HotkeyItemViewModel | Hotkey binding editor |
| TaskSettingsViewModel | Per-workflow task settings binding |
| HistoryViewModel | History database queries and filtering |
| ProviderCatalogViewModel | Plugin instance management |
| ToastViewModel | Toast display lifecycle |
| RecordingViewModel | Screen recording state management |

### 8.4 UI Services
Located in [XerahS.UI/Services](../../src/XerahS.UI/Services/)

| Service | Purpose |
|---------|---------|
| AvaloniaUIService | Implements IUIService for dialogs, message boxes |
| AvaloniaToastService | Implements IToastService for custom toast windows |
| ScreenCaptureService | UI wrapper around platform capture (shows region selector) |
| EditorClipboardAdapter | Bridges ShareX.Editor to platform clipboard |
| MainViewModelHelper | Wires upload callbacks between editor and core |

### 8.5 Data Flow
1. **User Action** → View (AXAML button/control)
2. **View** → ViewModel (Command binding via ReactiveUI)
3. **ViewModel** → Core Services (`PlatformServices`, `SettingsManager`, `TaskManager`)
4. **Core** → Platform Abstractions → Platform Implementation
5. **Result** → ViewModel (property updates via `INotifyPropertyChanged`)
6. **ViewModel** → View (data binding automatic refresh)

### 8.6 Navigation
- **MainWindow**: Tab-based navigation (History, Workflows, Settings, Editor)
- **Dialogs**: Opened via `IUIService.ShowDialog<T>()`
- **Toast Notifications**: Shown via `IToastService.ShowToast()`

### 8.7 Theming
- **Library**: FluentAvaloniaUI 2.4.1
- **Theme**: Fluent Design System (Windows 11 style)
- **Dark/Light Mode**: System-aware theme switching

---

## 9. Cross-Cutting Concerns

### 9.1 Error Handling
- **Exception Logging**: `DebugHelper.WriteException(ex)` with stack traces
- **Graceful Degradation**: Platform services fallback (e.g., DXGI → GDI+)
- **User Feedback**: Toast notifications for workflow errors

### 9.2 Performance
- **Async I/O**: File operations, uploads, configuration saves
- **Background Tasks**: Recording initialization, history loading
- **DPI Awareness**: Mixed-DPI monitor support in region capture

### 9.3 Security
- **Credential Encryption**: DPAPI for UploadersConfig (Windows)
- **Plugin Isolation**: AssemblyLoadContext prevents plugin conflicts
- **Input Validation**: Settings validators for configuration integrity

### 9.4 Testing
- **Test Project**: [XerahS.Tests](../../tests/XerahS.Tests/) (NUnit 4.2.2)
- **Coverage**: Partial (focused on critical workflows)
- **Note**: Test project exists but not in solution file (requires integration)

### 9.5 External Integrations
- **ShareX.Editor**: External Avalonia image editor (shared component)
- **FFmpeg**: CLI wrapper in `XerahS.Media.FFmpegCLIManager` for video encoding fallback

---

## 10. Build & Deployment

### 10.1 Target Frameworks
- **Windows-specific**: `net10.0-windows10.0.26100.0`
- **Cross-platform**: `net10.0`

### 10.2 Key NuGet Dependencies
- **Avalonia**: 11.3.10 (UI framework)
- **SkiaSharp**: 2.88.9 (locked to 2.x - Avalonia 11 compatibility)
- **Newtonsoft.Json**: 13.0.4 (configuration serialization)
- **FluentAvaloniaUI**: 2.4.1 (theming)
- **ReactiveUI**: 22.3.1 (MVVM framework)
- **System.CommandLine**: 2.0.1 (CLI parsing)
- **Vortice.Direct3D11/DXGI**: 3.8.2 (Windows capture)

### 10.3 Conditional Compilation
- **Preprocessor Symbols**: `WINDOWS`, `MACOS`, `LINUX`
- **Platform-Specific References**: Conditionally included in .csproj files

### 10.4 Build Artifacts
- **XerahS.exe**: Desktop application (Windows)
- **xerahs**: CLI executable (cross-platform)
- **Plugins**: `.xsdp` archives auto-deployed to `bin/.../Plugins/`

---

## 11. Architectural Patterns Summary

| Pattern | Usage |
|---------|-------|
| Service Locator | `PlatformServices` static class |
| Strategy | Platform-specific implementations behind abstractions |
| Plugin Architecture | Dynamic provider loading with assembly isolation |
| MVVM | Avalonia + ReactiveUI separation of concerns |
| Factory | `UploaderFactory`, encoder/capture source factories |
| Repository | `HistoryManager` (SQLite persistence) |
| Observer | ReactiveUI property changes, event callbacks |
| Singleton | `TaskManager.Instance`, `ScreenRecordingManager.Instance` |

---

## 12. Future Considerations

### 12.1 Platform Expansion
- Expand Linux platform implementation (X11/Wayland capture)
- macOS native AVFoundation recording
- ARM64 support (Windows/Linux/macOS)

### 12.2 Architecture Improvements
- Migrate from service locator to dependency injection (Microsoft.Extensions.DependencyInjection)
- Implement structured logging (Serilog or Microsoft.Extensions.Logging)
- Add comprehensive integration tests

### 12.3 Performance Optimization
- Bitmap pooling for high-frequency captures
- Memory-mapped file for large history thumbnails
- Plugin assembly trimming and AOT compatibility

---

**Last Updated**: 2026-01-18
**Review Phase**: 2 - Architecture Mapping
**Status**: ✅ COMPLETE

---

*This document serves as the authoritative reference for XerahS architecture.*
*Update this document when making significant architectural changes.*
