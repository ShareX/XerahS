# XerahS Project Status

This document tracks the current implementation status, backend porting checklist, pending tasks, and future enhancements for XerahS.

## Uploader Plugin System - Implementation Status

### ✅ Completed: Multi-Instance Provider Catalog (Dec 2025)

**Architecture implemented:**
- Renamed `IUploaderPlugin` → `IUploaderProvider` with multi-category support
- Separated provider (type) from instance (configured occurrence)
- `ProviderCatalog`: Static registry for provider types
- `InstanceManager`: Singleton for instance lifecycle, persistence, default selection
- Models: `UploaderInstance`, `InstanceConfiguration` with JSON serialization
- UI: `ProviderCatalogViewModel`, `CategoryViewModel`, `UploaderInstanceViewModel`
- Full CRUD operations: Add from catalog, duplicate, rename, remove, set default
- Cross-category support: Same provider (e.g., S3) can serve Image + Text + File

**Providers updated:**
- `ImgurProvider`: Supports Image + Text categories
- `AmazonS3Provider`: Supports Image + Text + File categories

**Files created:** 16 files (~1,500 LOC)
- Core: UploaderInstance, InstanceConfiguration, IUploaderProvider, UploaderProviderBase, ProviderCatalog, InstanceManager, ProviderInitializer
- Providers: ImgurProvider, AmazonS3Provider
- ViewModels: UploaderInstanceViewModel, CategoryViewModel, ProviderCatalogViewModel, ProviderViewModel
- Views: DestinationSettingsView (updated), ProviderCatalogDialog

**Persistence:** `%AppData%/XerahS/uploader-instances.json`

### ✅ Completed: Dynamic Plugin System (Jan 2026)

**Architecture implemented:**
- **Pure Dynamic Loading**: No compile-time plugin references in host app.
- **Isolation**: `PluginLoadContext` (AssemblyLoadContext) for each plugin.
- **Shared Dependencies**: Framework assemblies (Avalonia, Newtonsoft.Json) shared from host context.
- **Static Lifecycle**: Static `PluginLoader` prevents premature GC of contexts.
- **Dynamic UI**: Plugins expose configuration views via `IUploaderProvider.GetConfigView`.

**Components:**
- `PluginDiscovery`: Scans `Plugins/` folder for `plugin.json` manifests.
- `PluginLoader`: Loads assemblies and instantiates providers.
- `ProviderCatalog`: Central registry for both built-in and dynamic providers.

**Plugins Implemented:**
- `ShareX.Imgur.Plugin`: Image uploading with OAuth2.
- `ShareX.AmazonS3.Plugin`: S3 bucket uploads (Image/Text/File).

**Status:**
- [x] Extract common abstractions into `XerahS.Uploaders`.
- [x] Implement `PluginLoadContext` and loading logic.
- [x] Implement `plugin.json` manifest system.
- [x] Create Imgur and S3 plugins as standalone DLLs.
- [x] Integrate with UI (Catalog & Settings).

### ✅ Completed: Core Features & UX Enhancements (Jan 2026)

**Capture Engine Improvements:**
- **Capture Start Delay**: Configurable delay for Screen Capture and Screen Recording (TaskSettings driven).
- **Global Cursor Hiding**: Robust system cursor hiding during capture across platforms.
- **Capture Offset Fix**: Fixed region selection rectangle and crosshair alignment issues.
- **Modern Capture Integration**: 
    - Windows: Direct3D11 Capture
    - macOS: ScreenCaptureKit
    - Linux: XDG Desktop Portal

**UX / UI Polish:**
- **Modeless Tools**: Color Picker and QR Code dialogs are now non-blocking (Show instead of ShowDialog).
- **Control Spacing**: Improved visibility for TaskSettings numeric controls.
- **Hotkey Visibility**: Fixed main window visibility logic when "minimize to tray" is enabled.


### 🔄 Next: File-Type Routing (Planned)

**Specification:** See [.ai/skills/feature-specifications/SKILL.md](../.ai/skills/feature-specifications/SKILL.md); confirm planned behavior against current implementation.

