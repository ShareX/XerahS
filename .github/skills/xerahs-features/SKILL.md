---
name: ShareX Feature Specifications
description: Detailed specifications for Uploader Plugin System and Annotation Subsystem features
---

## Uploader Plugin System Specification

### Architecture Overview

**Multi-Instance Provider Catalog:**
- Renamed `IUploaderPlugin` → `IUploaderProvider` with multi-category support
- Separated provider (type) from instance (configured occurrence)
- `ProviderCatalog`: Static registry for provider types
- `InstanceManager`: Singleton for instance lifecycle, persistence, default selection
- Models: `UploaderInstance`, `InstanceConfiguration` with JSON serialization
- UI: `ProviderCatalogViewModel`, `CategoryViewModel`, `UploaderInstanceViewModel`
- Full CRUD operations: Add from catalog, duplicate, rename, remove, set default
- Cross-category support: Same provider (e.g., S3) can serve Image + Text + File

**Providers Reference:**
- `ImgurProvider`: Supports Image + Text categories
- `AmazonS3Provider`: Supports Image + Text + File categories

**Persistence:** `%AppData%/ShareX.Avalonia/uploader-instances.json`

### Dynamic Plugin System

**Architecture:**
- **Pure Dynamic Loading**: No compile-time plugin references in host app.
- **Isolation**: `PluginLoadContext` (AssemblyLoadContext) for each plugin.
- **Shared Dependencies**: Framework assemblies (Avalonia, Newtonsoft.Json) shared from host context.
- **Static Lifecycle**: Static `PluginLoader` prevents premature GC of contexts.
- **Dynamic UI**: Plugins expose configuration views via `IUploaderProvider.GetConfigView`.

**Components:**
- `PluginDiscovery`: Scans `Plugins/` folder for `plugin.json` manifests.
- `PluginLoader`: Loads assemblies and instantiates providers.
- `ProviderCatalog`: Central registry for both built-in and dynamic providers.

**Plugin Examples:**
- `ShareX.Imgur.Plugin`: Image uploading with OAuth2.
- `ShareX.AmazonS3.Plugin`: S3 bucket uploads (Image/Text/File).

### File-Type Routing (Planned)

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

### Full Automation Workflow (Path B - Planned)

**Goal**: Complete end-to-end automation matching ShareX feature parity

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

## Annotation Subsystem Specification

### Phase 1: Core Annotation Models (Completed)

**Project:** `ShareX.Avalonia.Annotations`
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

### Phase 2: Canvas Control and Full Feature Set

**Implement the annotation subsystem for ShareX.Avalonia, replacing WinForms and System.Drawing with Avalonia and Skia. All features listed in the ShapeType enum must be available.**

#### Design Core Abstractions

- Define a BaseShape in ShareX.Avalonia with properties for position, size, colour, border thickness and hit-testing.
- Create Avalonia equivalents for all ShapeType values (RegionRectangle, RegionEllipse, RegionFreehand, DrawingRectangle, DrawingEllipse, DrawingFreehand, DrawingFreehandArrow, DrawingLine, DrawingArrow, DrawingTextOutline, DrawingTextBackground, DrawingSpeechBalloon, DrawingStep, DrawingMagnify, DrawingImage, DrawingImageScreen, DrawingSticker, DrawingCursor, DrawingSmartEraser, EffectBlur, EffectPixelate, EffectHighlight, ToolSpotlight, ToolCrop, ToolCutOut). Each must subclass BaseShape and implement custom rendering logic.
- Expose shape-specific properties such as arrow-head direction, blur radius, pixel size and highlight colour with appropriate defaults.

#### Rendering and Effects

- Replace GDI+ drawing with Avalonia's DrawingContext and Skia. Implement vector drawing for shapes and text. For effects (blur, pixelate, highlight), use Skia image filters or custom pixel shaders to apply the effect only within the shape bounds.
- Implement the spotlight tool by darkening the canvas outside the selected rectangle and optionally applying blur/ellipse.
- Implement magnify drawing by rendering a zoomed portion of the underlying bitmap inside a circle/rectangle shape.

#### Interaction

- Create a RegionCaptureView control that captures mouse/touch events, draws handles for resizing/moving shapes and supports multi-shape editing.
- Implement keyboard shortcuts to switch between tools (e.g., arrow key, text tool, effect tool) and to cancel or confirm actions.
- Provide a shape toolbar (in XAML) to select drawing, effect and tool types; include controls for changing colours, thicknesses, font and effect parameters.

#### Shape Management

- Port ShapeManager to manage a collection of shapes, ordering, selection and removal. Provide undo/redo functionality and support copy/paste of shapes.
- Persist user preferences (colours, sizes, arrow head direction, blur radius, pixel size etc.) in AnnotationOptions analogues and load/save them on application start.

#### Image Insertion and Sticker Support

- Implement file picker integration for inserting external images (DrawingImage and DrawingImageScreen).
- Provide a sticker palette and cursor stamp support; allow users to import custom stickers.

#### Smart Eraser

- Analyse SmartEraserDrawingShape and implement a mask-based eraser that restores the original screenshot pixels under the eraser path.

#### Crop and Cut-Out Tools

- Implement crop and cut-out tools that modify the canvas bitmap and adjust existing shapes accordingly.
- Ensure non-destructive editing by allowing the user to revert or adjust crop boundaries.

#### Region Capture Integration

- Replace RegionCaptureForm with a new RegionCaptureWindow built in Avalonia. Handle monitor selection, last region recall, screen colour picker, ruler and other capture modes defined in RegionCaptureMode and RegionCaptureAction.
- Provide a seamless workflow from capture → annotate → upload, integrating with existing task runners.

#### Cross-Platform Considerations

- Avoid any reference to System.Drawing or WinForms; rely solely on Avalonia and Skia for drawing.
- Use platform abstraction services (e.g., clipboard, file dialogs) via the core platform layer defined in AGENTS.md.
- Ensure equal behaviour on Windows, macOS and Linux; implement stubs or fallbacks where OS-specific features cannot be supported.

#### Testing

- Develop unit and UI tests for each shape type and effect to verify correct rendering, hit-testing, configuration persistence and behaviour in undo/redo operations.
- Provide manual test scripts to validate annotation workflows across supported platforms.
