# Changelog

All notable changes to XerahS will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):
- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches


## Unreleased

## v0.15.6

### Fixes
- **Documentation**: Update FAQ to correctly reference XerahS instead of ShareX in Linux screen capture section `(699634f)`
- **Infrastructure**: Integrate update-changelog skill into maintenance-chores workflow `(5ade43b)`

## v0.15.5

### Features
- **Linux Capture**: Add Linux DBus fallbacks and KDE desktop permissions support `(290b3e0)`
- **Linux Capture**: Add Linux capture decision trace orchestration for debugging `(dc02dbd)`

### Fixes
- **Linux Capture**: Enforce portal-only sandbox capture policy `(2de4ac6)`
- **Linux Capture**: Unify Linux capture waterfall and add KDE DBus fallback `(c744059)`
- **Linux Capture**: Replace Console.WriteLine with DebugHelper.WriteLine in Linux capture stack `(a381faa)`
- **Builds**: Fix cross-platform build configuration and add linux-arm64 support `(ad8611c, 519423d)`

### Refactor
- **Linux Capture**: Split Linux runtime context detection components `(733a49d)`
- **Linux Capture**: Split Linux capture providers into parallel lanes `(5dd9931)`
- **Linux Capture**: Wire Linux capture through provider coordinator `(0a81693)`
- **Linux Capture**: Add modular Linux capture contracts and providers `(3569c0a)`

### Testing
- **Linux Capture**: Add Linux capture waterfall and lane matrix tests `(7f49769)`

### Documentation
- **Build System**: Rename developer README and add Linux guide `(717be27)`
- **Roadmap**: Finalize Linux phase roadmap and release gate `(76df673)`

## v0.15.0

### Features
- **Mobile**: Add Android MVP with Share-to-XerahS upload support `(8746372)`
- **Mobile**: Add iOS MVP with Share Extension support `(03698c6)`
- **Mobile**: Add Custom Uploader configuration UI for mobile `(#124, @Hexeption)`
- **Mobile**: Redesign Amazon S3 configuration UI to match Custom Uploaders `(#125, @Hexeption)`
- **Mobile**: Add mobile-friendly Amazon S3 configuration and settings navigation `(78a488e)`
- **Mobile**: Add .NET MAUI mobile app project to solution `(493d147)`
- **Indexer**: Implement async streaming indexer with progress and cancellation `(8b2fe88)`

### Fixes
- **Image Editor**: Share annotation preview visuals with ImageEditor to ensure consistency `(cc074ad)`
- **Annotations**: Remove draw-start dot artifact and align arrow preview `(d1afa2f)`
- **Annotations**: Optimize overlay drawing responsiveness `(faa84e7)`
- **Region Capture**: Optimize annotation rendering performance `(891eed0)`
- **Workflow**: Complete WorkflowType end-to-end wiring `(47ead0b)`
- **UX**: Hide SilentRun window on first open instead of minimizing `(7567223)`
- **Updates**: Gracefully handle repositories with only pre-releases `(ed68066)`
- **After Capture**: Persist and preserve "Show after capture window" behavior across repeated runs `(9a04c9d, a3a581d, a8262d4)`
- **Upload**: Add multi-uploader auto destination fallback and avoid retrying failed instances `(72079e6, c06f17f, a576e78)`
- **Mobile Upload**: Wire Amazon S3 and plugin integration to InstanceManager upload pipeline `(44c316b, 02087fb)`
- **Watch Folder**: Convert MOV captures to MP4 `(27f6fec)`
- **Settings**: Make backup and secrets filenames machine-specific `(c618542, 55a32d0)`
- **Amazon S3**: Reorder and renumber setup steps `(3196b02)`
- **iOS**: Improve local signing setup and share extension flow `(30f6822)`

### Build
- **Plugins**: Centralize plugin copy target and pass host TFM `(6bfa2e1)`
- **Dependencies**: Bump Avalonia packages to 11.3.12 `(27ce502)`
- **ImageEditor**: Keep submodule attached to develop branch and enforce latest `(5e8eee0, e03ec12)`
- **ImageEditor**: Update submodule for theme-aware editor view `(71601ee)`
- **ImageEditor**: Update submodule for ShareX net9 compatibility and track `develop` `(a17d91e, 493d147)`

### Documentation
- **Audits**: Organized audit files and added unused enums analysis report `(e3d2a9c)`
- **Audits**: Update UI control audit inventory snapshots `(aadfea4)`
- **Tasks**: Mark XIP0030 complete and move it to completed tasks `(25a83a1)`

## v0.14.0

