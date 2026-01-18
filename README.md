# ShareX.Avalonia

A cross-platform port of the popular **ShareX** screen capture and file sharing tool, built with **Avalonia UI** and .NET 10.

![ShareX Avalonia](https://getsharex.com/img/XerahS_Logo.png)
*(Note: Project is in active development)*

## ✨ Key Features
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
- **Workflows (Zero Inheritance)**:
    - **Unique Settings**: Each workflow is fully independent with its own hotkeys and tasks.
    - **No "Default" Inheritance**: Reduces configuration errors by avoiding complex inheritance chains.
    - **Task Workflow**: Configurable after-capture tasks (Save, Copy, Upload, Image Effects)
- **Modern Capture Architecture**:
    - **Windows**: Uses fast `Desktop Duplication API` (DXGI) for high-performance capture.
    - **macOS**: Leverages native `ScreenCaptureKit` for performant, permission-compliant recording.
    - **Linux**: X11 and Wayland support.
- **Image Editor**:
    - **Hardware Accelerated**: Fully GPU-accelerated rendering using Skia/Metal/Direct2D. renders 4K+ images at 60FPS.
    - **40+ Effects**: Organized into Adjustments, Filters, Manipulations, and Drawings.
    - **Real-time Preview**: Apply effects with instant visual feedback.
    - **Serialization**: Save and load annotations with full type support.

## 🚀 Getting Started

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
dotnet run --project src/XerahS.App/XerahS.App.csproj
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

## 🛠️ Developer Information

See [DEVELOPER_README.md](docs/guides/DEVELOPER_README.md) for architecture details and contribution guidelines.

## 📄 [License](LICENSE.txt)









