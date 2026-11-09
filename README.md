# XerahS

[![XerahS Health](https://oss-health-monitor.vercel.app/api/badge/ShareX/XerahS)](https://github.com/volotat/OSS-Health-Monitor)

A cross-platform port of the popular **ShareX** screen capture and file sharing tool, built with **Avalonia UI** and .NET 10.

![ShareX Avalonia](https://xerahs.com/assets/Logo.png)
*(Note: Project is in active development)*

## ⚠️ About This Project

**XerahS is developed using agentic coding workflows.** This project embraces AI-assisted development as a first-class engineering practice, leveraging tools like GitHub Copilot, Claude, and other AI agents to accelerate feature development, refactoring, and code quality improvements. The codebase is architected with bleeding-edge technologies (.NET 10, Avalonia 11.3+) and prioritizes patterns that maximize AI comprehension: strict nullability, exhaustive documentation, and standardized MVVM architecture.

**XerahS is and will always be free**, just like ShareX, built out of passion, not for profit. It's developed entirely through agentic coding as a parallel project to ShareX, designed to align with different user preferences and values. You're welcome to give it a try or stick with whatever tool works best for you.

**If agentic coding is not your style**, we encourage you to try the original [**ShareX**](https://github.com/ShareX/ShareX) 21 or earlier for Windows, which is developed using traditional methods and has a mature, battle-tested codebase backed by years of community contributions.

**Built by the community, for the community.** XerahS is a collaborative effort driven by passionate contributors who believe in open-source development. Every feature, every improvement, and every line of code represents a shared vision of making powerful screen capture tools accessible to everyone, everywhere.

For developers interested in AI-first development and cross-platform experimentation, XerahS offers a modern foundation built for the future of software engineering.

## ✨ Key Features
- **Cross-Platform Desktop**: Runs on Windows, Linux, and macOS (targeting).
- **Mobile Support (Experimental)**: .NET MAUI implementation for **Android** and **iOS** with file upload, share extension, and uploader configuration.
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

## 📱 Mobile (Experimental)

XerahS now includes an experimental **.NET MAUI** mobile implementation for Android and iOS, bringing ShareX's powerful upload capabilities to mobile devices.

### Mobile Features
- **File Upload**: Upload images, videos, and files from your device
- **Share Extension (iOS)**: Share content directly from other apps into XerahS
- **Uploader Support**: Configure Amazon S3 and Custom Uploaders
- **Settings Sync**: Mobile-optimized settings interface

### Building Mobile

#### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Android**: Android SDK API 21+ with emulator or physical device
- **iOS**: Xcode 16+ (macOS required), iOS 15.0+ target

#### Build & Run
```bash
# Android
dotnet build src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-android
dotnet run --project src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-android

# iOS (macOS only)
dotnet build src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-ios
dotnet run --project src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net10.0-ios
```

### Project Structure
- `XerahS.Mobile.Maui` - Main MAUI application (Android/iOS)
- `XerahS.Mobile.UI` - Shared mobile UI components
- `XerahS.Mobile.Android` - Android-specific platform code
- `XerahS.Mobile.iOS` - iOS-specific platform code
- `XerahS.Mobile.iOS.ShareExtension` - iOS Share Extension for receiving shared content

---

## 🚀 Getting Started

### Install (start here)

Pick your system, install, and launch XerahS. The first-run wizard checks your setup and ends with a first capture.

| System | Easiest install | Notes |
|---|---|---|
| **Windows** | `XerahS-<version>-win-x64.exe` (or `.msi`, or the `-portable.zip`) from the [latest release](https://github.com/ShareX/XerahS/releases/latest) | Use `win-arm64` on ARM PCs. Closest to the full ShareX feature set. |
| **Linux** | `XerahS-<version>-linux-x64.AppImage` (any distro), `.deb`, `.rpm` or `.flatpak` from the same page, or [Flatpak via FlatPark](#flatpak-flatpark), or [`xerahs-git` on the AUR](#arch-linux-aur) | Screenshots are the most reliable part. Screen recording on Wayland is still rough. See [Platform status](#platform-status). |
| **macOS** | `XerahS-<version>-mac-arm64.tar.gz` (Apple silicon) or `mac-x64.tar.gz` (Intel) | Allow Screen Recording and Accessibility, see [macOS permissions](#macos-permissions-screen-recording). |

Not sure which file you need? Take the `.exe` on Windows, the `.AppImage` on Linux and the `arm64` archive on a recent Mac.

### Platform status

XerahS is pre-1.0 and the platforms are not equally finished. This is the honest picture.

| | Screenshots | Screen recording | Overall |
|---|---|---|---|
| **Windows** | Region, window and full screen on multiple monitors | Native GPU recorder with audio, pause and timer | Closest to ShareX; expect pre-1.0 rough edges |
| **macOS** | Native ScreenCaptureKit | Works; needs permissions | Usable; some packaging and colour issues remain |
| **Linux** | X11 and Wayland (portal) | Most work in progress, an intermittent Wayland stall is not fixed | Fine for screenshots; keep another recorder for video for now |

The last step of the first-run wizard checks your capture folder, hotkeys and permissions and tells you what to fix. If XerahS cannot start, it now shows a message with the path of its log file.

### Update channels

| Channel | Repository | Use it for |
|---|---|---|
| Upstream releases | [ShareX/XerahS releases](https://github.com/ShareX/XerahS/releases) | Official release builds. |
| KovaForge development builds | [KovaForge/XerahS releases](https://github.com/KovaForge/XerahS/releases) | This fork's newer fixes; versions can differ from upstream. |

These are two repositories with separate release histories, so a larger version number in the fork does not mean upstream is out of date. In **Settings > Application > Updates**, `Release` checks `ShareX/XerahS`. `PreRelease` checks the selected source (KovaForge by default); you can also choose ShareX, both repositories, or a custom source. Committing a fix does not publish a downloadable release.

### Linux distro repositories
A Launchpad PPA, Fedora COPR and openSUSE OBS channel are planned but **not live yet**, so `add-apt-repository` and `dnf copr enable` will not find XerahS. Until they are published, use the `.deb`, `.rpm` or `.AppImage` from the [latest release](https://github.com/ShareX/XerahS/releases/latest), Flatpak or the AUR. The plan and the maintainer steps are in [docs/linux/distro-repos.md](docs/linux/distro-repos.md).

### Arch Linux (AUR)
Arch Linux users can install the latest development version via the community-maintained AUR package [`xerahs-git`](https://aur.archlinux.org/packages/xerahs-git) (maintained by @unicxrn).

The fork also maintains a [repository-owned Arch recipe](docs/linux/aur.md) and packaging CI.
The public AUR listing is separate; its ownership and recipe must be updated by its maintainer.

This package builds directly from the source code and automatically handles dependencies, including the .NET 10 SDK. It can be installed using an AUR helper like `yay`:
```bash
yay -S xerahs-git
```

### Flatpak (FlatPark)
Any Flatpak-capable distro can install XerahS from the community-maintained [FlatPark](https://flatpark.org/apps/com.xerahs.XerahS) repository (maintained by @jing2uo). It downloads the official `XerahS-<version>-linux-x64.tar.gz` release unmodified at install time and updates automatically with new releases:
```bash
flatpak remote-add --if-not-exists flatpark https://dl.flatpark.org/flatpark.flatpakrepo
flatpak install flatpark com.xerahs.XerahS
```

Screen recording and GIF creation need `ffmpeg`, which the Flatpak runtime does not ship. See [docs/linux/flatpak-permissions.md](docs/linux/flatpak-permissions.md) for the sandbox permissions XerahS expects.

### macOS Permissions (Screen Recording)
Screen capture on macOS requires Screen Recording permission:
1. Open **System Settings** > **Privacy & Security** > **Screen Recording**.
2. Enable XerahS for screen capture access.
3. Restart the app after granting permission.

### macOS Permissions (Global Hotkeys)
Global hotkeys use SharpHook and need Accessibility permission:
1. Open **System Settings** > **Privacy & Security** > **Accessibility**. On macOS 27 and later this pane is named **Device Control and Data Access**.
2. Enable XerahS (or the published app bundle) in that list.
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

## Build from source

### Cloning the Repository
XerahS requires the `ShareX.ImageEditor` and `Omacut` submodules. `Omacut` is the [KovaForge fork of omacut](https://github.com/KovaForge/omacut): a native Avalonia video trimmer that powers the video editor. Clone with:
```bash
git clone --recursive https://github.com/KovaForge/XerahS.git
```

If you already cloned without `--recursive`, initialize the submodules:
```bash
cd XerahS
git submodule update --init --recursive
```

### Desktop Quick Start (Windows)
On Windows, the fastest verified first-run path is:
```powershell
.\init-submodules.ps1
dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false
```

If you skip submodule initialization and run `dotnet build src/desktop/XerahS.sln` immediately after a plain `git clone`, the build fails with `MSB3202` because `ShareX.ImageEditor` and `Omacut` are checked out as Git submodules.

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- FFmpeg (`ffmpeg` and `ffprobe`) for recording and the video editor. XerahS can download it for you.

## 🛠️ Developer Information

See [XerahS Developer Guide](developers/README.md) for architecture details and contribution guidelines.

## 📄 [License](LICENSE.txt)