**Goals:**
- Deterministic routing based on file extension
- Conflict prevention (no overlapping file types per category)
- "All File Types" as exclusive option
- UI showing available/blocked file types

**Data model additions:**
- `FileTypeScope` class (AllFileTypes flag + FileExtensions list)
- `UploaderInstance.FileTypeRouting` property
- `IUploaderProvider.GetSupportedFileTypes()` method

**Routing logic:**
```
1. Exact extension match (e.g., .png → Imgur)
2. "All File Types" fallback
3. No match → error/notification
```

**UI features:**
- File type selector with conflict detection
- Disabled checkboxes for already-assigned types
- Tooltip showing which instance blocks a type
- Real-time validation

**Implementation phases:**
1. Data model extensions
2. Routing engine + validation
3. Provider metadata
4. UI (file type selector, conflict warnings)
5. Upload workflow integration

### ✅ Completed: After-Capture / After-Upload Automation Pipeline

**Status**: Core pipeline fully implemented. Most AfterCapture and AfterUpload flags are wired and functional.

#### Core Infrastructure — Fully Implemented

- `TaskManager` singleton with `ConcurrentQueue<WorkerTask>`, concurrency control, `TaskStarted`/`TaskCompleted` events
- `WorkerTask` — full pipeline: Capture → `CaptureJobProcessor` → `UploadJobProcessor`
- `WorkflowTask` — lightweight Path A wrapper (hotkey → capture → upload → clipboard)
- `TaskInfo` with full metadata, state, correlation ID, and upload progress reporting
- `UploadResult` with URL / ThumbnailURL / DeletionURL / ShortenedURL fields
- Upload progress events wired through `ProgressChanged` on `Uploader` base
- Upload retry / fallback chain: Auto provider tries all configured instances, falls back Image → File category
- History written to SQLite (`HistoryManagerSQLite`) after every completed task
- Toast notifications on task failure with truncated error message
- `AfterCaptureWindow` and `AfterUploadWindow` (Views + ViewModels) fully implemented

#### AfterCapture Pipeline (`CaptureJobProcessor`) — Wired Flags

| Flag | Status | Notes |
|------|--------|-------|
| `ShowAfterCaptureWindow` | ✅ | Full modal; result flags applied back; persists setting change |
| `AddImageEffects` | ✅ | SkiaSharp effects pipeline via `TaskHelpers.ApplyImageEffects` |
| `AnnotateMedia` | ✅ | Opens `ShareX.ImageEditor` via `PlatformServices.UI.ShowEditorAsync` |
| `SaveImageToFile` | ✅ | Full path resolution via `TaskHelpers.SaveImageAsFile` |
| `CopyImageToClipboard` | ✅ | Via `PlatformServices.Clipboard.SetImage` |
| `UploadImageToHost` | ✅ | Plugin dispatch with Auto fallback chain |

#### AfterCapture Flags — Services Exist, Not Yet Wired as Pipeline Steps

These features are fully implemented as standalone tool workflows (hotkey-triggered) but not yet dispatched inside `CaptureJobProcessor.ProcessAsync`:

| Flag | Existing Service | Location |
|------|-----------------|----------|
| `PinToScreen` | `PinToScreenToolService` + `PinToScreenManager` | `XerahS.UI/Services/` |
| `AnalyzeImage` | `MediaToolsToolService` + `ImageAnalyzerViewModel` | `XerahS.UI/Services/`, `ViewModels/` |
| `ScanQRCode` | `QrCodeToolService` + `QrCodeDecodeResultsViewModel` | `XerahS.UI/Services/`, `ViewModels/` |
| `DoOCR` | `OcrToolService` + `OcrViewModel` | `XerahS.UI/Services/`, `ViewModels/` |
| `DeleteFile` | `FileHelpers.DeleteFile` + toast action | `XerahS.Common/Helpers/`, `ToastViewModel` |
| `CopyFileToClipboard` | `ClipboardService` exists | Needs flag handler in processor |
| `CopyFilePathToClipboard` | `ClipboardService` exists | Needs flag handler in processor |
| `ShowInExplorer` | `URLHelpers.OpenURL` / `Process.Start` | Needs flag handler in processor |

