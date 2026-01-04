## License Header Requirement

When creating or editing C# files (`.cs`) in this repository, include the following license header at the top of the file (tailored for the Avalonia implementation):

```csharp
#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
```

# Agent Rules

## Multi-Agent Coordination

This project uses multiple AI developer agents working in parallel. See [MULTI_AGENT_COORDINATION.md](MULTI_AGENT_COORDINATION.md) for:
- Agent roles (Antigravity, Codex, Copilot)
- Task distribution rules
- Git workflow and branch naming
- Conflict avoidance protocols
- Communication requirements

**Lead Agent**: Antigravity (architecture, integration, merge decisions)

---

- Always summarize code changes in the final response, and use that summary when performing `git push` after each code update.

## Semantic Versioning Automation

**Product Name**: ShareX Ava (Project files/UI should reflect this, code namespace remains `ShareX.Avalonia`)

**Current Version**: 0.1.0 (Managed centrally in `Directory.Build.props`)

**Rules for Agents**:
1. **Automated Version Bumping**:
   - **PATCH (x.x.X)**: Bump for bug fixes, refactors, or minor tasks (Complexity ≤ 3).
   - **MINOR (x.X.x)**: Bump for new features, significant UI changes, or new workflows (Complexity 4-7).
   - **MAJOR (X.x.x)**: Bump for breaking changes or major releases (Complexity ≥ 8).

2. **How to Bump**:
   - Check `Directory.Build.props` for the current version.
   - Increment accordingly based on the highest complexity of changes in your session.
   - Update `<Version>` tag in `Directory.Build.props`.
   - **Do not** update individual `.csproj` versions; they inherit from `Directory.Build.props`.

3. **Commit Messages**:
   - Prefix commits with `[vX.Y.Z]` relative to the new version.
   - Example: `[v0.1.1] [Fix] Captured images now display in Editor`

## Semantic Versioning Standards
- Uses standard SemVer 2.0.0 (MAJOR.MINOR.PATCH).
- Pre-release tags allowed (e.g., `0.1.0-alpha.1`) for unstable features.

- This document provides clear operating instructions for LLM-assisted work in this repository.

## Scope

- Applies to documentation, code, tests, configs, and release notes.
- If a request conflicts with repository guidelines, ask for clarification.

## Communication

- Be concise and factual.
- Prefer short paragraphs and bullet lists.
- Use consistent terminology from existing docs.

## Repository Awareness

- Read existing docs before adding new guidance.
- Avoid duplicating information unless it is a deliberate summary.
- Keep instructions in ASCII unless the target file already uses Unicode.

## Change Safety

- Do not remove or rewrite unrelated content.
- Do not change version numbers unless explicitly requested.
- Flag assumptions clearly when requirements are ambiguous.

## Code and Config Changes

- Follow existing patterns in each project area.
- Keep changes minimal and targeted.
- Add small comments only when necessary to explain non-obvious logic.
- **Always build the solution (`dotnet build`) at the end of a coding session to ensure no compilation errors were introduced.**

## Testing

- If you modify executable code, suggest relevant tests.
- If tests are added, align them with current test conventions.

## Documentation

- Update or add docs when behavior or usage changes.
- Keep filenames and headings descriptive and stable.
- **All .md files created during work (including artifacts in brain directory) must be committed to the GitHub repository.**
- Ensure documentation artifacts are included in git commits alongside code changes.

## Security and Privacy

- Do not include secrets or tokens.
- Avoid logging sensitive data in examples.

## Output Format

- For changes, summarize what changed and where.
- Provide next steps only when they are natural and actionable.

## Platform Abstractions and Native Code Rules

All platform specific functionality must be isolated behind platform abstraction interfaces.

No code outside ShareX.Avalonia.Platform.* projects may reference:

NativeMethods

NativeConstants, NativeEnums, NativeStructs

Win32 P Invoke

System.Windows.Forms

Windows specific handles or messages

Direct calls to Windows APIs are forbidden in Common, Core, Uploaders, Media, or other backend projects.

### Required architecture

Define platform neutral interfaces in ShareX.Avalonia.Platform.Abstractions.

Implement Windows functionality in ShareX.Avalonia.Platform.Windows.

Create stub implementations for future platforms:

