# Changelog

All notable changes to XerahS (XerahS) will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):
- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches

## Unreleased

## v0.7.7 - Editor Namespace & Plugin Build Fixes

### Features & Improvements
- **Editor**: Renamed namespace from ShareX.Editor to XerahS.Editor for consistency `(25135d0, d0d1266)`
- **Build System**: Improved plugin copy target to only include plugin assemblies `(a9b5c63)`
- **Project Structure**: Updated all editor references and AXAML declarations `(1dfeb3b, 90b9fe0)`

### Bug Fixes
- Fix Font Awesome font loading error (`InvalidOperationException`) by correcting avares:// resource paths `(25135d0)`
- Fix MSB3030 plugin build errors where dependencies were looked up in wrong target framework path `(a9b5c63)`
- Remove OutputPath override from App's BuildPlugins target to prevent dependency resolution issues `(a9b5c63)`

## v0.7.5 - Linux Improvements & Custom Uploaders

### Features & Improvements
- **Custom Uploaders**: Implemented full support for Custom Uploaders including editor UI and integration `(5962870, 8020d73)`
- **Task Settings**: Redesigned Task Settings UX with dedicated Image/Video tabs `(43436af)`
- **Tray Icon**: Added recording-aware tray icon with pause/abort controls `(7d22818)`
- **Image Formats**: Added AVIF and WebP image format support `(3b89381)`
- **Linux/Wayland**: Fix screen capture on Wayland by integrating XDG Portal API `(4cc5a9f)`

### Bug Fixes
- Fix Linux active window capture hierarchy and coordinates `(2957c89, 007f261)`
- Fix Linux hotkey initialization and Region Capture `(73dd95d, e8a9cc8)`
- Hide main window when capture triggered from tray/navbar `(45264fb)`
- Fix update dialog layout `(7868256)`

## v0.7.0 - Annotation Overlays & Packaging

### Features & Improvements
- **Annotations**: Enabled Annotation Toolbar in Region Capture Overlay `(05dcaf3)`
- **Region Capture**: Added support for transparent background capture (RectangleTransparent) `(9ee7277)`
- **macOS**: Native single-file app bundle packaging (`.app`) `(c2b882c)`
- **Packaging**: Automated multi-arch Windows release builds `(49a7ec6)`
- **Plugins**: Support for user-installed plugins and packaging `(e787536)`
- **Window Capture**: Added support via monitor cropping fallback `(d73daf5)`

### Bug Fixes
- Fix annotation layer coordinate system for multi-monitor and high DPI `(5d69425, 61bd0c9)`
- Fix annotation layer compositing `(3875298)`
- Global exception handling implementation `(ad6d443)`

## v0.6.0 - UI Redesign & Auto-Update

### Features & Improvements
- **UI Redesign**: Comprehensive visual overhaul of strictly all views (Settings, History, Tools) using Grid layout and consistent styling `(34f4cbf, d390fa7)`
- **Auto-Update**: Implemented auto-update system with Avalonia UI `(54b9546)`
- **After Upload**: Added "After Upload" results window `(18a3ab7)`
- **Property Grid**: Added ApplicationConfig property grid `(c4d20bf)`

### Bug Fixes
- Improve GIF recording quality and added clipboard support `(1baecc0, 4148e49)`
- Add pause and stroke-based abort for recordings `(c3d04a7)`
- Fix "After Upload" window theming and errors `(9b752c0, 6dfe81e)`

## v0.5.1 - Verify Recording CLI & Editor Improvements

### Features & Improvements
- **CLI**: Added `verify-recording` command for automated screen recording validation `(732e173)`
- **Editor**: Unified editor undo history across different toolsets `(24ad021)`
- **Architecture**: Moved Windows-specific P/Invoke types to dedicated Platform.Windows project `(90da89a)`
- **FFmpeg**: Improved FFmpeg download/config UX with progress hooks and better path resolution `(1646cbb, 7677ceb, b4fdcbf)`

### Bug Fixes
- Fix speech balloon tail geometry rendering issues `(784594e)`
- Fix system cursor appearing in region screenshots on some configurations `(85a4e2f)`

## v0.5.0 - Core Capture & Editor Improvements

### Features & Improvements
- **Capture**: Added single instance enforcement for the application `(aacb23b)`
- **Region Capture**: Enhanced crosshair visibility and added magnifier pixel sampling from background `(a838ae1, 56aa4de)`
- **Region Capture**: Hide system cursor when ghost cursor is active `(d338b32)`
- **Editor**: Wire ImageEffectsViewModel to unified undo/redo stack `(81a3815)`
- **UX**: Set default file picker location to Desktop for easier access `(f5083e3)`

### Bug Fixes
- Fix 11+ HIGH/MEDIUM priority issues including null safety and resource management `(9188a22, 1f9a74f)`
- Set RegionCaptureControl cursor to None to prevent double cursor visibility `(fe35424)`

## v0.4.0 - Image Effects & Tools

