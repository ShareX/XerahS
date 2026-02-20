# Changelog

All notable changes to XerahS will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):
- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches


## Unreleased

## v0.16.1

### Fixes
- **ImageEditor**: Optimize Black & White filter, Smart Eraser, and canvas zooming performance

## v0.16.0

### Features
- **Media Explorer**: Implement provider file browsing with S3 and Imgur support, including navigation, search, filtering, and CDN thumbnail optimization `(9deedf9, e374160)`
- **Watch Folder**: Add watch-folder daemon with lifecycle hooks, runtime policy controls, and tests `(79c1292, 2b94600, 4265528, 992c41b)`
- **Mobile**: Add adaptive theming infrastructure with native styling polish `(4b79ddb, a7cfb22, 1e5f9eb, 30bbe98)`
- **Mobile**: Add upload queue, picker, and history screens `(68d97d9, 52d6ad2)`
- **UI**: Add Copy Errors to UI (HistoryView, AfterUploadWindow, Toast) `(5c08812)`

### Fixes
- **Scrolling Capture**: Improve auto-scroll behavior and workflow settings integration `(1fa45f2, 971219c, 8ac2c8b)`
- **Workflows**: Allow OCR and scrolling workflows from tray `(4e07852)`
- **Media Explorer**: Harden listing, normalize URLs, and improve error handling `(9bab13e, e1a5d59, 6b2b8d6, f4e796b)`
- **Mobile**: Unify iOS share payload handling and TimeZoneInfo serialization `(0aad5c1, a835153)`
- **Upload**: Align MainViewModel helper with parameterless copy/upload events `(06a2232)`
- **ImageEditor**: Update submodule with context menu fixes `(bb862c4, c5618de)`
- **Capture**: Optimize annotation layer rendering and resource management `(f3e3908, b3034be, af35c74, 4048f00)`
- **Documentation**: Update FAQ to correctly reference XerahS instead of ShareX in Linux screen capture section `(699634f)`
- **Infrastructure**: Integrate update-changelog skill into maintenance-chores workflow `(5ade43b)`

### Refactor
- **Core**: Remove WindowState naming collisions `(506072e)`
- **Core**: Split GeneralHelpers into utility classes `(78214dd)`
- **Upload**: Add polymorphic uploader config pilot `(7f2815d)`
- **Workflows**: Extract app workflow orchestration services `(4ee8ab9)`

### Build
- **Android**: Add Android mobile build infrastructure `(3952287)`
- **Linux**: Harden plugin packaging, RPM strip behavior, and display diagnostics `(817d83a, 0723b45, 1c79a94)`
- **Hooks**: Add cross-platform ImageEditor recovery and auto-push on pre-push `(3098824, 899e8f1)`

### Documentation
- **Planning**: Update task planning docs and move completed XIP0033 `(caeaae1, e3f37e3, 04cf9cf, 168b2ea)`
- **Plugins**: Consolidate plugin documentation into 'developers/plugins' and standardize on .xsdp extension `(b78882f, 41702bd, 21927b4)`
- **Developer**: Consolidate developer documentation into 'developers' root folder `(1f17491)`
- **Architecture**: Add feasibility report for JS/CSS migration `(8fc7446, 47d833c, ce35146, e9ed21a, 8e97f89, ccff1c4)`
- **Submodules**: Add sync-submodules workflow and update ImageEditor to latest develop `(a05200f, a0e3054, 14be1df)`
- **Tasks**: Add refactoring audit skill and native UI theming task `(ff8ea0e)`

## v0.15.5

### Features
- **Linux Capture**: Add DBus fallbacks, KDE desktop permissions, and decision trace orchestration `(290b3e0, dc02dbd)`

### Fixes
- **Linux Capture**: Enforce portal-only sandbox policy, unify waterfall, and improve logging `(2de4ac6, c744059, a381faa)`
- **Builds**: Fix cross-platform build configuration and add linux-arm64 support `(ad8611c, 519423d)`

### Refactor
- **Linux Capture**: Modularize providers with parallel lanes, coordinator, and contracts `(733a49d, 5dd9931, 0a81693, 3569c0a)`

### Testing
- **Linux Capture**: Add Linux capture waterfall and lane matrix tests `(7f49769)`

### Documentation
- **Build System**: Rename developer README and add Linux guide `(717be27)`
- **Roadmap**: Finalize Linux phase roadmap and release gate `(76df673)`

## v0.15.0

