# XerahS

A cross-platform port of the popular **ShareX** screen capture and file sharing tool, built with **Avalonia UI** and .NET 10.

![ShareX Avalonia](https://getsharex.com/xerahs/img/XerahS_Logo.png)
*(Note: Project is in active development)*

## ⚠️ About This Project

**XerahS is developed using agentic coding workflows.** This project embraces AI-assisted development as a first-class engineering practice, leveraging tools like GitHub Copilot, Claude, and other AI agents to accelerate feature development, refactoring, and code quality improvements. The codebase is architected with bleeding-edge technologies (.NET 10, Avalonia 11.3+) and prioritizes patterns that maximize AI comprehension: strict nullability, exhaustive documentation, and standardized MVVM architecture.

**XerahS is and will always be free**, just like ShareX, built out of passion, not for profit. It's developed entirely through agentic coding as a parallel project to ShareX, designed to align with different user preferences and values. You're welcome to give it a try or stick with whatever tool works best for you.

**If agentic coding is not your style**, we encourage you to try the original [**ShareX**](https://github.com/ShareX/ShareX) for Windows, which is developed using traditional methods and has a mature, battle-tested codebase backed by years of community contributions.

For developers interested in AI-first development and cross-platform experimentation, XerahS offers a modern foundation built for the future of software engineering.

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

### Arch Linux (AUR)
Arch Linux users can install the latest development version via the community-maintained AUR package [`xerahs-git`](https://aur.archlinux.org/packages/xerahs-git) (maintained by @unicxrn).

This package builds directly from the source code and automatically handles dependencies, including the .NET 10 SDK. It can be installed using an AUR helper like `yay`:
```bash
yay -S xerahs-git
```

### macOS Permissions (Screen Recording)
Screen capture on macOS requires Screen Recording permission:
1. Open **System Settings** > **Privacy & Security** > **Screen Recording**.
2. Enable XerahS for screen capture access.
3. Restart the app after granting permission.

### macOS Permissions (Global Hotkeys)
Global hotkeys use SharpHook and need Accessibility permission:
1. Open **System Settings** > **Privacy & Security** > **Accessibility**.
2. Enable XerahS (or the published app bundle) for accessibility access.
3. Restart the app and retest hotkeys.

### macOS Troubleshooting ("App is damaged")
If you see a message saying **"XerahS is damaged and can't be opened"**, it is due to macOS security (Gatekeeper) on quarantined downloads. To fix it:

1. Open **Terminal**.
2. Type the following command (do not hit Enter yet):
   ```bash
   xattr -cr 
   ```
3. Drag the **XerahS.app** file from Finder into the Terminal window (this pastes the full path).
4. Only now, press **Enter**.

## 🛠️ Developer Information

See [DEVELOPER_README.md](DEVELOPER_README.md) for architecture details and contribution guidelines.

## 📄 [License](LICENSE.txt)