### Features
- **Monitor Test**: Implement MonitorTest workflow with diagnostic and pattern testing modes `(56a1ea3, 1dc10f8)`
- **Tools**: Add Ruler workflow routing and placeholder service `(5647b4d)`
- **Tools**: Implement Ruler workflow with full RegionCapture integration `(8ea9419)`
- **Indexer**: Make Index Folder open in its own window `(8b20b3b, v0.14.2)`
- **Editor**: Integrate upstream ShareX.ImageEditor submodule `(0db2c71, v0.14.3)`
- **Editor**: Add File Open choice dialog for replace or shape insert `(1a41df5, v0.14.4)`
- **Region Capture**: Add annotation options persistence for RegionCapture `(7e82df3, v0.14.5)`

### Fixes
- **Logging**: Fix duplicate date in log filename on date rotation `(69cb3c2, v0.14.1)`
- **Region Capture**: Improve RegionCapture annotation toolbar integration `(4500b8a, v0.14.3)`
- **Region Capture**: Reduce annotation rebuild pressure `(3bf8224, v0.14.3)`
- **Indexer**: Enable Open in Browser button after indexing `(4582529, v0.14.3)`
- **Indexer**: Remove WebView and open HTML in system browser `(16945a0, v0.14.3)`
- **Navigation**: Enable menu navigation and update editor data transfer APIs `(49772bf, v0.14.3)`
- **Editor**: Sync ImageEditor fixes and add comparison report `(3ee199a, v0.14.3)`
- **Packaging**: Restore macOS icon in Windows package build `(ba40fbb, v0.14.3)`
- **ImageEditor**: Update submodule to unified Core undo-redo `(240649d, v0.14.3)`
- **Upload**: Delay upload progress title update until actual upload starts `(9d4894b, v0.14.4)`
- **macOS**: Harden mac packaging and cross-platform editor wiring `(6e1d569, v0.14.4)`
- **Dialogs**: Prevent File Open dialog crash and add global exception logging `(5cbf5dd, v0.14.4)`
- **Editor**: Persist region annotation options and track ImageEditor main `(2cc8fa7, v0.14.5)`
- **Editor**: Refactor MonitorTest and Ruler platform abstractions `(554099c, v0.14.5)`
- **Editor**: Update editor drag drop integration and task status `(79eb2be, v0.14.5)`
- **Editor**: Enable Zoom to Fit menu command and shortcut `(e5ffef7, v0.14.5)`
- **ImageEditor**: Update submodule with smart padding crop sync, clipboard fixes, z-order fixes, and double-dispose bug fixes `(b3125b8, 0ee0ad7, 4eb30bf, 0c2b53e, 1131223, 751eb7c, v0.14.5)`

### Build
- **Cross-Compilation**: Add cross-compilation support for macOS from Windows `(a2bf5a6, v0.14.3)`
- **Documentation**: Add build system documentation and update ISS paths `(19b3a84, v0.14.3)`
- **Infrastructure**: Fix version parsing in Windows package script `(5069a01, v0.14.4)`

## v0.13.0

### Fixes
- **Menu Bar**: Fix menu-bar hash checker routing and dynamic workflows menu `(8068e6f)`
- **Upload**: Improve Upload Content workflow handling and window UX `(62a1cda)`
- **Upload**: Improve Upload nav workflow fallback and text upload routing `(4fd8182)`

## v0.12.0

