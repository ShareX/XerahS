# ShareX.Avalonia

A cross-platform port of the popular **ShareX** screen capture and file sharing tool, built with **Avalonia UI** and .NET 10.

![ShareX Avalonia](https://getsharex.com/img/ShareX_Logo.png)
*(Note: Project is in active development)*

## âœ¨ Key Features
- **Cross-Platform**: Runs on Windows, Linux, and macOS (targeting).
- **Modern UI**: Reimagined interface inspired by modern design principles.
- **Powerful Capture**:
    - **Region Capture**: supports multi-monitor setups with crosshair cursor.
    - **Fullscreen** & **Window** capture modes.
- **macOS Support (MVP)**:
    - Screenshot service via native `screencapture`
    - Global hotkeys powered by SharpHook (requires Accessibility permission)
- **Advanced Annotation Tools**:
    - **17 Annotation Types**: Rectangle, Ellipse, Line, Arrow, Text, Number/Step, Blur, Pixelate, Magnify, Highlight, Freehand, SpeechBalloon, Image/Sticker, Spotlight, SmartEraser, Crop, plus base types
    - **Basic Shapes**: Rectangle, Ellipse, Line, Arrow, Text, Number/Step
    - **Effect Shapes**: Blur, Pixelate, Magnify, Highlight with real-time preview
    - **Freehand Tools**: Pen, Highlighter, Smart Eraser
    - **Advanced Shapes**: Speech Balloon, Image/Sticker insertion, Spotlight
    - **Object-based** selection, moving, resizing, and deletion
    - **Full Undo/Redo** support
    - **Keyboard Shortcuts**: V(Select), R(Rectangle), E(Ellipse), A(Arrow), L(Line), P(Pen), H(Highlighter), T(Text), B(Balloon), N(Number), C(Crop), M(Magnify), S(Spotlight), F(Effects)
- **Image Effects**:
    - **40+ Effects**: Auto-discovered from ImageEffects library (13 Adjustments, 17 Filters, 10 Manipulations, 6 Drawings)
    - **Categories**: Filters, Adjustments, Manipulations, Drawings
    - **Real-time Preview**: Apply effects with instant feedback
    - **Parameter Control**: Adjustable effect parameters via dynamic UI
- **Serialization**: Save and load annotations with full type support
- **Task Workflow**: Configurable after-capture tasks (Save, Copy, Upload)

## ðŸš€ Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Building and Running
```bash
# Clone the repository
git clone https://github.com/ShareX/ShareX.Avalonia.git
cd ShareX.Avalonia

# Build the solution
dotnet build

# Run the application
dotnet run --project src/ShareX.Avalonia.App/ShareX.Avalonia.App.csproj
```

### macOS Permissions (Screen Recording)
Screen capture on macOS requires Screen Recording permission:
1. Open **System Settings** > **Privacy & Security** > **Screen Recording**.
2. Enable ShareX.Avalonia for screen capture access.
3. Restart the app after granting permission.

### macOS Permissions (Global Hotkeys)
Global hotkeys use SharpHook and need Accessibility permission:
1. Open **System Settings** > **Privacy & Security** > **Accessibility**.
2. Enable ShareX.Avalonia (or the published app bundle) for accessibility access.
3. Restart the app and retest hotkeys.

## ðŸ› ï¸ Developer Information

See [DEVELOPER_README.md](docs/guides/DEVELOPER_README.md) for architecture details and contribution guidelines.

## ðŸ“„ License
GPL-3.0 (See LICENSE file)










