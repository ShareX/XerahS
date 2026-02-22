# Editor Extraction: ShareX.Editor Standalone Project

## Executive Summary

The image editor component was successfully extracted from XerhaS (XerahS) into a standalone project called **ShareX.Editor**. This architectural change enables the editor to be shared across XerahS, and the original ShareX (WinForms.

## Background

### Origins

The image editor was originally developed as part of the **XerahS** project. The editor provides a comprehensive suite of annotation tools and image effects built on Avalonia UI and SkiaSharp.

### Motivation

The extraction was driven by the need to:

1. **Enable WinForms Integration**: Allow the original ShareX (WinForms) application to utilize the modern Avalonia-based editor
2. **Reduce Code Duplication**: Create a single, shared codebase for editor functionality
3. **Improve Maintainability**: Centralize editor development and bug fixes in one location
4. **Support Multiple Consumers**: Enable XerahS, XerahS, and ShareX WinForms to all use the same editor

## Project Structure

### New Repository Location

```
XerahS.Editor/
├── src/
│   ├── ShareX.Editor/           # Core editor library
│   │   ├── Annotations/         # 20+ annotation types
│   │   ├── ImageEffects/        # 51 image effect classes
│   │   ├── ViewModels/          # MVVM architecture
│   │   ├── Views/               # Avalonia XAML views
│   │   ├── Services/            # Editor services
│   │   └── Serialization/       # Save/load annotations
│   └── ShareX.Editor.Loader/    # Standalone demo app
├── docs/
└── ShareX.Editor.sln
```

### Technology Stack

- **UI Framework**: Avalonia 11.3.10 (cross-platform)
- **Rendering**: SkiaSharp 2.88.9 (high-performance graphics)
- **Architecture**: MVVM with CommunityToolkit.Mvvm
- **Target Framework**: .NET 10.0 (`net10.0-windows` on Windows, `net10.0` on other platforms)

## What Was Moved

### From XerahS to ShareX.Editor

| Component | Files | Description |
|-----------|-------|-------------|
| **Annotations** | 20 files | All annotation types (Rectangle, Ellipse, Arrow, Text, Blur, etc.) |
| **ImageEffects** | 51 files | Effects organized in 4 categories: Adjustments, Filters, Manipulations, Drawings |
| **ViewModels** | Multiple | MainViewModel, tool state management |
| **Views** | Multiple | EditorView, EditorCanvas, toolbar controls |
| **Serialization** | 1 file | AnnotationSerializer for save/load functionality |

### Removed from XerahS

The following projects were removed after migration:

- `src/XerahS.Annotations/` → Consolidated into ShareX.Editor
- `src/XerahS.ImageEffects/` → Consolidated into ShareX.Editor
- `src/desktop/app/XerahS.UI/ViewModels/MainViewModel.cs` → Moved to ShareX.Editor
- `src/desktop/app/XerahS.UI/Views/EditorView.axaml*` → Moved to ShareX.Editor

## Namespace Changes

All code was reorganized under the `ShareX.Editor` namespace:

| Old Namespace | New Namespace |
|---------------|---------------|
| `ShareX.Ava.Annotations.Models` | `ShareX.Editor.Annotations` |
| `ShareX.Ava.Annotations.Serialization` | `ShareX.Editor.Serialization` |
| `ShareX.Ava.ImageEffects` | `ShareX.Editor.ImageEffects` |
| `ShareX.Ava.ImageEffects.Helpers` | `ShareX.Editor.ImageEffects` |

## Integration Points

### XerahS Integration

XerahS now references ShareX.Editor as an external dependency:

```csharp
// XerahS.UI/Views/EditorWindow.axaml.cs
using ShareX.Editor.ViewModels;
using ShareX.Editor.Views;

var editorViewModel = new MainViewModel();
editorViewModel.ApplicationName = AppResources.AppName;
editorViewModel.UpdatePreview(image);

var editorWindow = new EditorWindow
{
    DataContext = editorViewModel
};
```

**Key Files**:
- [XerahS.UI/Views/EditorWindow.axaml](../../src/desktop/app/XerahS.UI/Views/EditorWindow.axaml) - Window host
- [XerahS.UI/Services/AvaloniaUIService.cs](../../src/desktop/app/XerahS.UI/Services/AvaloniaUIService.cs) - Editor launch service
- [XerahS.UI/Services/EditorClipboardAdapter.cs](../../src/desktop/app/XerahS.UI/Services/EditorClipboardAdapter.cs) - Clipboard integration

### XerahS Integration

XerahS (the original project where the editor was born) continues to use ShareX.Editor seamlessly:

```csharp
// XerahS customizes the editor window title
var editorViewModel = new MainViewModel();
editorViewModel.ApplicationName = AppResources.AppName;  // Shows product-based title
```

**Reference**: [EditorCustomization.md](./EditorCustomization.md)

### ShareX WinForms Integration (Planned)

The extraction was specifically designed to enable ShareX (WinForms) integration. The planned approach:

1. Add ShareX.Editor reference to ShareX.csproj
2. Create an Avalonia control host in WinForms
3. Convert between GDI+ Bitmap and SkiaSharp SKBitmap
4. Replace existing editor with ShareX.Editor

**Status**: ⏸️ Pending (design complete, implementation not yet started)

## Key Features Preserved

All editor functionality was preserved during the extraction:

### Annotation Tools (17 types)
- Basic shapes: Rectangle, Ellipse, Line, Arrow
- Drawing: Freehand (Pen), Highlighter
- Text: Text boxes, Speech Balloons, Step Numbers
- Effects: Blur, Pixelate, Magnify, Highlight
- Advanced: Smart Eraser, Image/Sticker, Spotlight, Crop

### Image Effects (40+ effects)
- **Adjustments** (13): Brightness, Contrast, Gamma, Hue, Saturation, etc.
- **Filters** (17): Blur, Sharpen, Edge Detect, Emboss, etc.
- **Manipulations** (10): Crop, Resize, Rotate, Flip, etc.
- **Drawings** (6): Background, Border, Text, Checkerboard, etc.

### Core Capabilities
- Object-based selection, moving, and resizing
- Multi-level Undo/Redo
- Keyboard shortcuts for all tools
- Serialization (save/load annotations)
- Real-time preview
- Clipboard integration

## Git History

The editor extraction was completed through the following commits:

- **5ebb101** - Merge PR #21 "editor-move" (104 files changed, +418/-10,530 lines)
- **bfc56e4** - Improve clipboard image handling and update editor preview
- **7fed578** - Refactor to use MainViewModel from ShareX.Editor
- **86c6588** - Remove image editor and effects panel UI components
- **4411a17** - Refactor editor components to ShareX.Editor namespace
- **993d801** - Remove legacy editor and effects panel views

**Branch**: `feature/editor-decoupling` (merged to `develop`)

## Benefits Achieved

### 1. Code Reusability
- Single editor codebase serves multiple applications
- Reduced total lines of code (~10,000 lines removed from duplicates)

### 2. Maintainability
- Bug fixes and features automatically benefit all consumers
- Centralized testing and quality assurance

### 3. Cross-Platform Capability
- Editor runs on Windows, Linux, and macOS via Avalonia
- WinForms integration brings cross-platform editor to Windows-only ShareX

### 4. Independent Versioning
- ShareX.Editor can be versioned independently
- Easier to track editor-specific changes

### 5. Cleaner Architecture
- Clear separation of concerns
- Well-defined API boundaries

## Current Users

1. **XerahS** ✅ - Using via XerahS
2. **ShareX (WinForms)** ⏸️ - Planned integration

## Related Documentation

- [EditorCustomization.md](./EditorCustomization.md) - How to customize editor branding
- [XerahS.Editor README](../../../XerahS.Editor/README.md) - Editor project overview
- [XerahS README](../../README.md) - Main project documentation

## Technical Decisions

### Why Avalonia Instead of Framework-Agnostic?

The editor uses Avalonia's rendering primitives (`DrawingContext`) which provide:
- Hardware-accelerated rendering
- Cross-platform consistency
- Rich control system for interactive editing

While this creates a dependency on Avalonia, the benefits outweigh the costs:
- ✅ Better performance
- ✅ Consistent look/feel across platforms
- ✅ Mature UI framework
- ❌ Requires Avalonia host (solvable with WinForms.Avalonia package)

### Why SkiaSharp for Effects?

SkiaSharp provides:
- High-performance pixel manipulation
- Comprehensive image processing APIs
- Cross-platform compatibility
- Battle-tested in production apps

## Future Roadmap

1. **Complete WinForms Integration** - Enable ShareX to use ShareX.Editor
2. **NuGet Package** - Publish as reusable library
3. **Performance Optimizations** - Improve rendering for large images
4. **Additional Tools** - Add more annotation types as needed
5. **Plugin System** - Allow third-party annotation tools

## Lessons Learned

### What Went Well
- Clean separation of UI and business logic
- MVVM architecture made extraction straightforward
- Comprehensive test coverage ensured no regressions
- Clear namespace organization

### Challenges
- Managing clipboard integration across different frameworks
- Ensuring consistent behavior between old and new implementations
- Updating all references in XerahS

### Best Practices Established
- Centralized application name via `ApplicationName` property
- Adapter pattern for platform-specific services (clipboard, etc.)
- Dependency injection for service integration

## Conclusion

The extraction of ShareX.Editor into a standalone project represents a significant architectural improvement. It enables code sharing across the ShareX ecosystem while maintaining the full feature set and performance characteristics of the original editor. The project is production-ready for Avalonia applications and positioned for future WinForms integration.

---

**Last Updated**: 2026-01-11
**Status**: ✅ Complete (Avalonia) | ⏸️ Pending (WinForms)
**Maintainer**: ShareX Team
