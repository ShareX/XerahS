## License Header Requirement

When creating or editing C# files (`.cs`) in this repository, include the following license header at the top of the file (tailored for the Avalonia implementation):

```csharp
#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

- Always summarize code changes in the final response, and use that summary when performing `git push` after each code update.

## Semantic Versioning

- Use semantic versioning for all versioned artifacts: MAJOR.MINOR.PATCH.
- MAJOR: breaking changes to public APIs, data contracts, or user-visible behavior.
- MINOR: backward-compatible features or enhancements.
- PATCH: backward-compatible bug fixes or small corrections.
- Pre-release and build metadata follow SemVer 2.0.0 conventions.

## Purpose

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

## Testing

- If you modify executable code, suggest relevant tests.
- If tests are added, align them with current test conventions.

## Documentation

- Update or add docs when behavior or usage changes.
- Keep filenames and headings descriptive and stable.

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

## Future TODO - Uploaders Plug-in Architecture

- Extract common abstractions (e.g., `GenericUploader`, `UploaderService<T>`) into a core library referenced by the app and plug-ins.
- Split each uploader into its own class library (one per `FileDestination` entry) so `ShareX.UploadersLib.<Uploader>.dll` can be built individually.
- Replace in-assembly reflection with a dynamic plug-in loader that scans `C:\Users\<your-username>\AppData\Local\ShareX.Avalonia\Plugins` for uploader assemblies.
- Remove hard-coded uploader enums and update configuration UI to list plug-ins discovered at runtime.
- Update build/deployment scripts to package plug-in DLLs separately and load them on demand.

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
- [ ] AppVeyor.cs
- [ ] AppVeyorUpdateChecker.cs
- [ ] BlackStyleCheckBox.cs
- [ ] BlackStyleProgressBar.cs
- [ ] Canvas.cs
- [ ] CaptureHelpers.cs
- [ ] ClipboardHelpers.cs
- [ ] ClipboardHelpersEx.cs
- [x] ClipboardFormat.cs
- [x] CMYK.cs
- [x] ColorBgra.cs
- [ ] ColorBox.cs
- [ ] ColorEventHandler.cs
- [ ] ColorMatrixManager.cs
- [ ] ColorPicker.cs
- [x] ColorPickerOptions.cs
- [ ] ColorSlider.cs
- [ ] ConvolutionMatrixManager.cs
- [x] ConvolutionMatrix.cs
- [ ] CursorData.cs
- [ ] CustomVScrollBar.cs
- [x] DebugTimer.cs
- [ ] DesktopIconManager.cs
- [x] DPAPI.cs
- [x] DPAPIEncryptedStringPropertyResolver.cs
- [x] DPAPIEncryptedStringValueProvider.cs
- [x] WritablePropertiesOnlyResolver.cs
- [ ] DWMManager.cs
- [x] Emoji.cs
- [x] EnumDescriptionConverter.cs
- [x] EnumExtensions.cs
- [x] EnumInfo.cs
- [x] EnumProperNameConverter.cs
- [x] EnumProperNameKeepCaseConverter.cs
- [x] Extensions.cs
- [x] ExternalProgram.cs
- [x] FastDateTime.cs
- [ ] FFmpegUpdateChecker.cs
- [x] FileDownloader.cs
- [x] FixedSizedQueue.cs
- [x] FileHelpersLite.cs
- [x] FontSafe.cs
- [x] FPSManager.cs
- [x] GifClass.cs
- [x] GitHubUpdateChecker.cs
- [ ] GitHubUpdateManager.cs
- [x] GradientInfo.cs
- [x] GradientStop.cs
- [x] GraphicsPathExtensions.cs
- [x] GraphicsQualityManager.cs
- [x] GrayscaleQuantizer.cs
- [x] Helpers.cs
- [ ] HotkeyInfo.cs
- [x] HSB.cs
- [x] HttpClientFactory.cs
- [ ] ImageFilesCache.cs
- [x] Logger.cs
- [ ] InputHelpers.cs
- [ ] InputManager.cs
- [x] JsonHelpers.cs
- [ ] KeyboardHook.cs
- [x] KnownTypesSerializationBinder.cs
- [ ] ListExtensions.cs
- [x] MaxLengthStream.cs
- [x] MimeTypes.cs
- [x] MutexManager.cs
- [x] MyColor.cs
- [x] MyColorConverter.cs
- [ ] NativeConstants.cs
- [ ] NativeEnums.cs
- [ ] NativeMessagingHost.cs
- [ ] NativeMethods.cs
- [ ] NativeMethods_Helpers.cs
- [ ] NativeStructs.cs
- [x] OctreeQuantizer.cs
- [x] PaletteQuantizer.cs
- [x] PingHelper.cs
- [x] PingResult.cs
- [x] Point.cs
- [x] PointF.cs
- [x] PointInfo.cs
- [ ] PrintHelper.cs
- [ ] PrintSettings.cs
- [ ] PrintTextHelper.cs
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
- [ ] ShareXTheme.cs
- [ ] ShortcutHelpers.cs
- [ ] SingleInstanceManager.cs
- [x] StringCollectionToStringTypeConverter.cs
- [x] StringLineReader.cs
- [ ] TaskbarManager.cs
- [x] TaskEx.cs
- [x] ThreadWorker.cs
- [ ] TimerResolutionManager.cs
- [x] UnsafeBitmap.cs
- [x] UpdateChecker.cs
- [x] URLHelpers.cs
- [x] Vector2.cs
- [x] WindowState.cs
- [ ] WshShell.cs
- [x] XmlColor.cs
- [x] XmlFont.cs
- [ ] XMLUpdateChecker.cs

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