### Fixes
- **Tools**: Added media tools to Tools navigation bar `(485a438)`
- **DataTemplates**: Fix DataTemplate issues `(485a438)`
- **Proxy**: Fix custom uploader loading and add configuration UI `(#77)` ([@Hexeption](https://github.com/Hexeption))
- **Linux**: Add dark mode support and theme settings `(#62)` ([@unicxrn](https://github.com/unicxrn))
- **macOS**: Add native application menu `(#60)` ([@Hexeption](https://github.com/Hexeption))
- **Custom Uploaders**: Fix compatibility improvements `(#74)` ([@Hexeption](https://github.com/Hexeption))
- **Linux**: Wayland Hyprland screenshot support `(#61)` ([@unicxrn](https://github.com/unicxrn))
- **Security**: Fix DPAPI platform warning `(#73)` ([@Hexeption](https://github.com/Hexeption))
- **Custom Uploaders**: Fix version compatibility `(#71)` ([@emmsixx](https://github.com/emmsixx))

### Refactor
- **Editor**: Renamed namespace from ShareX.Editor to XerahS.Editor for consistency `(25135d0, d0d1266)`
- **Project Structure**: Updated all editor references and AXAML declarations `(1dfeb3b)`

### Build
- **Plugins**: Improved plugin copy target to only include plugin assemblies `(a9b5c63)`
- **Configuration**: Updated build files and packaging configuration `(09222cc)`
- **Infrastructure**: Added GitHub issue templates `(5c03c33)` and bug report configuration `(b107da9)`
- **Git**: Updated .gitignore `(789ec93)`

## v0.11.0

### Features
- **Upload**: Implement UploadContentWindow and remove superseded upload WorkflowTypes `(298457a)`

## v0.10.0

### Features
- **Workflows**: Implement AutoCapture workflows `(a45d02f)`

## v0.9.0

### Features
- **Workflows**: Implement Pin to Screen workflows `(1e0d3f2)`
- **Amazon S3**: Enhance SSO with region selection `(6880866)`

### Fixes
- **Upload**: Improve upload error surfacing and history actions `(760a6ef)`
- **Workflows**: Preserve workflow order and exclude None `(6c08b22)`
- **Custom Uploaders**: Fix compatibility check for XerahS versions `(422710a)`

### Build
- **Plugins**: Restore plugin DLL deduplication with retry logic `(81db32e)`

### Core
- **Rendering**: Remove RectangleLight; modern Skia rendering deprecated it `(12d3ae5)`

## v0.8.0

### Features
- **Security**: Add cross-platform secrets store `(c2b8105)`
- **Upload**: Add auto destination uploader `(f3abe81)`
- **Diagnostics**: Add secret store diagnostics `(f626f09)`
- **Custom Uploaders**: Implemented full support for Custom Uploaders including editor UI and integration `(5962870, 8020d73)`
- **Task Settings**: Redesigned Task Settings UX with dedicated Image/Video tabs `(43436af)`
- **Tray Icon**: Added recording-aware tray icon with pause/abort controls `(7d22818)`
- **Image Formats**: Added AVIF and WebP image format support `(3b89381)`
- **Linux/Wayland**: Fix screen capture on Wayland by integrating XDG Portal API `(4cc5a9f)`

### Fixes
- **Capture**: Allow clipboard payloads in capture phase `(a2e336f)`
- **Upload**: Add clipboard upload auto routing `(6527590)`
- **Region Capture**: Correct crop offset and refresh AfterCapture UI `(c5efeab)`
- **Region Capture**: Fix coordinate mapping for Windows `(#29)`
- Fix Linux active window capture hierarchy and coordinates `(2957c89, 007f261)`
- Fix Linux hotkey initialization and Region Capture `(73dd95d, e8a9cc8)`
- Hide main window when capture triggered from tray/navbar `(45264fb)`
- Fix update dialog layout `(7868256)`

### Refactor
- **Editor**: Update XerahS.Editor.csproj references `(1dfeb3b)`
- **Docs**: Rename editor docs references to XerahS.Editor `(90b9fe0)`


## v0.7.0 - Annotation Overlays & Packaging

### Features & Improvements
- **Annotations**: Enabled Annotation Toolbar in Region Capture Overlay `(05dcaf3)`
- **Region Capture**: Added support for transparent background capture (RectangleTransparent) `(9ee7277)`
- **macOS**: Native single-file app bundle packaging (`.app`) `(c2b882c)`
- **Packaging**: Automated multi-arch Windows release builds `(49a7ec6)`
- **Plugins**: Support for user-installed plugins and packaging `(e787536)`
- **Window Capture**: Added support via monitor cropping fallback `(d73daf5)`
- **Annotations**: Refactor Annotation Toolbar `(#53)`
- **Media Library**: Basic implementation `(#49)`

### Bug Fixes
- Fix annotation layer coordinate system for multi-monitor and high DPI `(5d69425, 61bd0c9)`
- Fix annotation layer compositing `(3875298)`
- Global exception handling implementation `(ad6d443)`
- **Screen**: Fix frozen screen issue `(#51)`
- **Cursor**: Fix system cursor issues `(#46)`

## v0.6.0 - UI Redesign & Auto-Update

### Features & Improvements
- **UI Redesign**: Comprehensive visual overhaul of strictly all views (Settings, History, Tools) using Grid layout and consistent styling `(34f4cbf, d390fa7)`
- **Auto-Update**: Implemented auto-update system with Avalonia UI `(54b9546)`
- **After Upload**: Added "After Upload" results window `(18a3ab7)`
- **Property Grid**: Added ApplicationConfig property grid `(c4d20bf)`
- **CLI**: Added `verify-recording` command for automated screen recording validation `(732e173)`
- **Editor**: Unified editor undo history across different toolsets `(24ad021)`
- **Architecture**: Moved Windows-specific P/Invoke types to dedicated Platform.Windows project `(90da89a)`
- **FFmpeg**: Improved FFmpeg download/config UX with progress hooks and better path resolution `(1646cbb, 7677ceb, b4fdcbf)`
- **Documentation**: Replace ShareX.Avalonia references with XerahS `(#44)`
- **Workflow**: Update cursor handling `(#43)`

### Bug Fixes
- Improve GIF recording quality and added clipboard support `(1baecc0, 4148e49)`
- Add pause and stroke-based abort for recordings `(c3d04a7)`
- Fix "After Upload" window theming and errors `(9b752c0, 6dfe81e)`
- Fix speech balloon tail geometry rendering issues `(784594e)`
- Fix system cursor appearing in region screenshots on some configurations `(85a4e2f)`
- Fix region capture hotkey issues `(#38, #39)`

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

- **v0.14.0**: Monitor Test and Ruler workflows, ShareX.ImageEditor submodule integration, indexer improvements, cross-compilation support
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