#### AfterCapture Flags — Not Yet Implemented

| Flag | Blocker |
|------|---------|
| `ShowQuickTaskMenu` | No quick-task menu UI designed |
| `SendImageToPrinter` | `PrintHelper` not ported |
| `SaveImageToFileWithDialog` | No Save-As dialog wired into pipeline |
| `SaveThumbnailImageToFile` | No thumbnail generation in pipeline |
| `PerformActions` | External actions/scripts system not implemented |
| `ShowBeforeUploadWindow` | No before-upload confirmation dialog |

#### AfterUpload Pipeline (`UploadJobProcessor.HandleAfterUploadTasksAsync`) — Wired Flags

| Flag | Status | Notes |
|------|--------|-------|
| `ShowAfterUploadWindow` | ✅ | Non-blocking show with full `AfterUploadWindowInfo` (URL, thumbnails, clipboard format) |
| `CopyURLToClipboard` | ✅ | Async via `PlatformServices.Clipboard.SetTextAsync` |
| `UseURLShortener` | ⚠️ | Flag checked; `TODO` comment; no URL shortener provider wired yet |
| `OpenURL` | ⚠️ | **Window button fully works** (`AfterUploadViewModel.OpenPrimaryUrl` → `PlatformServices.System.OpenUrl`). The *silent* flag path (auto-open without showing the window) is not yet wired in the processor. |
| `ShowQRCode` | ❌ | `QrCodeToolService` exists; not wired in processor |
| `ShareURL` | ❌ | No sharing service implemented |

#### AfterUploadWindow — Fully Implemented

`AfterUploadWindow` (View + `AfterUploadViewModel`) is a complete post-upload UI:
- **Open URL** — `PlatformServices.System.OpenUrl` (cross-platform)
- **Open file** — `PlatformServices.System.OpenFile`
- **Open folder** — `FileHelpers.OpenFolderWithFile`
- **Copy image** — `PlatformServices.Clipboard.SetImage`
- **Copy format** — clipboard format list with grouped entries (Primary URL, Embeds, Management, Local, Custom)
- **Copy errors** — copies error details to clipboard
- **Format substitution** — `$result`, `$url`, `$shorturl`, `$thumbnail`, `$deletion`, `$filepath`, `$filename`
- **Auto-close timer** — configurable countdown via `AutoCloseAfterUploadForm` setting
- **Image preview** — loads from local file or captured `SKBitmap`

## Annotation Subsystem - Implementation Status

### ✅ Completed: Full Image Editor (ShareX.ImageEditor submodule)

The annotation subsystem is fully implemented as the standalone cross-platform `ShareX.ImageEditor` project (Avalonia/SkiaSharp). It is integrated into XerahS via `PlatformServices.UI.ShowEditorAsync` and wired into the `AnnotateMedia` AfterCapture flag.

**Annotation shapes** (`Core/Annotations/`):
- Shapes: Rectangle, Ellipse, Arrow, Line, Freehand, SmartEraser, Crop, CutOut, Image
- Text: Text, Number (auto-increment), SpeechBalloon
- Effects: Blur, Highlight, Magnify, Pixelate, Spotlight

**Presentation layer** (`Presentation/`):
- `SKCanvasControl` — interactive Skia-backed canvas control
- `AnnotationVisualFactory` + per-shape `.Visual.cs` rendering
- `MainViewModel` (partial: CanvasState, EffectPreview, ImageState, ToolOptions)
- `EditorHistory` / `EditorMemento` — full undo/redo
- `EditorCore`, `EditorTool` enum, `EditorToolbarAdapter`
- Dozens of image-effect dialogs (Blur, Border, Brightness, ColorMatrix, Crop, DrawText, Flip, Grayscale, Levels, Resize, Rotate, and many more)

**Hosting / integration** (`Hosting/`):
- `AvaloniaIntegration` — entry point for embedding in Avalonia hosts
- `EditorServices` — DI composition
- `ImageEditorOptions` — configuration contract