### Features
- **Mobile**: Add Android and iOS MVP with Share Extension support, .NET MAUI project `(8746372, 03698c6, 493d147)`
- **Mobile**: Add Custom Uploader and Amazon S3 configuration UI `(#124, #125, @Hexeption; 78a488e)`
- **Indexer**: Implement async streaming indexer with progress and cancellation `(8b2fe88)`

### Fixes
- **Image Editor**: Share annotation preview visuals with ImageEditor to ensure consistency `(cc074ad)`
### Fixes
- **Annotations**: Optimize rendering, remove draw-start dot artifact, and improve responsiveness `(d1afa2f, faa84e7, 891eed0)`
- **Workflow**: Complete WorkflowType end-to-end wiring `(47ead0b)`
- **UX**: Hide SilentRun window on first open instead of minimizing `(7567223)`
- **Updates**: Gracefully handle repositories with only pre-releases `(ed68066)`
- **After Capture**: Persist "Show after capture window" behavior across repeated runs `(9a04c9d, a3a581d, a8262d4)`
- **Upload**: Add multi-uploader auto destination fallback and wire mobile Amazon S3 and plugin integration to InstanceManager `(72079e6, c06f17f, a576e78, 44c316b, 02087fb)`
- **Watch Folder**: Convert MOV captures to MP4 `(27f6fec)`
- **Settings**: Make backup and secrets filenames machine-specific `(c618542, 55a32d0)`
- **Amazon S3**: Reorder and renumber setup steps `(3196b02)`
- **iOS**: Improve local signing setup and share extension flow `(30f6822)`

### Build
- **Plugins**: Centralize plugin copy target and pass host TFM `(6bfa2e1)`
- **Dependencies**: Bump Avalonia packages to 11.3.12 `(27ce502)`
- **ImageEditor**: Update submodule for theme-aware view, net9 compatibility, and track develop branch `(5e8eee0, e03ec12, 71601ee, a17d91e, 493d147)`

### Documentation
- **Audits**: Organize audit files and update UI control inventory snapshots `(e3d2a9c, aadfea4)`
- **Tasks**: Mark XIP0030 complete and move to completed tasks `(25a83a1)`

## v0.14.0

### Features
- **Monitor Test**: Implement MonitorTest workflow with diagnostic and pattern testing modes `(56a1ea3, 1dc10f8)`
- **Tools**: Add Ruler workflow with full RegionCapture integration `(5647b4d, 8ea9419)`
- **Indexer**: Make Index Folder open in its own window `(8b20b3b)`
- **Editor**: Integrate upstream ShareX.ImageEditor submodule with File Open choice dialog `(0db2c71, 1a41df5)`
- **Region Capture**: Add annotation options persistence `(7e82df3)`

### Fixes
- **Logging**: Fix duplicate date in log filename on date rotation `(69cb3c2)`
- **Region Capture**: Improve annotation toolbar integration and reduce rebuild pressure `(4500b8a, 3bf8224)`
- **Indexer**: Enable Open in Browser button and remove WebView in favor of system browser `(4582529, 16945a0)`
- **Navigation**: Enable menu navigation and update editor data transfer APIs `(49772bf)`
- **Editor**: Sync ImageEditor fixes, persist annotation options, refactor platform abstractions, enable Zoom to Fit `(3ee199a, 2cc8fa7, 554099c, 79eb2be, e5ffef7)`
- **ImageEditor**: Update submodule with unified undo-redo, smart padding crop sync, clipboard fixes, z-order fixes, and dispose bug fixes `(240649d, b3125b8, 0ee0ad7, 4eb30bf, 0c2b53e, 1131223, 751eb7c)`
- **Packaging**: Restore macOS icon in Windows package build `(ba40fbb)`
- **Upload**: Delay upload progress title update until actual upload starts `(9d4894b)`
- **macOS**: Harden mac packaging and cross-platform editor wiring `(6e1d569)`
- **Dialogs**: Prevent File Open dialog crash and add global exception logging `(5cbf5dd)`

### Build
- **Cross-Compilation**: Add macOS from Windows support and build system documentation `(a2bf5a6, 19b3a84)`
- **Infrastructure**: Fix version parsing in Windows package script `(5069a01)`

## v0.13.0

### Fixes
- **Menu Bar**: Fix hash checker routing and dynamic workflows menu `(8068e6f)`
- **Upload**: Improve Upload Content workflow handling, window UX, and text upload routing `(62a1cda, 4fd8182)`

## v0.12.0