ShareX.Avalonia.Platform.Linux

ShareX.Avalonia.Platform.MacOS

### Windows only features

If a capability is Windows only:

It must still be defined via an abstraction interface.

Windows provides the concrete implementation.

Other platforms provide a stub implementation that either:

throws PlatformNotSupportedException, or

returns a safe no-op with a logged warning.

UI and workflows must detect capability availability and disable or hide unsupported features.

### Porting rule for existing ShareX code

A file may only be ported directly if it contains zero references to:

NativeMethods or related native helpers

WinForms types

Windows specific interop

If a file mixes logic and native calls:

Extract pure logic into Common or Core.

Move native code into ShareX.Avalonia.Platform.Windows.

Replace callsites with interface calls.

Native method names and signatures should remain Windows specific and must not leak into shared layers.

### Enforcement

When porting ShareX.HelpersLib, files that reference:

NativeMethods.cs

NativeMethods_Helpers.cs

NativeMessagingHost.cs

DWM, hooks, clipboard, or input APIs

must be treated as platform code and cannot be copied wholesale.

## Main Goal and Porting Rules

- Main goal: build ShareX.Avalonia by porting the ShareX backend first, then designing the Avalonia UI.
- Examine `C:\Users\liveu\source\repos\ShareX Team\ShareX` to understand existing non-UI logic and reuse it by copying into this repo after the Avalonia solution and projects are drafted.
- Do not reuse WinForms or any WinForms UI code; only copy non-UI methods and data models.
- Keep the backend port the priority until the UI design phase is explicitly started.

## Avalonia Solution Proposal (from ShareX analysis)

- Start with the simplest backend libraries first, then move to more complex modules.
- Proposed structure:
  - `ShareX.Avalonia.Common`: shared helpers, serialization, utilities.
  - `ShareX.Avalonia.Core`: task settings, workflows, application-level services.
  - `ShareX.Avalonia.Uploaders`: uploaders, config, OAuth, HTTP helpers.
  - `ShareX.Avalonia.History`: history models and persistence.
  - `ShareX.Avalonia.Indexer`: file indexing and search.
  - `ShareX.Avalonia.ImageEffects`: filters/effects pipeline.
  - `ShareX.Avalonia.Media`: encoding, thumbnails, FFmpeg integration.
  - `ShareX.Avalonia.ScreenCapture`: capture engines and platform abstractions.
  - `ShareX.Avalonia.Platform.*`: OS-specific implementations (Windows first, others later).
  - `ShareX.Avalonia.App` and `ShareX.Avalonia.UI`: Avalonia UI and view models (defer until backend is ready).

## Uploader Plugin System - Implementation Status

### ✅ Completed: Multi-Instance Provider Catalog (Dec 2024)

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

**Persistence:** `%AppData%/ShareX.Avalonia/uploader-instances.json`

### Next: File-Type Routing (Planned)

**Specification:** See `FILETYPE_ROUTING_SPEC.md` (in brain artifacts)

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

### ✅ Completed: Dynamic Plugin System (Jan 2025)

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
- [x] Extract common abstractions into `ShareX.Avalonia.Uploaders`.
- [x] Implement `PluginLoadContext` and loading logic.
- [x] Implement `plugin.json` manifest system.
- [x] Create Imgur and S3 plugins as standalone DLLs.
- [x] Integrate with UI (Catalog & Settings).

### TODO - Full Automation Workflow (Path B)

**Goal**: Complete end-to-end automation matching ShareX feature parity

**Status**: Path A (minimal automation) in progress. Path B deferred for future implementation.

**Specification**: See `automation_workflow_plan.md` (brain artifacts)

**Components needed**:
1. **Task System** (~600 LOC)
   - `WorkflowTask.cs` - Async automation engine with lifecycle management
   - `TaskInfo.cs` - Task metadata and state
   - `TaskManager.cs` - Queue orchestration with concurrency control
   - `TaskStatus.cs` - Status enums and job types

2. **Workflow Configuration** (~200 LOC)
   - `AfterCaptureTasks` flags: AddImageEffects, AnnotateImage, SaveImageToFile, UploadImageToHost, etc.
   - `AfterUploadTasks` flags: CopyURLToClipboard, OpenURL, ShortenURL, ShowQRCode, ShowNotification
   - `UploadInfoParser` - Template-based clipboard format (`%url%`, `%filename%`, `%time%`, etc.)