**Cross-platform:** Targets `net11.0` (no Windows-specific APIs). Works on Windows, Linux, and macOS.

## Backend Porting Checklist

- [x] Expand UploadersLib settings/data stubs to match ShareX models.
- [x] Align URL helpers with ShareX prefix behavior.
- [x] Expand folder variable handling in Common helpers.
- [x] Port remaining ShareX.HelpersLib non-UI utilities needed by backend workflows.
- [x] Verify uploader settings models cover all fields referenced by config/task flows.
- [x] Audit OAuth manager signature support and match ShareX behavior.
- [ ] Enforce platform abstraction rules for all new ported code (no native references outside platform projects).

## Pending Backend Tasks (Gap Report)

Gap report derived from comparing the ShareX libraries against the Avalonia projects. UI-named files (Form/Control/Designer/Renderer/MessageBox/etc.) are excluded from this checklist and deferred to the UI phase.

### ShareX.HelpersLib

- [x] CodeMenuEntry.cs
- [x] CodeMenuEntryActions.cs
- [x] AnimatedGifCreator.cs
- [x] AppVeyor.cs
- [x] AppVeyorUpdateChecker.cs
- [x] BlackStyleCheckBox.cs (N/A — WinForms-only control; replaced by Avalonia built-in)
- [x] BlackStyleProgressBar.cs (N/A — WinForms-only control; replaced by Avalonia built-in)
- [x] Canvas.cs (N/A — WinForms-only; replaced by Avalonia Canvas / SkiaSharp)
- [x] CaptureHelpers.cs (Refactored to use PlatformServices.Screen)
- [x] ClipboardHelpers.cs (Platform-agnostic, uses PlatformServices pattern)
- [x] ClipboardHelpersEx.cs (Platform-agnostic DIB image manipulation)
- [x] ClipboardFormat.cs
- [x] CMYK.cs
- [x] ColorBgra.cs
- [x] ColorBox.cs (N/A — WinForms-only; replaced by Avalonia ColorPicker)
- [x] ColorEventHandler.cs
- [x] ColorMatrixManager.cs
- [x] ColorPicker.cs (Replaced by `ColorPickerDialog` + `ColorPickerViewModel` + `ColorPickerService`)
- [x] ColorPickerOptions.cs
- [x] ColorSlider.cs (N/A — WinForms-only; replaced by Avalonia Slider)
- [x] ConvolutionMatrixManager.cs
- [x] ConvolutionMatrix.cs
- [x] CursorData.cs
- [x] CustomVScrollBar.cs (N/A — WinForms-only; Avalonia has native scroll support)
- [x] DebugTimer.cs
- [x] DesktopIconManager.cs
- [x] DPAPI.cs
- [x] DPAPIEncryptedStringPropertyResolver.cs
- [x] DPAPIEncryptedStringValueProvider.cs
- [x] WritablePropertiesOnlyResolver.cs
- [x] DWMManager.cs
- [x] Emoji.cs
- [x] EnumDescriptionConverter.cs
- [x] EnumExtensions.cs
- [x] EnumInfo.cs
- [x] EnumProperNameConverter.cs
- [x] EnumProperNameKeepCaseConverter.cs
- [x] Extensions.cs
- [x] ExternalProgram.cs
- [x] FastDateTime.cs
- [x] FFmpegUpdateChecker.cs
- [x] FileDownloader.cs
- [x] FixedSizedQueue.cs
- [x] FileHelpersLite.cs
- [x] FontSafe.cs
- [x] FPSManager.cs
- [x] GifClass.cs
- [x] GitHubUpdateChecker.cs
- [x] GitHubUpdateManager.cs
- [x] GradientInfo.cs
- [x] GradientStop.cs
- [x] GraphicsExtensions.cs
- [x] GraphicsPathExtensions.cs
- [x] GraphicsQualityManager.cs
- [x] GrayscaleQuantizer.cs
- [x] Helpers.cs
- [x] MathHelpers.cs
- [x] HotkeyInfo.cs
- [x] HSB.cs
- [x] HttpClientFactory.cs
- [x] ImageFilesCache.cs
- [x] Logger.cs
- [x] InputHelpers.cs
- [x] InputManager.cs
- [x] JsonHelpers.cs
- [x] KeyboardHook.cs
- [x] KnownTypesSerializationBinder.cs
- [x] ListExtensions.cs
- [x] MaxLengthStream.cs
- [x] MimeTypes.cs
- [x] MutexManager.cs
- [x] MyColor.cs
- [x] MyColorConverter.cs
- [x] NativeConstants.cs
- [x] NativeEnums.cs
- [x] NativeMessagingHost.cs
- [x] NativeMethods.cs
- [x] NativeMethods_Helpers.cs
- [x] NativeStructs.cs (Partial)
- [x] OctreeQuantizer.cs
- [x] PaletteQuantizer.cs
- [x] PingHelper.cs
- [x] PingResult.cs
- [x] Point.cs
- [x] PointF.cs
- [x] PointInfo.cs
- [ ] PrintHelper.cs (Pending — printing not yet ported)
- [x] PrintSettings.cs
- [ ] PrintTextHelper.cs (Pending — printing not yet ported)
- [x] PropertyExtensions.cs
- [x] ProxyInfo.cs
- [x] Quantizer.cs
- [x] RandomCrypto.cs
- [x] RegistryHelpers.cs
- [x] RGBA.cs
- [x] SafeStringEnumConverter.cs
- [x] SevenZipManager.cs
- [x] ShareX.HelpersLib.AssemblyInfo.cs (N/A — auto-generated)
- [x] ShareX.HelpersLib.resources.cs (N/A — auto-generated)
- [x] ShareXTheme.cs
- [x] ShortcutHelpers.cs
- [x] SingleInstanceManager.cs
- [x] StringCollectionToStringTypeConverter.cs
- [x] StringLineReader.cs
- [x] TaskbarManager.cs
- [x] TaskEx.cs
- [x] ThreadWorker.cs
- [x] TimerResolutionManager.cs
- [x] UnsafeBitmap.cs
- [x] UpdateChecker.cs
- [x] URLHelpers.cs
- [x] Vector2.cs
- [x] WindowState.cs
- [x] WshShell.cs
- [x] XmlColor.cs
- [x] XmlFont.cs
- [x] XMLUpdateChecker.cs

