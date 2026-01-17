# Platform Compatibility Audit

> **Generated**: 2026-01-14

## Summary

This document catalogs all Windows-only dependencies and `SupportedOSPlatform("windows")` usages across ShareX.Avalonia, grouped by feature area.

---

## 1. Files with `SupportedOSPlatform("windows")`

### Uploaders
| File | Symbol | Issue | Recommendation |
|------|--------|-------|----------------|
| [ImageUploader.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Uploaders/BaseUploaders/ImageUploader.cs) | `UploadImage(Image, string)` | Uses `System.Drawing.Image` | Add SKBitmap overload, deprecate old |

### Platform Abstractions
| File | Symbol | Issue | Recommendation |
|------|--------|-------|----------------|
| [ToastConfig.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Platform.Abstractions/ToastConfig.cs) | `ToastConfig` class | Uses `System.Drawing.Size`, `ContentAlignment` | Replace with cross-platform primitives |

### Media
| File | Symbol | Issue | Recommendation |
|------|--------|-------|----------------|
| [ImageBeautifierOptions.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Media/ImageBeautifierOptions.cs) | Class | Uses `System.Drawing.Drawing2D.LinearGradientMode` | Replace with enum |
| [GradientInfo.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Media/GradientInfo.cs) | Class | Uses `System.Drawing.Color`, `LinearGradientMode` | Replace with SKColor |

### Common Helpers (Windows-only implementations)
| File | Description |
|------|-------------|
| [NativeMethods.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/NativeMethods.cs) | P/Invoke declarations |
| [NativeMethods_Helpers.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/NativeMethods_Helpers.cs) | Helper wrappers |
| [CaptureHelpers.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Helpers/CaptureHelpers.cs) | GDI capture helpers |
| [ClipboardHelpersEx.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Helpers/ClipboardHelpersEx.cs) | Windows clipboard extensions |
| [ShortcutHelpers.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Helpers/ShortcutHelpers.cs) | Shell link creation |
| [RegistryHelpers.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Helpers/RegistryHelpers.cs) | Windows registry access |

### Native/OS-Specific
| File | Description |
|------|-------------|
| [DWMManager.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Native/DWMManager.cs) | Desktop Window Manager |
| [WindowInfo.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Native/WindowInfo.cs) | Window enumeration |
| [KeyboardHook.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Input/KeyboardHook.cs) | Low-level keyboard hook |

### Cryptography
| File | Description |
|------|-------------|
| [DPAPI.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Cryptographic/DPAPI.cs) | Windows Data Protection API |
| [DPAPIEncryptedStringValueProvider.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Settings/DPAPIEncryptedStringValueProvider.cs) | DPAPI settings provider |

### Graphics Extensions (Windows GDI+)
| File | Description |
|------|-------------|
| [GraphicsExtensions.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Extensions/GraphicsExtensions.cs) | `System.Drawing.Graphics` extensions |
| [GraphicsPathExtensions.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Extensions/GraphicsPathExtensions.cs) | `GraphicsPath` extensions |
| [GraphicsQualityManager.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/GraphicsQualityManager.cs) | GDI+ quality settings |

### Other
| File | Description |
|------|-------------|
| [CursorData.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/CursorData.cs) | Cursor capture |
| [FontSafe.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/FontSafe.cs) | GDI+ Font wrapper |
| [XmlFont.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/XmlFont.cs) | Font serialization |
| [ImageFilesCache.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/ImageFilesCache.cs) | Image caching |
| [DesktopIconManager.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/DesktopIconManager.cs) | Desktop icon visibility |
| [PrintSettings.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/PrintSettings.cs) | Print configuration |

### Screen Recording
| File | Description |
|------|-------------|
| [ScreenRecorderService.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.ScreenCapture/ScreenRecording/ScreenRecorderService.cs) | Windows GDI capture method |

---

## 2. Files with `System.Drawing` Usage (No Attribute)

These files import `System.Drawing` for primitive types but lack the OS attribute:

| Area | Files |
|------|-------|
| **Core Models** | `ApplicationConfig.cs`, `TaskSettings.cs`, `TaskSettingsOptions.cs` |
| **Tasks** | `WorkerTask.cs`, `WorkflowTask.cs` |
| **Platform Services** | `IScreenService.cs`, `IInputService.cs`, `IWindowService.cs` |
| **UI Services** | `WindowService.cs`, `ScreenService.cs`, `RecordingBorderWindow.axaml.cs` |
| **Media** | `FFmpegCLIManager.cs`, `VideoInfo.cs`, `VideoThumbnailOptions.cs` |
| **History** | `ImageHistorySettings.cs` |
| **Recording** | `RecordingModels.cs`, `RegionCropper.cs` |

---

## 3. Recommended Type Replacements

| `System.Drawing` Type | Cross-Platform Replacement |
|----------------------|---------------------------|
| `Point` | `Avalonia.Point` or `SKPointI` |
| `PointF` | `Avalonia.Point` or `SKPoint` |
| `Size` | `Avalonia.Size` or `SKSizeI` |
| `SizeF` | `Avalonia.Size` or `SKSize` |
| `Rectangle` | `Avalonia.PixelRect` or `SKRectI` |
| `RectangleF` | `Avalonia.Rect` or `SKRect` |
| `Color` | `Avalonia.Media.Color` or `SKColor` |
| `Image` | `SKBitmap` or `SKImage` |
| `Bitmap` | `SKBitmap` |
| `ContentAlignment` | Custom enum |
| `LinearGradientMode` | Custom enum |

---

## 4. Already Cross-Platform

These components already use cross-platform types:

| Component | Notes |
|-----------|-------|
| [IClipboardService](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Platform.Abstractions/IClipboardService.cs) | Uses `SKBitmap` ✓ |
| [ImageHelpers.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Common/Helpers/ImageHelpers.cs) | Uses `SKBitmap` for load/save/resize ✓ |
| [CaptureJobProcessor.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Core/Tasks/Processors/CaptureJobProcessor.cs) | Uses file-based upload ✓ |
| [FileUploader.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Uploaders/BaseUploaders/FileUploader.cs) | Stream-based, cross-platform ✓ |

---

## 5. Priority Ranking

### High Priority (Core Upload/Capture Pipeline)
1. `ImageUploader.cs` - Add cross-platform `UploadImage(SKBitmap)` method
2. `ToastConfig.cs` - Replace `System.Drawing` primitives

### Medium Priority (Settings/Config)
3. Core model files using `System.Drawing` for primitives
4. `IScreenService`, `IWindowService` interfaces with `Rectangle`/`Point`

### Low Priority (Windows-Only Features OK)
5. `NativeMethods*.cs`, `DPAPI.cs`, `RegistryHelpers.cs` - Truly Windows-only
6. GDI+ specific files - Already correctly marked as Windows-only