3. **Upload Integration** (~150 LOC)
   - `UploadResult` model with URL/ThumbnailURL/DeletionURL/ShortenedURL
   - `UploaderErrorManager` for error handling
   - Progress tracking events
   - Retry logic with secondary uploaders

4. **After-Upload Automation** (~100 LOC)
   - URL shortening integration
   - Clipboard copy with custom formats
   - Browser opening
   - QR code generation
   - Notification system

5. **UI Integration** (~150 LOC)
   - Progress indicator during upload
   - Toast notifications
   - Upload history panel
   - Task list view

**Implementation phases**:
1. Core task system (WorkflowTask, TaskManager)
2. Upload workflow integration
3. After-upload automation (clipboard, notifications)
4. Hotkey → full automation wiring
5. Progress UI and history

**Total effort**: ~1,200 LOC, 8-12 hours

**Blockers**:
- Annotation editor is blocking (modal). Need separate workflow path for "quick capture+upload" vs "editor mode"
- Need async/await patterns throughout (ShareX uses threads)
- Upload providers need `UploadAsync` method signatures

**Path A (minimal) delivered**:
- Basic WorkflowTask structure
- Upload provider wiring
- Simple "Copy URL after upload" to clipboard
- Hotkey → Capture → Upload → URL workflow

**Path B additions**:
- Full task queue with concurrency limits
- Comprehensive AfterCapture/AfterUpload pipelines
- Progress tracking with percentage/speed/ETA
- Notification system with customizable formats
- Upload retry logic with fallback uploaders
- Task history persistence
- UI for task monitoring and management


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
- [ ] BlackStyleCheckBox.cs (TODO: Avalonia UI)
- [ ] BlackStyleProgressBar.cs (TODO: Avalonia UI)
- [ ] Canvas.cs (TODO: Avalonia UI)
- [x] CaptureHelpers.cs (Refactored to use PlatformServices.Screen)
- [x] ClipboardHelpers.cs (Platform-agnostic, uses PlatformServices pattern)
- [x] ClipboardHelpersEx.cs (Platform-agnostic DIB image manipulation)
- [x] ClipboardFormat.cs
- [x] CMYK.cs
- [x] ColorBgra.cs
- [ ] ColorBox.cs (TODO: Avalonia UI)
- [x] ColorEventHandler.cs
- [x] ColorMatrixManager.cs
- [ ] ColorPicker.cs (TODO: Avalonia UI)
- [x] ColorPickerOptions.cs
- [ ] ColorSlider.cs (TODO: Avalonia UI)
- [x] ConvolutionMatrixManager.cs
- [x] ConvolutionMatrix.cs
- [x] CursorData.cs
- [ ] CustomVScrollBar.cs (TODO: Avalonia UI)
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
- [ ] PrintHelper.cs (TODO: Avalonia UI - printing)
- [x] PrintSettings.cs
- [ ] PrintTextHelper.cs (TODO: Avalonia UI - printing)
- [x] PropertyExtensions.cs
- [x] ProxyInfo.cs
- [x] Quantizer.cs
- [x] RandomCrypto.cs
- [x] RegistryHelpers.cs
- [x] RGBA.cs
- [x] SafeStringEnumConverter.cs
- [x] SevenZipManager.cs
- [ ] ShareX.HelpersLib.AssemblyInfo.cs
- [ ] ShareX.HelpersLib.resources.cs
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
- [ ] HistoryItemManager.cs
- [ ] ShareX.HistoryLib.AssemblyInfo.cs
- [ ] ShareX.HistoryLib.resources.cs

### ShareX.ImageEffectsLib
- [ ] CanvasMargin.cs
- [ ] ColorBgra.cs
- [ ] ColorMatrixManager.cs
- [ ] ConvolutionMatrixManager.cs
- [ ] DrawingExtensions.cs
- [ ] DrawParticles.cs
- [ ] DrawTextEx.cs
- [ ] GradientInfo.cs
- [ ] GradientStop.cs
- [ ] ImageEffectPackager.cs
- [ ] ImageEffectPreset.cs
- [ ] ImageEffectPropertyExtensions.cs
- [ ] ImageEffectsProcessing.cs
- [ ] ImageEffectsSerializationBinder.cs
- [ ] ReplaceColor.cs
- [ ] SelectiveColor.cs
- [ ] ShareX.ImageEffectsLib.AssemblyInfo.cs
- [ ] ShareX.ImageEffectsLib.resources.cs
- [ ] UnsafeBitmap.cs
- [ ] WatermarkConfig.cs
- [ ] WatermarkHelpers.cs

