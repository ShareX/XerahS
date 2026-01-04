# ShareX.Avalonia

A cross-platform port of the popular **ShareX** screen capture and file sharing tool, built with **Avalonia UI** and .NET 9.

![ShareX Avalonia](https://getsharex.com/img/ShareX_Logo.png)
*(Note: Project is in active development)*

## ✨ Key Features
- **Cross-Platform**: Runs on Windows, Linux, and macOS (targeting).
- **Modern UI**: Reimagined interface inspired by modern design principles (SnapX and WinShot inspiration).
- **Powerful Capture**:
    - **Region Capture**: supports multi-monitor setups with crosshair cursor.
    - **Fullscreen** & **Window** capture modes.
- **Advanced Annotation Tools**:
    - **Basic Shapes**: Rectangle, Ellipse, Line, Arrow, Text, Number/Step
    - **Effect Shapes**: Blur, Pixelate, Magnify, Highlight with real-time preview
    - **Freehand Tools**: Pen, Highlighter, Smart Eraser
    - **Advanced Shapes**: Speech Balloon, Image/Sticker insertion, Spotlight
    - **Object-based** selection, moving, resizing, and deletion
    - **Full Undo/Redo** support
    - **Keyboard Shortcuts**: V(Select), R(Rectangle), E(Ellipse), A(Arrow), L(Line), P(Pen), H(Highlighter), T(Text), B(Balloon), N(Number), C(Crop), M(Magnify), S(Spotlight), F(Effects)
- **Image Effects**:
    - **50+ Effects**: Auto-discovered from ImageEffects library
    - **Categories**: Filters, Adjustments, Manipulations
    - **Real-time Preview**: Apply effects with instant feedback
    - **Parameter Control**: Adjustable effect parameters via dynamic UI
- **Serialization**: Save and load annotations with full type support
- **Task Workflow**: Configurable after-capture tasks (Save, Copy, Upload)

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

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

## 🛠️ Developer Information

See [DEVELOPER_README.md](docs/guides/DEVELOPER_README.md) for architecture details and contribution guidelines.

## 📄 License
GPL-3.0 (See LICENSE file)
