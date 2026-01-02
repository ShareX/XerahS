# ShareX.Avalonia Developer Guide

## Architecture Overview

This project follows the **MVVM (Model-View-ViewModel)** pattern using the `CommunityToolkit.Mvvm` library.

### Key Projects
*   **ShareX.Avalonia.App**: Main application entry point
*   **ShareX.Avalonia.UI**: UI layer using Avalonia. Contains Views, ViewModels, and shared UI resources
*   **ShareX.Avalonia.Core**: Core application logic, task management (`WorkerTask`), and business models
*   **ShareX.Avalonia.Annotations**: Annotation system with 16+ annotation types and serialization support
*   **ShareX.Avalonia.ImageEffects**: 50+ image effects (filters, adjustments, manipulations) using SkiaSharp
*   **ShareX.Avalonia.Platform.***: Platform-specific implementations (e.g., `WindowsScreenCaptureService`)
*   **ShareX.Avalonia.ScreenCapture**: Screen capture logic and region selection
*   **ShareX.Avalonia.Uploaders**: Upload providers (Imgur, Amazon S3, etc.)
*   **ShareX.Avalonia.History**: Capture history management

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
*   **Models**: Located in `ShareX.Avalonia.Annotations/Models/`, inheriting from base `Annotation` class
*   **Types**: 16 annotation types including Rectangle, Ellipse, Arrow, Line, Text, Number, Blur, Pixelate, Magnify, Highlight, Freehand, SpeechBalloon, Image, Spotlight, SmartEraser, Crop
*   **Drawing**: Handled in `EditorView.axaml.cs` for performance and direct pointer manipulation
*   **State**: `MainViewModel` manages tool state (`ActiveTool`, `SelectedColor`, `StrokeWidth`, etc.)
*   **Undo/Redo**: Implemented using `Stack<Control>` to manage visual elements on canvas
*   **Serialization**: JSON-based using `System.Text.Json` with `[JsonDerivedType]` attributes for polymorphism
*   **Keyboard Shortcuts**: V(Select), R(Rectangle), E(Ellipse), A(Arrow), L(Line), P(Pen), H(Highlighter), T(Text), B(Balloon), N(Number), C(Crop), M(Magnify), S(Spotlight), F(Effects Panel)

### Image Effects System
*   **Auto-Discovery**: Effects are discovered via reflection from `ShareX.Avalonia.ImageEffects` assembly
*   **Categories**: Filters, Adjustments, Manipulations
*   **Parameter Binding**: Dynamic UI generation for effect parameters
*   **Integration**: `EffectsPanelView` provides UI, `MainViewModel` handles application logic

### Region Capture
Located in `Views/RegionCapture/`:
*   `RegionCaptureWindow`: Spans **all monitors** (Virtual Screen) with crosshair cursor
*   Multi-monitor DPI handling
*   Uses `System.Drawing.Graphics.CopyFromScreen` (GDI+) for pixel capture on Windows

## Contribution
1.  Review project documentation and existing code patterns
2.  Ensure code compiles with `dotnet build` (0 errors)
3.  Follow MVVM separation: UI logic in Views, business logic in ViewModels
4.  Add XML documentation for public APIs
5.  Test on multiple platforms when possible

## Building
```bash
dotnet build ShareX.Avalonia.sln
```

## Testing
```bash
dotnet test
```