### ShareX.IndexerLib
- [ ] ShareX.IndexerLib.AssemblyInfo.cs
- [ ] ShareX.IndexerLib.resources.cs

### ShareX.MediaLib
- [ ] DesignStubs.cs
- [ ] FFmpegDownloader.cs
- [ ] FFmpegGitHubDownloader.cs
- [ ] GradientInfo.cs
- [ ] ImageBeautifier.cs
- [ ] ImageCombinerOptions.cs
- [ ] Resources.cs
- [ ] ShareX.MediaLib.AssemblyInfo.cs
- [ ] ShareX.MediaLib.resources.cs

### ShareX.ScreenCaptureLib
- [ ] AnnotationOptions.cs
- [ ] ArrowDrawingShape.cs
- [ ] BaseDrawingShape.cs
- [ ] BaseEffectShape.cs
- [ ] BaseRegionShape.cs
- [ ] BaseShape.cs
- [ ] BaseTool.cs
- [ ] BlurEffectShape.cs
- [ ] ColorBlinkAnimation.cs
- [ ] CropTool.cs
- [ ] CursorDrawingShape.cs
- [ ] CutOutTool.cs
- [ ] EllipseDrawingShape.cs
- [ ] EllipseRegionShape.cs
- [ ] FreehandArrowDrawingShape.cs
- [ ] FreehandDrawingShape.cs
- [ ] FreehandRegionShape.cs
- [ ] HardDiskCache.cs
- [ ] HighlightEffectShape.cs
- [ ] ImageCache.cs
- [ ] ImageDrawingShape.cs
- [ ] ImageFileDrawingShape.cs
- [ ] ImageScreenDrawingShape.cs
- [ ] InputManager.cs
- [ ] LineDrawingShape.cs
- [ ] MagnifyDrawingShape.cs
- [ ] MouseState.cs
- [ ] PixelateEffectShape.cs
- [ ] PointAnimation.cs
- [ ] RectangleAnimation.cs
- [ ] RectangleDrawingShape.cs
- [ ] RectangleRegionShape.cs
- [ ] RegionCaptureOptions.cs
- [ ] RegionCaptureTasks.cs
- [ ] ResizeNode.cs
- [ ] ScreenRecorder.cs
- [ ] ScreenRecordingOptions.cs
- [ ] Screenshot.cs
- [ ] Screenshot_Transparent.cs
- [ ] ScrollbarManager.cs
- [ ] ScrollingCaptureManager.cs
- [ ] ShapeManager.cs
- [ ] ShareX.ScreenCaptureLib.AssemblyInfo.cs
- [ ] ShareX.ScreenCaptureLib.resources.cs
- [ ] SmartEraserDrawingShape.cs
- [ ] SnapSize.cs
- [ ] SpeechBalloonDrawingShape.cs
- [ ] SpotlightTool.cs
- [ ] StepDrawingShape.cs
- [ ] StickerDrawingShape.cs
- [ ] TextAnimation.cs
- [ ] TextDrawingOptions.cs
- [ ] TextDrawingShape.cs
- [ ] TextOutlineDrawingShape.cs

