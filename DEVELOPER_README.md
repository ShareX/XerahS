# ShareX.Avalonia Developer Guide

## Architecture Overview

This project follows the **MVVM (Model-View-ViewModel)** pattern using the `CommunityToolkit.Mvvm` library.

### Key Projects
*   **ShareX.Avalonia.UI**: The main UI project using Avalonia. Contains Views, ViewModels, and App entry point.
*   **ShareX.Avalonia.Core**: Contains core logic, task management (`WorkerTask`), and models.
*   **ShareX.Avalonia.Platform.* **: Platform-specific implementations (e.g., `WindowsScreenCaptureService`).
*   **ShareX.Avalonia.Services.Abstractions**: Interfaces for platform services (`IScreenCaptureService`, `IClipboardService`).

### Services & Dependency Injection
Services are initialized in `Program.cs` and `App.axaml.cs`. We use a Service Locator pattern via `PlatformServices` static class for easy access in ViewModels (though Constructor Injection is preferred where possible).

### Annotation System
The annotation system is built on a `Canvas` overlay in `MainWindow.axaml`.
*   **Drawing**: Handled in code-behind (`MainWindow.axaml.cs`) for performance and direct pointer manipulation.
*   **State**: `MainViewModel` manages the tool state (`ActiveTool`, `SelectedColor`, etc.).
*   **Undo/Redo**: Implemented using `Stack<Control>` in the View to manage visual elements.

### Region Capture
Located in `Views/RegionCapture/`.
*   `RegionCaptureWindow`: Uses manual bounds calculation to span **all monitors** (Virtual Screen).
*   Uses `System.Drawing.Graphics.CopyFromScreen` (GDI+) for pixel capture on Windows.

## Contribution
1.  Follow the internal `task.md` for prioritized items.
2.  Ensure code compiles with `dotnet build`.
3.  Keep UI logic separated in ViewModels/Views appropriately.

## Building
```bash
dotnet build ShareX.Avalonia.sln
```