### Fixes
- **Tools**: Add media tools to navigation bar and fix DataTemplate issues `(485a438)`
- **Proxy**: Fix custom uploader loading and add configuration UI `(#77, @Hexeption)`
- **Linux**: Add dark mode support, theme settings, and Wayland Hyprland screenshot support `(#62, @unicxrn; #61, @unicxrn)`
- **macOS**: Add native application menu `(#60, @Hexeption)`
- **Custom Uploaders**: Fix compatibility improvements and version compatibility `(#74, @Hexeption; #71, @emmsixx)`
- **Security**: Fix DPAPI platform warning `(#73, @Hexeption)`

### Refactor
- **Editor**: Rename namespace from ShareX.Editor to XerahS.Editor and update all references `(25135d0, d0d1266, 1dfeb3b)`

### Build
- **Plugins**: Improve plugin copy target to only include plugin assemblies `(a9b5c63)`
- **Configuration**: Update build files, packaging configuration, issue templates, and .gitignore `(09222cc, 5c03c33, b107da9, 789ec93)`

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
- **Security**: Add cross-platform secrets store with diagnostics `(c2b8105, f626f09)`
- **Upload**: Add auto destination uploader `(f3abe81)`
- **Custom Uploaders**: Implement full support including editor UI and integration `(5962870, 8020d73)`
- **Task Settings**: Redesign Task Settings UX with dedicated Image/Video tabs `(43436af)`
- **Tray Icon**: Add recording-aware tray icon with pause/abort controls `(7d22818)`
- **Image Formats**: Add AVIF and WebP image format support `(3b89381)`
- **Linux/Wayland**: Fix screen capture on Wayland by integrating XDG Portal API `(4cc5a9f)`

### Fixes
- **Capture**: Allow clipboard payloads in capture phase `(a2e336f)`
- **Upload**: Add clipboard upload auto routing `(6527590)`
- **Region Capture**: Correct crop offset, refresh AfterCapture UI, and fix coordinate mapping for Windows `(c5efeab, #29)`
- **Linux**: Fix active window capture hierarchy, coordinates, hotkey initialization, and Region Capture `(2957c89, 007f261, 73dd95d, e8a9cc8)`
- **UX**: Hide main window when capture triggered from tray/navbar `(45264fb)`
- **UI**: Fix update dialog layout `(7868256)`

### Refactor
- **Editor**: Update XerahS.Editor.csproj references and docs `(1dfeb3b, 90b9fe0)`


## v0.7.0 - Annotation Overlays & Packaging

### Features & Improvements
- **Annotations**: Enable Annotation Toolbar in Region Capture Overlay and refactor `(05dcaf3, #53)`
- **Region Capture**: Add support for transparent background capture (RectangleTransparent) `(9ee7277)`
- **macOS**: Native single-file app bundle packaging (`.app`) `(c2b882c)`
- **Packaging**: Automated multi-arch Windows release builds `(49a7ec6)`
- **Plugins**: Support for user-installed plugins and packaging `(e787536)`
- **Window Capture**: Add support via monitor cropping fallback `(d73daf5)`
- **Media Library**: Basic implementation `(#49)`

### Bug Fixes
- **Annotation Layer**: Fix coordinate system for multi-monitor/high DPI and compositing `(5d69425, 61bd0c9, 3875298)`
- **Exceptions**: Global exception handling implementation `(ad6d443)`
- **Screen**: Fix frozen screen issue `(#51)`
- **Cursor**: Fix system cursor issues `(#46)`

## v0.6.0 - UI Redesign & Auto-Update

### Features & Improvements
- **UI Redesign**: Comprehensive visual overhaul of all views using Grid layout and consistent styling `(34f4cbf, d390fa7)`
- **Auto-Update**: Implement auto-update system with Avalonia UI `(54b9546)`
- **After Upload**: Add "After Upload" results window `(18a3ab7)`
- **Property Grid**: Add ApplicationConfig property grid `(c4d20bf)`
- **CLI**: Add `verify-recording` command for automated screen recording validation `(732e173)`
- **Editor**: Unify editor undo history across different toolsets `(24ad021)`
- **Architecture**: Move Windows-specific P/Invoke types to dedicated Platform.Windows project `(90da89a)`
- **FFmpeg**: Improve FFmpeg download/config UX with progress hooks and better path resolution `(1646cbb, 7677ceb, b4fdcbf)`
- **Documentation**: Replace ShareX.Avalonia references with XerahS `(#44)`
- **Workflow**: Update cursor handling `(#43)`

