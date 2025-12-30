# ShareX.Avalonia

A cross-platform port of the popular **ShareX** screen capture and file sharing tool, built with **Avalonia UI** and .NET 8.

![ShareX Avalonia](https://sharex.com/img/sharex_256.png)
*(Note: Project is in active development)*

## ✨ Key Features
- **Cross-Platform**: Runs on Windows, Linux, and macOS (targeting).
- **Modern UI**: Reimagined interface inspired by modern design principles (WinShot inspiration).
- **Powerful Capture**:
    - **Region Capture**: supports multi-monitor setups.
    - **Fullscreen** & **Window** capture modes.
- **Annotation Tools**:
    - Rectangle, Ellipse, Line, Arrow, Text.
    - Customizable colors and stroke widths.
    - Object-based selection, moving, and deletion.
    - Full Undo/Redo support.
- **Task Workflow**: Configurable after-capture tasks (Save, Copy, Upload).

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
dotnet run --project src/ShareX.Avalonia.UI/ShareX.Avalonia.UI.csproj
```

## 🛠️ Developer Information

See [DEVELOPER_README.md](DEVELOPER_README.md) for architecture details and contribution guidelines.

## 📄 License
GPL-3.0 (See LICENSE file)