### ShareX.HistoryLib

- [x] HistoryItemManager.cs (Replaced by full `XerahS.History` project: `HistoryManagerSQLite`, `HistoryManagerJSON`, `HistoryItem`, `HistoryFilter`, `HistoryHelpers`, `HistoryViewModel`, `HistoryView`)
- [x] ShareX.HistoryLib.AssemblyInfo.cs (N/A — auto-generated)
- [x] ShareX.HistoryLib.resources.cs (N/A — auto-generated)

### ShareX.ImageEffectsLib

- [x] CanvasMargin.cs (Replaced by `BorderDialog` + `BorderImageEffect` in `ShareX.ImageEditor`)
- [x] ColorBgra.cs (Moved to XerahS.Common)
- [x] ColorMatrixManager.cs (Replaced by SkiaSharp)
- [x] ConvolutionMatrixManager.cs (Moved to XerahS.Common)
- [x] DrawingExtensions.cs (Replaced by SkiaSharp)
- [x] DrawParticles.cs (Replaced by `DrawParticlesEffect.cs` + `DrawParticlesDialog` in `ShareX.ImageEditor`)
- [x] DrawTextEx.cs (Replaced by TextAnnotation rendering)
- [x] GradientInfo.cs (Moved to XerahS.Common)
- [x] GradientStop.cs (Moved to XerahS.Common)
- [x] ImageEffectPackager.cs (ShareX.Editor)
- [x] ImageEffectPreset.cs (ShareX.Editor)
- [x] ImageEffectPropertyExtensions.cs (ShareX.Editor)
- [x] ImageEffectsProcessing.cs (ShareX.Editor)
- [x] ImageEffectsSerializationBinder.cs (ShareX.Editor)
- [x] ReplaceColor.cs (ShareX.Editor)
- [x] SelectiveColor.cs (ShareX.Editor)
- [x] ShareX.ImageEffectsLib.AssemblyInfo.cs (N/A — auto-generated)
- [x] ShareX.ImageEffectsLib.resources.cs (N/A — auto-generated)
- [x] UnsafeBitmap.cs (Moved to XerahS.Common)
- [ ] WatermarkConfig.cs (Pending — watermark overlay feature not yet ported)
- [ ] WatermarkHelpers.cs (Pending — watermark overlay feature not yet ported)

