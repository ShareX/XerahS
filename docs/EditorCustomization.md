# Editor Customization Guide

## Overview

ShareX.Editor is a decoupled, reusable editor library that can be integrated into different host applications. This guide explains how to customize the editor window title to reflect the host application's name.

## Customizing the Editor Window Title

The editor window title can be customized by setting the `ApplicationName` property on the `MainViewModel` before displaying the editor window.

### Default Behavior

By default, the editor window title is "ShareX Editor" (using the default `ApplicationName = "ShareX"`).

### Customization in XerahS (ShareX.Avalonia Fork)

XerahS customizes the editor title to display "XerahS Editor" by using a centralized application name constant.

#### Application Name Constant

The app name is defined in `ShareXResources.cs`:

```csharp
public static class ShareXResources
{
    public const string AppName = "XerahS";
    public const string ProductName = AppName;
    // ...
}
```

#### 1. Main Window Editor View

Located in `App.axaml.cs`:

```csharp
var mainViewModel = new MainViewModel();
mainViewModel.ApplicationName = ShareXResources.AppName;  // Uses "XerahS"

desktop.MainWindow = new Views.MainWindow
{
    DataContext = mainViewModel,
};
```

#### 2. Standalone Editor Window

Located in `AvaloniaUIService.ShowEditorAsync()`:

```csharp
var editorViewModel = new MainViewModel();
editorViewModel.ShowCaptureToolbar = false;
editorViewModel.ApplicationName = ShareXResources.AppName;  // Uses "XerahS"

editorWindow.DataContext = editorViewModel;
```

### Why Use a Centralized Constant?

Using `ShareXResources.AppName` ensures:
- **Consistency**: The app name is defined in one place
- **Maintainability**: Changing the app name only requires updating one constant
- **Correctness**: The editor title automatically matches the application name

### Integration Example for Other Applications

When integrating ShareX.Editor into a different application, create a similar centralized constant:

```csharp
// MyApp.Common/AppResources.cs
public static class AppResources
{
    public const string AppName = "MyApp";
}

// MyApp.UI/Services/EditorService.cs
var editorViewModel = new ShareX.Editor.ViewModels.MainViewModel();
editorViewModel.ApplicationName = AppResources.AppName;  // Window title becomes "MyApp Editor"

var editorWindow = new EditorWindow
{
    DataContext = editorViewModel
};

editorViewModel.UpdatePreview(myImage);
editorWindow.Show();
```

## Implementation Details

### MainViewModel Properties

The `MainViewModel` class exposes:

- `ApplicationName` (string, observable property): The name of the host application
- `EditorTitle` (string, computed property): Returns `"{ApplicationName} Editor"`

### XAML Binding

The `EditorWindow.axaml` binds its `Title` property to the `EditorTitle`:

```xml
<Window Title="{Binding EditorTitle}" ...>
```

This ensures the window title updates automatically when `ApplicationName` changes.

## Common Mistake: Hardcoding the Application Name

? **Don't do this:**
```csharp
editorViewModel.ApplicationName = "ShareX.Avalonia";  // Hardcoded!
```

? **Do this instead:**
```csharp
editorViewModel.ApplicationName = ShareXResources.AppName;  // Uses constant
```

## Benefits

1. **Branding**: Each host application can display its own name in the editor
2. **User Experience**: Users know which application the editor belongs to
3. **Flexibility**: The same editor library works seamlessly across different host applications
4. **Maintainability**: Single source of truth for application name

## Related Files

- `ShareX.Avalonia.Common/ShareXResources.cs`: Centralized app name constant
- `ShareX.Editor/ViewModels/MainViewModel.cs`: Contains the `ApplicationName` and `EditorTitle` properties
- `ShareX.Avalonia.UI/Views/EditorWindow.axaml`: Window definition with title binding
- `ShareX.Avalonia.UI/App.axaml.cs`: Main window initialization
- `ShareX.Avalonia.UI/Services/AvaloniaUIService.cs`: Standalone editor window creation

## Future Enhancements

Consider extending this pattern to customize other branding elements:
- Window icon (use app-specific icon)
- Color schemes/themes
- About dialog information
- Application-specific menu items