### ShareX.UploadersLib
- [ ] AmazonS3.cs
- [ ] AmazonS3StorageClass.cs
- [ ] AzureStorage.cs
- [ ] BackblazeB2.cs
- [ ] BitlyURLShortener.cs
- [ ] Box.cs
- [ ] Chevereto.cs
- [ ] CustomFileUploader.cs
- [ ] Dropbox.cs
- [ ] Email.cs
- [ ] EmailSharingService.cs
- [ ] FirebaseDynamicLinksURLShortener.cs
- [ ] FlickrUploader.cs
- [ ] FTP.cs
- [ ] GitHubGist.cs
- [ ] GoogleCloudStorage.cs
- [ ] GoogleDrive.cs
- [ ] Hastebin.cs
- [ ] Hostr.cs
- [ ] ImageShackUploader.cs
- [ ] Imgur.cs
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
- [ ] Pastebin.cs
- [ ] Pastie.cs
- [ ] Photobucket.cs
- [ ] Plik.cs
- [ ] PolrURLShortener.cs
- [ ] Pomf.cs
- [ ] Pushbullet.cs
- [ ] PushbulletSharingService.cs
- [ ] Puush.cs
- [ ] Resources.cs
- [ ] Seafile.cs
- [ ] SharedFolderUploader.cs
- [ ] ShareX.UploadersLib.AssemblyInfo.cs
- [ ] ShareX.UploadersLib.resources.cs
- [ ] Streamable.cs
- [ ] Stubs.cs
- [ ] Sul.cs
- [ ] Upaste.cs
- [ ] UploadScreenshot.cs
- [ ] VgymeUploader.cs
- [ ] YourlsURLShortener.cs
- [ ] YouTube.cs
- [ ] ZeroWidthURLShortener.cs

## TODO ARM64 optimisations and compatibility

Goal
Ensure ShareX.Avalonia runs natively on Windows ARM64 and remains portable to Linux ARM64 and macOS ARM64 where feasible.

Build targets
- Add `win-arm64` to CI publish matrix and local build scripts
- Ensure self contained publish works for `win-arm64` at the project level, not the solution level
- Produce separate artefacts for x64 and arm64 with clear naming

Native dependencies audit
- Inventory all native binaries and platform specific libraries used by the app
- Identify x64 only components and plan replacements or arm64 builds
- For each native dependency define source, licence, update process, and supported RIDs

FFmpeg and video pipeline
- Provide ARM64 ffmpeg builds or a managed fallback
- Verify screen recording, GIF encoding, and video conversion paths on ARM64
- Add runtime selection logic for the correct ffmpeg binary per RID

P Invoke and interop hardening
- Audit all P Invoke calls and structs for pointer size assumptions
- Replace `int` handles with `nint` where appropriate
- Validate packing, alignment, and charsets for ARM64
- Add tests that exercise critical interop paths on arm64

Capture and graphics
- Remove reliance on GDI plus only code paths where possible
- Validate capture performance on ARM64 and avoid unnecessary pixel format conversions
- Optimise image processing hotspots for ARM64 including memory copies and allocations
- Consider SIMD friendly code paths where it is low risk

Hotkeys, hooks, and input
- Verify global hotkeys and low level hooks on Windows ARM64
- If hooks rely on native DLLs, provide arm64 versions or a managed approach
- Add graceful fallback for features not supported on non Windows or arm64

Installer and update experience
- Ensure installer detects architecture and installs the correct build
- Keep plugins and user data in per user locations compatible with ARM64
- Validate portable mode behaviour on ARM64

Plugin loading and isolation
- Ensure plugin loader supports arm64 assemblies and blocks x64 only plugins
- Add compatibility metadata for plugins such as supported RIDs and minimum app version
- Add logging for plugin load failures including architecture mismatch

Performance and diagnostics
- Add startup timing logs for ARM64 builds
- Add optional verbose logging around capture, encode, and upload workflows
- Create a lightweight benchmark command for capture and encode throughput

Test coverage
- Add automated smoke tests for `win-arm64` on CI if runners are available
- Add manual test checklist for Windows on ARM64 devices
- Track known limitations and workarounds in docs

## Annotation Subsystem - Implementation Status

### ✅ Phase 1 Complete: Core Annotation Models (Dec 2024)

**New project:** `ShareX.Avalonia.Annotations`  
**Files created:** 10 files (~800 LOC)

**Annotation types implemented:**
- `EditorTool` enum: Select, Rectangle, Ellipse, Arrow, Line, Text, Number, Spotlight, Crop, Pen
- `Annotation` abstract base class with rendering, hit-testing, bounds calculation
- Concrete types: RectangleAnnotation, EllipseAnnotation, LineAnnotation, ArrowAnnotation, TextAnnotation, NumberAnnotation (auto-increment), SpotlightAnnotation (focus effect), CropAnnotation (resize handles)