### ShareX.IndexerLib

Note: The folder/disk indexing functionality of `ShareX.IndexerLib` (HTML/XML/CSV index generation) has not been ported. It is low-priority; auto-generated files are N/A.

- [x] ShareX.IndexerLib.AssemblyInfo.cs (N/A — auto-generated)
- [x] ShareX.IndexerLib.resources.cs (N/A — auto-generated)

### ShareX.MediaLib

- [x] DesignStubs.cs (N/A — WinForms designer stubs, not applicable to Avalonia)
- [x] FFmpegDownloader.cs (Replaced by `XerahS.Common.FFmpegDownloader`)
- [ ] FFmpegGitHubDownloader.cs (Pending — GitHub-specific FFmpeg release download not yet ported)
- [x] GradientInfo.cs (Replaced by `XerahS.Common.GradientInfo` + `XerahS.Media`)
- [ ] ImageBeautifier.cs (Pending — `ImageBeautifierOptions.cs` exists in `XerahS.Media` but the processor/renderer not yet ported)
- [x] ImageCombinerOptions.cs (Absorbed into `XerahS.Media.ImageCombiner` with full UI: `ImageCombinerViewModel` + `ImageCombinerWindow`)
- [x] Resources.cs (N/A — auto-generated resource class)
- [x] ShareX.MediaLib.AssemblyInfo.cs (N/A — auto-generated)
- [x] ShareX.MediaLib.resources.cs (N/A — auto-generated)

### ShareX.ScreenCaptureLib