### Bug Fixes
- **Recording**: Improve GIF recording quality, add clipboard support, pause, and stroke-based abort `(1baecc0, 4148e49, c3d04a7)`
- **After Upload**: Fix window theming and errors `(9b752c0, 6dfe81e)`
- **Rendering**: Fix speech balloon tail geometry rendering `(784594e)`
- **Region Capture**: Fix system cursor appearing in screenshots and hotkey issues `(85a4e2f, #38, #39)`

## v0.5.0 - Core Capture & Editor Improvements

### Features & Improvements
- **Capture**: Add single instance enforcement for the application `(aacb23b)`
- **Region Capture**: Enhance crosshair visibility, add magnifier pixel sampling, and hide system cursor when ghost cursor active `(a838ae1, 56aa4de, d338b32)`
- **Editor**: Wire ImageEffectsViewModel to unified undo/redo stack `(81a3815)`
- **UX**: Set default file picker location to Desktop for easier access `(f5083e3)`

### Bug Fixes
- Fix 11+ HIGH/MEDIUM priority issues including null safety and resource management `(9188a22, 1f9a74f)`
- Set RegionCaptureControl cursor to None to prevent double cursor visibility `(fe35424)`

## v0.4.0 - Image Effects & Tools

### Features & Improvements
- **Image Effects**: Refactor preset management and improve effects UI `(154a6c9, 5d9dbd7, ee47e3d)`
- **Tools**: Add QR code generator/decoder and Color Picker tools with standard color name mapping `(66bd61b, bdb22f8, 0b50328)`
- **Watch Folders**: Implement Watch Folder system with per-folder workflow assignments `(49e838d, 63124f6, 951e034)`
- **Indexer**: Add Index Folder preview and modernize HTML output using WebView `(63ca369, 3f3a751, e57932e)`
- **macOS**: Add native ScreenCaptureKit video recording support `(fd75640)`

### Bug Fixes
- **Capture**: Fix cursor tracking and visibility during GDI capture `(f6973f6, e0a056b, 265a96a)`
- **Capture**: Fix NullReferenceException in DXGI capture by preventing premature disposal of D3D11 device context `(df9bd33)`


## v0.3.0 - Modern Capture Architecture

### Features & Improvements
- **Modern Capture**: Implement DXGI-based high-performance screen capture for Windows `(1440efc, 25f544d)`
- **Screen Recording**: Unified recording pipeline with Windows Media Foundation and FFmpeg support `(9224b62, 7a6e47b, 8fc451c)`
- **Workflow System**: Major overhaul of hotkeys into full Workflow system with GUID persistence `(faebe87, 09f1e35)`
- **Toast Notifications**: New custom Avalonia-based notification system with advanced settings `(6229154, f1d9b88)`
- **Linux**: Initial support for Wayland via XDG Desktop Portal and native X11 capture `(3573ad1, f7a103c, b92fb89, 7ccd5d9)`
- **Settings**: Add weekly backup system for application settings `(0a8e15f)`
- **UX**: Add tray icon support with customizable click actions `(035e8b4, 4ddfb59)`

### Bug Fixes
- **Modern Capture**: Fix multi-monitor blank capture issues `(52ae45e)`
- **Region Capture**: Fix DPI handling, coordinate mapping, and offsets/scaling on multi-monitor setups `(e4817b1, 954dee3, e47e81b)`
- **Code Quality**: Massive code audit fixing 500+ license headers and 160+ nullability issues `(dca9217, dd90761)`
- **Windows**: Standardize Windows TFM and fix CsWinRT interop issues `(2f44742, 4e88d23)`



## v0.2.0 - macOS Support & Plugin System

### Features & Improvements
- **macOS**: Initial platform support including ScreenCaptureKit, SharpHook hotkeys, and app bundling `(acba9d5, ca05d4b, 6fbf63e)`
- **Plugins**: Implement dynamic plugin system with packaging (`.sxap`), CLI tools, and `.sxadp` file association `(f81c656, a2adbf3, e787536, df9bbd1)`
- **History**: Switch history storage from XML to SQLite with automatic backups `(22b6cf5, 0f20d76)`
- **Editor**: Integrate ShareX.Editor as core component with SkiaSharp rendering `(57bfe32, 90b5871)`

## v0.1.0 - Initial Feature Set

### Core Features
- **UI**: Reimagined interface with two-toolbar system and modern dark theme `(c0bad1e, 231e4df)`
- **Capture**: Region, Fullscreen, and Window capture modes `(4839944)`
- **Annotations**: Object-based editor with Rectangle, Ellipse, Arrow, Line, Text, Number, Crop tools, and full Undo/Redo support `(bd1153c, 9b6cfe0, 9ecd720, cb7b54a)`
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