**Technical features:**
- Avalonia `DrawingContext` rendering
- Configurable hit testing (tolerance-based)
- Color/stroke width support
- Z-index for layering
- Manual distance calculations (Avalonia API compatibility)
- FormattedText for text rendering with bold/italic support

**Build status:** ✅ Clean (0 errors)

### Remaining Tasks

#### Phase 2: Canvas Control (~6-8h)
Implement the annotation subsystem for ShareX.Avalonia, replacing WinForms and System.Drawing with Avalonia and Skia. All features listed in the ShapeType enum must be available.

### Tasks

- **Design core abstractions**
  - Define a BaseShape in ShareX.Avalonia with properties for position, size, colour, border thickness and hit-testing.
  - Create Avalonia equivalents for all ShapeType values (RegionRectangle, RegionEllipse, RegionFreehand, DrawingRectangle, DrawingEllipse, DrawingFreehand, DrawingFreehandArrow, DrawingLine, DrawingArrow, DrawingTextOutline, DrawingTextBackground, DrawingSpeechBalloon, DrawingStep, DrawingMagnify, DrawingImage, DrawingImageScreen, DrawingSticker, DrawingCursor, DrawingSmartEraser, EffectBlur, EffectPixelate, EffectHighlight, ToolSpotlight, ToolCrop, ToolCutOut). Each must subclass BaseShape and implement custom rendering logic.
  - Expose shape-specific properties such as arrow-head direction, blur radius, pixel size and highlight colour with appropriate defaults.

- **Rendering and effects**
  - Replace GDI+ drawing with Avalonias DrawingContext and Skia. Implement vector drawing for shapes and text. For effects (blur, pixelate, highlight), use Skia image filters or custom pixel shaders to apply the effect only within the shape bounds.
  - Implement the spotlight tool by darkening the canvas outside the selected rectangle and optionally applying blur/ellipse.
  - Implement magnify drawing by rendering a zoomed portion of the underlying bitmap inside a circle/rectangle shape.

- **Interaction**
  - Create a RegionCaptureView control that captures mouse/touch events, draws handles for resizing/moving shapes and supports multi-shape editing.
  - Implement keyboard shortcuts to switch between tools (e.g., arrow key, text tool, effect tool) and to cancel or confirm actions.
  - Provide a shape toolbar (in XAML) to select drawing, effect and tool types; include controls for changing colours, thicknesses, font and effect parameters.

- **Shape management**
  - Port ShapeManager to manage a collection of shapes, ordering, selection and removal. Provide undo/redo functionality and support copy/paste of shapes.
  - Persist user preferences (colours, sizes, arrow head direction, blur radius, pixel size etc.) in AnnotationOptions analogues and load/save them on application start.

- **Image insertion and sticker support**
  - Implement file picker integration for inserting external images (DrawingImage and DrawingImageScreen).
  - Provide a sticker palette and cursor stamp support; allow users to import custom stickers.

- **Smart eraser**
  - Analyse SmartEraserDrawingShape and implement a mask-based eraser that restores the original screenshot pixels under the eraser path.

- **Crop and cut-out tools**
  - Implement crop and cut-out tools that modify the canvas bitmap and adjust existing shapes accordingly.
  - Ensure non-destructive editing by allowing the user to revert or adjust crop boundaries.

- **Region capture integration**
  - Replace RegionCaptureForm with a new RegionCaptureWindow built in Avalonia. Handle monitor selection, last region recall, screen colour picker, ruler and other capture modes defined in RegionCaptureMode and RegionCaptureAction.
  - Provide a seamless workflow from capture ? annotate ? upload, integrating with existing task runners.

- **Cross-platform considerations**
  - Avoid any reference to System.Drawing or WinForms; rely solely on Avalonia and Skia for drawing.
  - Use platform abstraction services (e.g., clipboard, file dialogs) via the core platform layer defined in AGENTS.md.
  - Ensure equal behaviour on Windows, macOS and Linux; implement stubs or fallbacks where OS-specific features cannot be supported.

- **Testing**
  - Develop unit and UI tests for each shape type and effect to verify correct rendering, hit-testing, configuration persistence and behaviour in undo/redo operations.
  - Provide manual test scripts to validate annotation workflows across supported platforms.