- [x] AnnotationOptions.cs (ShareX.Editor)
- [x] ArrowDrawingShape.cs (ShareX.Editor/Annotations)
- [x] BaseDrawingShape.cs (ShareX.Editor/Annotations)
- [x] BaseEffectShape.cs (ShareX.Editor/Annotations)
- [x] BaseRegionShape.cs (ShareX.Editor/Annotations)
- [x] BaseShape.cs (ShareX.Editor/Annotations)
- [x] BaseTool.cs (ShareX.Editor/Annotations)
- [x] BlurEffectShape.cs (ShareX.Editor/Annotations)
- [x] ColorBlinkAnimation.cs (N/A — WinForms GDI+ overlay animation; superseded by Avalonia RegionCapture)
- [x] CropTool.cs (ShareX.Editor/Annotations)
- [ ] CursorDrawingShape.cs (Pending — cursor stamp annotation not yet in ShareX.ImageEditor)
- [x] CutOutTool.cs (ShareX.Editor/Annotations)
- [x] EllipseDrawingShape.cs (ShareX.Editor/Annotations)
- [x] EllipseRegionShape.cs (N/A — WinForms region selection shape; XerahS.RegionCapture handles region selection via Avalonia)
- [x] FreehandArrowDrawingShape.cs (ShareX.Editor/Annotations)
- [x] FreehandDrawingShape.cs (ShareX.Editor/Annotations)
- [x] FreehandRegionShape.cs (N/A — WinForms region selection shape; superseded by XerahS.RegionCapture)
- [x] HardDiskCache.cs (N/A — WinForms-era disk caching for screen recording; not needed in new architecture)
- [x] HighlightEffectShape.cs (ShareX.Editor/Annotations)
- [x] ImageCache.cs (N/A — WinForms-era in-memory frame cache; not needed in new architecture)
- [x] ImageDrawingShape.cs (ShareX.Editor/Annotations)
- [x] ImageFileDrawingShape.cs (ShareX.Editor/Annotations)
- [x] ImageScreenDrawingShape.cs (ShareX.Editor/Annotations)
- [x] InputManager.cs (ShareX.Editor)
- [x] LineDrawingShape.cs (ShareX.Editor/Annotations)
- [x] MagnifyDrawingShape.cs (ShareX.Editor/Annotations)
- [x] MouseState.cs (ShareX.Editor)
- [x] PixelateEffectShape.cs (ShareX.Editor/Annotations)
- [x] PointAnimation.cs (N/A — WinForms GDI+ overlay animation; superseded by Avalonia RegionCapture)
- [x] RectangleAnimation.cs (N/A — WinForms GDI+ overlay animation; superseded by Avalonia RegionCapture)
- [x] RectangleDrawingShape.cs (ShareX.Editor/Annotations)
- [x] RectangleRegionShape.cs (N/A — WinForms region selection shape; superseded by XerahS.RegionCapture)
- [x] RegionCaptureOptions.cs (XerahS.RegionCapture)
- [x] RegionCaptureTasks.cs (XerahS.RegionCapture)
- [x] ResizeNode.cs (ShareX.Editor)
- [x] ScreenRecorder.cs (XerahS.RegionCapture)
- [x] ScreenRecordingOptions.cs (XerahS.RegionCapture)
- [x] Screenshot.cs (XerahS.RegionCapture)
- [x] Screenshot_Transparent.cs (XerahS.RegionCapture)
- [x] ScrollbarManager.cs (N/A — WinForms scrollbar overlay for scrolling capture; superseded by `XerahS.RegionCapture.ScrollingCaptureManager`)
- [x] ScrollingCaptureManager.cs (Replaced by `XerahS.RegionCapture.ScrollingCaptureManager` + `IScrollingCaptureService` + `WindowsScrollingCaptureService`)
- [x] ShapeManager.cs (N/A — monolithic WinForms shape manager; fully superseded by `ShareX.ImageEditor` + `XerahS.RegionCapture`)
- [x] ShareX.ScreenCaptureLib.AssemblyInfo.cs (N/A — auto-generated)
- [x] ShareX.ScreenCaptureLib.resources.cs (N/A — auto-generated)
- [x] SmartEraserDrawingShape.cs (ShareX.Editor/Annotations)
- [ ] SnapSize.cs (Pending — snap-to-size helper for region selection not yet ported)
- [x] SpeechBalloonDrawingShape.cs (ShareX.Editor/Annotations)
- [x] SpotlightTool.cs (ShareX.Editor/Annotations)
- [x] StepDrawingShape.cs (Replaced by NumberAnnotation in ShareX.Editor)
- [ ] StickerDrawingShape.cs (Pending — sticker annotation shape not yet in ShareX.ImageEditor; `CartoonStickerCutoutImageEffect` is a separate effect, not the same)
- [x] TextAnimation.cs (N/A — WinForms GDI+ overlay animation; superseded by Avalonia RegionCapture)
- [x] TextDrawingOptions.cs (ShareX.Editor)
- [x] TextDrawingShape.cs (ShareX.Editor/Annotations)
- [ ] TextOutlineDrawingShape.cs (Pending — text outline/stroke annotation not yet in ShareX.ImageEditor)

### ShareX.UploadersLib