### Features & Improvements
- **Image Effects**: Refactored preset management and improved effects UI `(154a6c9, 5d9dbd7, ee47e3d)`
- **Tools**: Added QR code generator/decoder tools `(66bd61b)`
- **Tools**: Added Color Picker tools with standard color name mapping `(bdb22f8, 0b50328)`
- **Watch Folders**: Implemented Watch Folder system with per-folder workflow assignments `(49e838d, 63124f6, 951e034)`
- **Indexer**: Added Index Folder preview and modernized HTML output using WebView `(63ca369, 3f3a751, e57932e)`
- **macOS**: Added native ScreenCaptureKit video recording support `(fd75640)`

### Bug Fixes
- Fix cursor tracking and visibility during GDI capture `(f6973f6, e0a056b, 265a96a)`

## v0.3.1 - Critical Bug Fixes

### Bug Fixes
- **Capture**: Fix NullReferenceException in DXGI capture by preventing premature disposal of D3D11 device context `(df9bd33)`

## v0.3.0 - Modern Capture Architecture

### Features & Improvements
- **Modern Capture**: Implemented DXGI-based high-performance screen capture for Windows `(1440efc, 25f544d)`
- **Screen Recording**: Unified recording pipeline with Windows Media Foundation and FFmpeg support `(9224b62, 7a6e47b, 8fc451c)`
- **Workflow System**: Major overhaul of hotkeys into a full Workflow system with GUID persistence `(faebe87, 09f1e35)`
- **Toast Notifications**: New custom Avalonia-based notification system with advanced settings `(6229154, f1d9b88)`
- **Linux**: Initial support for Wayland via XDG Desktop Portal `(3573ad1, f7a103c, b92fb89)`
- **Linux**: Native X11 capture path implementation `(7ccd5d9)`
- **Settings**: Added weekly backup system for application settings `(0a8e15f)`
- **UX**: Added tray icon support with customizable click actions `(035e8b4, 4ddfb59)`

### Bug Fixes
- Fix multi-monitor blank capture issues in modern capture path `(52ae45e)`
- Fix DPI handling and coordinate mapping in region capture `(e4817b1, 954dee3)`
- Massive code audit: fixed 500+ license headers and 160+ nullability issues `(dca9217, dd90761)`

## v0.2.1 - Multi-Monitor & DPI Polish

### Bug Fixes
- Fix region capture offsets and scaling issues on multi-monitor setups `(e47e81b)`
- Standardized Windows TFM and fixed CsWinRT interop issues `(2f44742, 4e88d23)`

## v0.2.0 - macOS Support & Plugin System

### Features & Improvements
- **macOS**: Initial platform support including ScreenCaptureKit, SharpHook hotkeys, and app bundling `(acba9d5, ca05d4b, 6fbf63e)`
- **Plugins**: Implemented dynamic plugin system with packaging (`.sxap`) and CLI tools `(f81c656, a2adbf3, e787536)`
- **History**: Switched history storage from XML to SQLite with automatic backups `(22b6cf5, 0f20d76)`
- **Editor**: Integrated ShareX.Editor as a core component with SkiaSharp rendering `(57bfe32, 90b5871)`
- **Integration**: Added `.sxadp` file association for plugin packages `(df9bbd1)`

## v0.1.0 - Initial Feature Set

### Core Features
- **UI**: Reimagined interface with two-toolbar system and modern dark theme `(c0bad1e, 231e4df)`
- **Capture**: Region, Fullscreen, and Window capture modes `(4839944)`
- **Annotations**: Object-based editor with Rectangle, Ellipse, Arrow, Line, Text, Number, and Crop tools `(bd1153c, 9b6cfe0)`
- **Annotations**: Full Undo/Redo support and object manipulation `(9ecd720, cb7b54a)`
- **Hotkeys**: Global hotkey system with Win32 registration `(80cd222)`
- **Image Effects**: Initial implementation of 40+ effects including Resize, Shadows, and Gradients `(0840cef, 6777d86)`
- **History**: Basic task history tracking `(9c1c2f8)`

---

## Version Summary

- **v0.7.5**: Custom Uploaders, Task Settings redesign, Linux/Wayland fixes, WebP/AVIF support
- **v0.7.0**: Annotation overlays in region capture, transparent capture, macOS .app bundles
- **v0.6.0**: Complete UI Redesign, Auto-Update system, After Upload window
- **v0.5.1**: verify-recording CLI, editor improvements
- **v0.5.0**: Core capture fixes, region capture magnifier
- **v0.4.0**: Image effects, QR codes, Color picker, Watch folders
- **v0.3.1**: DXGI capture crash fix
- **v0.3.0**: Modern capture architecture, screen recording, workflow system, toast notifications
- **v0.2.0**: macOS support, plugin system, SQLite history, editor extraction
- **v0.1.0**: Initial implementation

---

*This changelog follows Semantic Versioning while the project remains in pre-release (0.x.x).*