- [x] AmazonS3.cs (Implemented as Plugin)
- [x] AmazonS3StorageClass.cs (XerahS.Uploaders)
- [ ] AzureStorage.cs
- [ ] BackblazeB2.cs
- [ ] BitlyURLShortener.cs
- [ ] Box.cs
- [x] Chevereto.cs (XerahS.Uploaders)
- [x] CustomFileUploader.cs (XerahS.Uploaders/CustomUploader)
- [x] Dropbox.cs (Implemented as `Dropbox.Plugin`)
- [ ] Email.cs
- [ ] EmailSharingService.cs
- [ ] FirebaseDynamicLinksURLShortener.cs
- [ ] FlickrUploader.cs
- [x] FTP.cs (XerahS.Uploaders)
- [x] GitHubGist.cs (Implemented as `GitHubGist.Plugin`)
- [ ] GoogleCloudStorage.cs
- [ ] GoogleDrive.cs
- [ ] Hastebin.cs
- [ ] Hostr.cs
- [ ] ImageShackUploader.cs
- [x] Imgur.cs (Implemented as Plugin)
- [ ] JiraUpload.cs
- [ ] KuttURLShortener.cs
- [ ] Lambda.cs
- [ ] LobFile.cs
- [ ] LocalhostAccount.cs
- [ ] MediaFire.cs
- [ ] Mega.cs
- [ ] OneDrive.cs
- [ ] OneTimeSecret.cs
- [ ] OwnCloud.cs
- [ ] Paste_ee.cs
- [x] Pastebin.cs (Implemented as `Pastebin.Plugin`)
- [ ] Pastie.cs
- [ ] Photobucket.cs
- [ ] Plik.cs
- [ ] PolrURLShortener.cs
- [ ] Pomf.cs
- [ ] Pushbullet.cs
- [ ] PushbulletSharingService.cs
- [ ] Puush.cs
- [x] Resources.cs (N/A — auto-generated resource class)
- [ ] Seafile.cs
- [ ] SharedFolderUploader.cs
- [x] ShareX.UploadersLib.AssemblyInfo.cs (N/A — auto-generated)
- [x] ShareX.UploadersLib.resources.cs (N/A — auto-generated)
- [ ] Streamable.cs
- [x] Stubs.cs (N/A — WinForms UI stubs, not applicable to Avalonia)
- [ ] Sul.cs
- [ ] Upaste.cs
- [ ] UploadScreenshot.cs
- [ ] VgymeUploader.cs
- [ ] YourlsURLShortener.cs
- [ ] YouTube.cs
- [ ] ZeroWidthURLShortener.cs

## TODO: ARM64 Optimisations and Compatibility

### Goal

Ensure XerahS runs natively on Windows ARM64 and remains portable to Linux ARM64 and macOS ARM64 where feasible.

### Build Targets

- Add `win-arm64` to CI publish matrix and local build scripts
- Ensure self-contained publish works for `win-arm64` at the project level, not the solution level
- Produce separate artefacts for x64 and arm64 with clear naming

### Native Dependencies Audit

- Inventory all native binaries and platform-specific libraries used by the app
- Identify x64-only components and plan replacements or arm64 builds
- For each native dependency define source, licence, update process, and supported RIDs

### FFmpeg and Video Pipeline

- Provide ARM64 ffmpeg builds or a managed fallback
- Verify screen recording, GIF encoding, and video conversion paths on ARM64
- Add runtime selection logic for the correct ffmpeg binary per RID

### P/Invoke and Interop Hardening

- Audit all P/Invoke calls and structs for pointer size assumptions
- Replace `int` handles with `nint` where appropriate
- Validate packing, alignment, and charsets for ARM64
- Add tests that exercise critical interop paths on arm64

### Capture and Graphics

- Remove reliance on GDI+ only code paths where possible
- Validate capture performance on ARM64 and avoid unnecessary pixel format conversions
- Optimise image processing hotspots for ARM64 including memory copies and allocations
- Consider SIMD-friendly code paths where it is low risk

### Hotkeys, Hooks, and Input

- Verify global hotkeys and low-level hooks on Windows ARM64
- If hooks rely on native DLLs, provide arm64 versions or a managed approach
- Add graceful fallback for features not supported on non-Windows or arm64

### Installer and Update Experience

- Ensure installer detects architecture and installs the correct build
- Keep plugins and user data in per-user locations compatible with ARM64
- Validate portable mode behaviour on ARM64

### Plugin Loading and Isolation

- Ensure plugin loader supports arm64 assemblies and blocks x64-only plugins
- Add compatibility metadata for plugins such as supported RIDs and minimum app version
- Add logging for plugin load failures including architecture mismatch

### Performance and Diagnostics

- Add startup timing logs for ARM64 builds
- Add optional verbose logging around capture, encode, and upload workflows
- Create a lightweight benchmark command for capture and encode throughput

### Test Coverage

- Add automated smoke tests for `win-arm64` on CI if runners are available
- Add manual test checklist for Windows on ARM64 devices
- Track known limitations and workarounds in docs
