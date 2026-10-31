# Changelog

All notable changes to XerahS will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):

- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches

---

## v0.33.5

Includes the macOS startup and update-dialog fixes from v0.33.4. Both native macOS launch checks passed for that build; its final Chocolatey job hit GitHub's anonymous API rate limit.

### Build

- **Release reliability**: Authenticate GitHub metadata requests during Chocolatey packaging so shared runner API quotas do not prevent publication of the package.

---

## v0.33.4

### Fixes

- **macOS startup**: Keep the tray's native menu instance stable when initializing or rebuilding its items. This fixes the v0.33.3 startup crash reporting "The menu being updated does not match" and retains UI-thread updates for Linux.
- **Update dialogs**: Keep the update prompt opaque while the image editor is open. App dialogs use the shell's dialog surface; editor dialogs retain their own overlay.

### Build

- **macOS verification**: Start the packaged app twice on the native CI runner with an isolated profile. Require tray initialization, loaded plugins and a live process before publishing the archives.

---

## v0.33.3

### Features

- **Privacy mode**: Review sensitive content detected by the configured AI provider before an image upload, apply redaction, or open the annotation editor for manual changes.
- **Recording controls**: Stop an active screen recording with Enter.

### Fixes

- **Annotation workflows**: Save, copy and upload the flattened annotated image when continuing from the editor. Cancel and window close now stop the workflow before its save, copy and upload tasks.
- **Editor colors**: Preserve red, blue and alpha channels when converting between Avalonia and Skia images, including macOS RGBA images.
- **Linux startup and logout**: Open the clipboard viewer safely when the main window is hidden, avoid activating background opens, and disable X11 session registration that can block logout.
- **Network Monitor**: Make background monitoring opt-in, with settings and manual-session controls that stop probes when monitoring is disabled.
- **Wayland recording**: Drain recorder output pipes, preserve playable fragments after interruption, and report forced shutdown or unusable output as failure before upload. Retain fatal errors through concurrent stop and completion paths.
- **Windows cursor recovery**: Keep the recovery marker when cursor restoration fails so exit or next startup can retry.
- **Upload reliability**: Handle moved watch-folder screenshots and let the CLI use file destinations that also accept images.
- **Cloud and iOS**: Improve Cloud sign-in and TOTP setup, iOS credential storage, share-extension history and import handling, destination imports and uploaded file content types.

### Build and downloads

- **FFmpeg**: Use verified BtbN FFmpeg 9.0 stable builds for managed downloads.
- **Arch Linux**: Maintain the AUR recipe and metadata in the repository, enforce pinned recursive dependencies, and verify packaging in a clean Arch CI container. Bound build-server shutdown so packaging can finish after a stalled shutdown client.
- **Download channels**: Explain the separate ShareX upstream and KovaForge release histories and update sources.

### Known limitations

- The original intermittent Wayland/PipeWire stall and Windows capture crash still need reproduction; this release fixes recording failure handling and cursor recovery without claiming those underlying failures are eliminated.
- Native macOS color and KDE/XFCE logout confirmation remain outstanding. The public AUR listing and separately hosted website require their own updates.
- iOS and Cloud changes are source changes; this desktop release does not publish an iOS app or deploy Cloud.

---

## v0.33.1

### Features
- **Windows recording (XIP0089)**: Record H.264/AAC through the native GPU pipeline, with system audio, microphone audio or both. Select audio devices and gains, record secondary displays and spanning regions, and choose direct window or popup-inclusive capture.
- **Windows recording**: Pause and resume within one recording, exclude paused time from the media timeline, and move a paused region between displays while retaining its output size. Native settings expose hardware-encoder requirements, low latency and capture-border preferences.
- **Recording verification**: Inspect existing recordings without loading application settings, or record and verify a workflow through the CLI. JSON reports check codecs, audio formats, stream durations and active recording time.

### Fixes
- **Recording reliability**: Complete workflows when a captured window closes or changes size, release failed native sessions before FFmpeg fallback, preserve existing output files, and handle repeated stop and concurrent abort safely. Runtime audio failure warns and continues video with silence.
- **Windows audio fallback**: Map selected microphone endpoints to FFmpeg device names and mix both requested audio sources instead of dropping the microphone when system audio is enabled.
- **Linux Wayland recording**: Record the selected desktop, window or region instead of a webcam, and pack raw I420 frames correctly for FFmpeg to avoid corrupted output.

### Build
- **Packaging and verification**: Preserve Linux executable permissions regardless of output path. Add Windows x64/ARM64 compilation and Windows/Linux recording regression checks, including rejection of empty test runs.

### Known limitations
- **Windows recording**: Live GPU, device-removal and 30-minute audible A/V acceptance checks remain outstanding. HDR, camera overlays and process-specific loopback are deferred. FFmpeg system-audio fallback still requires Stereo Mix; hardware-encoder requirements apply to the native backend.

---

## v0.29.1

### Features
- **Annotations**: implement effect shapes, freehand tools, and serialization infrastructure; Implement Phase 2 Annotation Models
- **Automation**: implement Path A quick automation workflow\n\n- Created WorkflowTask for minimal automation (capture → upload → clipboard)\n- Added UploaderExtensions.UploadAsync() async wrapper\n- Integrated with ProviderCatalog + InstanceManager APIs\n- Workflow: CreateImageUploadTask → ExecuteAsync → UploadAsync → CopyURLToClipboard\n- Saved AUTOMATION_WORKFLOW_PLAN.md to docs/ for Path B reference\n\nPath A delivered:\n- Basic WorkflowTask structure\n- Upload provider wiring  \n- Simple URL to clipboard automation\n- Hotkey → Capture → Upload → URL flow ready\n\nPath B (deferred - documented in AGENTS.md):\n- Full task queue with concurrency\n- AfterCaptureTasks/AfterUploadTasks pipelines\n- Progress tracking UI\n- Upload history\n- Retry logic\n\nBuild: 0 errors, 578 warnings (platform compatibility - pre-existing)
- **Core**: Achieve 100% Platform Abstraction in Core; add about view and update links; and related changes
- **Editor**: Add ShareX.Editor DLL with core shapes and services; complete annotation phase 2 - add keyboard shortcuts for all tools\n\n- Implemented keyboard shortcuts: V(Select), R(Rectangle), E(Ellipse), A(Arrow), L(Line), P(Pen), H(Highlighter), T(Text), B(SpeechBalloon), N(Number), C(Crop), M(Magnify), S(Spotlight), F(FX Panel)\n- Enhanced keyboard event handling with proper e.Handled and early returns\n- Completes Batch 5: Polish & Data Factory\n- All Annotation Phase 2 work is now complete (100%); Integrate ShareX.Editor with ShareX.Avalonia.UI
- **Export**: implement copy and save-as enhancements\n\n- Copy: Convert Avalonia Bitmap to PNG, save to temp file, copy file to clipboard\n- SaveAs: Implement file picker dialog with PNG/JPEG/BMP format selection\n- Added CopyRequested and SaveAsRequested events for View delegation\n- Both commands now support flattened images with annotations\n- Build: 0 errors (3 obsolete API warnings are non-blocking)
- **Hotkeys**: implement persistence using HotkeysConfig and deduplicate HotkeySettings model
- **Implement SIP0016 Stage 3**: macOS Capture via CLI Wrapper
- **IOS Core**: domain models, Paths, Settings/Queue/History repos, S3 and custom uploaders, UploadQueueWorker
- **IOS Full UI**: Loading, Upload, History, Settings hub, S3 and Custom uploader config, navigation
- **Linux**: implement native Wayland global shortcuts portal (Fix 4)
- **Macos**: implement native build integration and single-file app bundle packaging
- **Overlay**: Enable AnnotationToolbar in Region Capture Overlay (XIP-0023); Prepare OverlayWindow layout for AnnotationToolbar (XIP-0023)
- **Packaging**: enable self-contained windows build and sync plugin logic; finalize packaging scripts and configuration
- **Region Capture**: add CaptureTransparent option for frozen screenshot background
- **Ui**: add error dialog for clipboard failures\n\n- Added ShowErrorDialog event to MainViewModel\n- Implemented modal dialog in EditorView\n- Clipboard errors now show in a dialog instead of just status bar\n- Build: 0 errors, 9 warnings

### Fixes
- **Abstractions**: add default impl for IHotkeyService.ShowInteractiveConfigurationAsync
- **Arch**: switch to direct project references for plugins to resolve assembly loading conflicts
- **Build**: Fix compilation and standardize Windows TFM
- **Bump ImageEditor Submodule**: Highlight Skia rendering overhaul
- **Clipboard**: add retry logic and proper error handling\n\n- Windows clipboard can fail if another app is using it\n- Implement 5 retry attempts with 100ms delays\n- Throw exceptions instead of swallowing them silently\n- Better error messages for debugging\n- Should fix copy button not working on Windows; implement direct Windows API via P/Invoke\n\n- Replace System.Windows.Forms.Clipboard with direct User32.dll calls\n- Fixes 'Windows.Win32.Foundation.AgileComPointer' method not found error\n- Uses OpenClipboard, SetClipboardData, CloseClipboard APIs\n- Converts Image to DIB (Device Independent Bitmap) format\n- Allocates global memory with GlobalAlloc/GlobalLock\n- Includes retry logic for clipboard contention\n- Memory management: clipboard takes ownership on success\n- Build: 0 errors, 578 warnings (platform compatibility - pre-existing)
- **Core**: Accent scrollbar thumbs app wide; Accept desktop Bearer tokens on Cloud account API; and related changes
- **Editor**: Update AXAML namespace references to ShareX.Editor
- **Hotkeys**: prevent duplication of Job property in JSON serialization
- **ImageEditor**: Highlight annotation fill and move bugs
- **Linux**: add PhysicalVirtualScreenBoundsForCrop and PhysicalRectForCrop to CaptureOptions; add region capture diagnostics; and related changes
- **Linux — Hotkeys (XIP0044)**: fix(linux/hotkeys): retry portal bind after window opens (XIP0044 Fix 5)
- **Linux — Platform (XIP0044)**: fix(linux): don't dispose debounce CTS synchronously in ScheduleRebind; add XIP0044
- **Linux/packaging**: symlink /usr/bin/xerahs, fix StartupWMClass; add BindShortcuts diagnostics
- **Linux/x11**: use physical pixel origin for overlay Window.Position on X11
- **Recording**: Correct MediaFoundation VTable indexes [2026-01-10 08:42]; Correct SetCurrentLength VTable index [2026-01-10 09:05]; and related changes
- **Release**: redirect find_tag_run_id status to stderr; resolve tag name collision in bump script
- **Ui**: correctly save task settings (file naming pattern) to configuration; Minimize Tools menu by default in MainWindow; and related changes
- **Update Changelog Script**: ensure entries array has Count for single-category
- **Windows**: Marshal hotkey registration to message loop thread to resolve Win32 errors 1408/1419

### Refactor
- **Clipboard**: use MemoryStream instead of temp files\n\n- Replace temp file approach with direct PNG byte array in clipboard\n- Use 'image/png' MIME type via DataObject.Set()\n- No temp files created, cleaner implementation\n- 10 warnings (2 obsolete API warnings for DataObject/SetDataObjectAsync)\n- Build: 0 errors; use native OS clipboard via PlatformServices\n\n- Convert Avalonia Bitmap to System.Drawing.Image for OS compatibility\n- Use PlatformServices.Clipboard.SetImage() for native clipboard format\n- Ensures proper paste functionality in native apps (Paint, Word, etc.)\n- Removed custom MIME type approach that may not work cross-platform\n- Build: 0 errors, 1230 warnings (platform compatibility - pre-existing)
- **Core**: Add modular Linux capture contracts and providers; Add polymorphic uploader config pilot; and related changes
- **Editor**: Remove redundant Annotations and ImageEffects projects; Restructure ShareX.Editor per SIP0014 with MVVM layout
- **Indexer**: collapse async adapters; externalize html styles; and related changes
- **Ui**: Add accessibility properties to RecordingBorderWindow; Redesign AfterCaptureWindow with Grid layout and consistent styling; and related changes

### Build
- **Core**: Add build system documentation and update ISS paths; Add changelog update automation script; and related changes
- **Pre Commit**: validate license headers in Swift files; update githooks docs
- **Recording**: Add logging to MediaFoundationEncoder and move SetCurrentLength [2026-01-10 09:12]
- **Tool**: Update audit tool to detect Flyout wiring

### Documentation
- **Agents**: add full automation workflow TODO (Path B)\n\n- Documented complete automation architecture in AGENTS.md\n- Added AfterCaptureTasks and AfterUploadTasks specifications\n- Noted Path A (minimal) vs Path B (full) implementation strategy\n- Estimated effort: 1,200 LOC, 8-12 hours for Path B\n- Updated task.md to track automation phases
- **Core**: Add .sxadp file association implementation plan; Add blog scheduling (Cursor Automations, cron, run-daily-draft); and related changes
- **IOS README**: features, share-extension note
- **Linux — Documentation (XIP0051)**: Add XIP0051 Linux selector preferences
- **Sip0014**: Update with implementation status and WinForms guide
- **SIP0017**: Add lessons learnt regarding Windows TFM and CsWinRT; Add milestone for Stage 1 zero build errors
- **Xip Sync**: update backup path from tasks/ to docs/proposals/xip/

### Testing
- **Core**: Guardrail and test-coverage improvements (Headless.NUnit, McpServer.Tests, FFmpeg regression tests)

### Performance
- **Core**: faster overlay and smoother crosshair on Linux (region capture); Optimize Modern Capture (~5x faster) & Move debug logs. - Implemented batched initialization for DXGI capture to reduce latency on multi-monitor setups. - Changed region capture debug log location to 'Debug/RegionCapture' subfolder.; and related changes

### Changed
- **Add Drawing Effects**: background, border, and helpers
- **Add XerahS.DestinationsPluginSdk**: lightweight plugin contracts (interfaces + DTOs)
- **Bump VideoEditor Submodule**: fix button theme isolation; fix ReactiveUI main thread scheduler
- **Close To Tray**: ensure tray visible when hiding; clarify comments
- **Core**: .gitignore update; [Build] Add Android mobile build infrastructure; and related changes
- **CP03**: Add After Capture Tasks UI to Task Settings
- **Editor Baseline**: reapply main window chrome updates; restore editor and annotations to 3babd33b
- **Finalize Annotation Editor**: Keyboard Shortcuts, Cursor Polish, Focus Handling
- **Fix Annotation Resizing**: Enable OverlayCanvas hit testing and restore e.Source logic
- **Fix Arrow Geometry**: update ImageEditor submodule and fix AnnotationVisualFactoryTests
- **Fix Build**: Add CommunityToolkit.Mvvm and fix project references
- **Fix CS0414 And SYSLIB0013**: Remove unused field and replace obsolete Uri API
- **Fix CS8625/CS8600**: Make UploadResult and Tags properties nullable - reduced errors from 186 to 165
- **Fix Linux Audio Recording**: correct FFmpeg args, add wf-recorder audio, PulseAudio monitor detection
- **Fix ScreenRecorder**: Toast, Hotkey Toggle, Dest Persistence
- **Implement Crop Tool**: Drag to select, Enter to crop
- **Implement Export Logic**: Copy to Clipboard and Quick Save
- **Improve Pre Commit Hooks**: Use dynamic year detection
- **Linux — Hotkeys (XIP0044)**: Update XIP0044-linux-global-hotkeys-not-firing-when-app-backgrounded.md
- **Linux — Hotkeys (XIP0046)**: Create XIP0046-linux-portal-and-hotkey-issues.md
- **Linux — Mixed-DPI (XIP0047)**: XIP0047: Linux region capture DPI and performance — summary and attempts
- **Linux — Platform (XIP0029)**: Renumber XIP files: Wayland fix as XIP0029, Mobile as XIP0030
- **Linux — Platform (XIP0035)**: [XIP0035] Refactor Extract CLI capture executor; thin LinuxScreenCaptureService coordinator
- **Linux — Platform (XIP0036)**: [XIP0036] Move XerahS.Platform.Linux to src/platform
- **Linux — Platform (XIP0046)**: [Fix] Add KDE Spectacle and GNOME region capture fallbacks on Wayland (XIP0046-C)
- **macOS — Platform (XIP0036)**: [XIP0036] Move XerahS.Platform.MacOS to src/platform
- **Mobile.Kt**: accept video share (video/*) for MP4 and other video files; S3 settings match Ava (custom domain, signed payload, public ACL); no auto-capitalise for bucket and custom domain; and related changes
- **Mobile.Swift**: S3 custom domain/signed/public ACL, copy-to-clipboard feedback, video share
- **Phase 1**: XerahS.Mobile.Kt - native Kotlin Android shell under src/
- **Phase 10**: Polish - clipboard + Toast for Copy URL/Error
- **Phase 2**: Data layer - HistoryRepository, QueueRepository, Paths init
- **Phase 3**: Refactor ViewModels to abstract dialogs and ensure MVVM separation; Upload pipeline - S3, custom .sxcu, queue worker
- **Phase 4**: Upload screen - ViewModel, UI, share wiring
- **Phase 5**: History screen - ViewModel + UI
- **Phase 6**: Settings hub - list destinations, S3/Custom nav
- **Phase 7**: S3 config screen - form, validation, persistence
- **Phase 8**: Custom uploader config screen - CRUD .sxcu
- **Phase 9**: Share intent - MainActivity filters, URI to cache, navigate
- **Port Helper Classes**: MathHelpers, URLHelpers, Vector2, GraphicsExtensions, and update Helpers; URLHelpers, Helpers, RandomCrypto, FileHelpers methods. Resolve namespace conflicts.
- **Refactor Settings UI**: App, Task, Hotkey layout; Feature: Hotkey list UI
- **Region Capture**: Linux portal crop, milestones, overlay focus, known issues
- **Rewrite XIP0030**: mobile support via Avalonia Mobile
- **SIP0017**: Complete Stage 1 MVP with FFmpeg fallback implementation; Complete Stage 2 Window & Region Parity implementation; and related changes
- **Update AGENTS.md Gap Report**: mark 16 existing utility classes as completed (~28% progress)
- **Update Docs**: Correct SDK to .NET 9.0, update task progress
- **Update FAQ.md**: Reorder questions and add XerahS vs ShareX comparison table
- **Update ShareX.ImageEditor**: ShareX.ImageEditor submodule updates
- **Xerahs Bugfix**: drain 1 false-positive queue item; drain 3 false-positive queue items; and related changes
- **Xerahs Changelog**: document v0.29.1 release notes (Declan/Mikhail lineage)
- **Xerahs Review**: 0-candidate ingest (all 375 findings gated); 1 candidate ingested (CaptureCommandPalette RegisterHotkey race); and related changes
- **XerahS.Mobile.Kt**: build fixes, Gradle wrapper, .gitignore for Kotlin/Android
- **XIP0036**: staged implementation and namespaces out of scope
- **XIP0052**: Complete agentic refactoring — DI, MVVM, monolith decomposition; Migrate to Microsoft.Extensions.DependencyInjection

---

## v0.29.0

### Features
- **Tools**: Add Network Monitor with connect/disconnect history, latency chart, and selectable time ranges

---

## v0.28.32

### Features
- **Windows — Portable**: Ship `XerahS-{version}-win-x64-portable.zip` and `XerahS-{version}-win-arm64-portable.zip` on tagged releases, with `portable.txt` beside `XerahS.exe` so settings stay next to the extract folder.

### Fixes
- **Linux — RPM**: Recreate the `omaxerahs` `/usr/bin` symlink after copying the payload so rpmbuild no longer fails with "File exists" on Ubuntu hosts.
- **Linux — Image Editor**: Embed English `Strings.resx` under `ShareX.ImageEditor.Localization.Strings` so startup no longer throws `MissingManifestResourceException` (#283).

---

## v0.28.2

### Features
- **After Capture**: Choose Copy Image, Copy File Path, or Copy URL as the goal. Required flags (save, upload, clipboard) are selected automatically; non-conflicting extras such as save, annotate, OCR, and URL follow-ups stay optional.

### Fixes
- **Destinations**: Opening Destination Settings no longer crashes when a plugin compiled against the previous `IProviderContext` contract reads secrets (`MissingMethodException` on `get_Secrets`).

---

## v0.28.1

### Features
- **Destinations**: Add an async plugin upload contract (`IUploadHandler`) so destinations can take cancellation and progress without inheriting the legacy `HttpWebRequest` engine. Nextcloud, Immich, and XBackBone implement it; custom uploaders and remaining destinations are adapted.
- **HTTP**: Pool shared `HttpClient` instances with `SocketsHttpHandler`, proxy-key recycle, and infinite timeout for large uploads/downloads. Plugins resolve the factory at request time so proxy changes do not leave a disposed client.
- **Plugins**: Optional host-rendered config schema and injectable `IProviderCatalog`. Manifest capability flags (`supportsCancellation`, `supportsProgress`, `supportsResume`) are additive.

### Fixes
- **Cloud**: Accept desktop Bearer tokens on the account API and stamp OAuth JWTs with `aal2` when MFA is verified (staging).

---

## v0.28.0

### Features
- **Linux — Distro repos (#253)**: Add first-party Launchpad PPA, Fedora COPR, and openSUSE OBS publish support. `publish-release --publish-distro-repos` and the tag workflow upload when maintainer secrets are set, and skip a backend when they are not.

---

## v0.25.6

### Features
- **Linux**: Ship self-contained AppImage packages for linux-x64 and linux-arm64 (`XerahS-{version}-linux-{arch}.AppImage`) alongside tar.gz, deb, rpm, and Flatpak. Flatpak packaging is unchanged.

---

## v0.25.5

### Fixes
- **Linux ARM64 packaging**: Publish destination plugins one at a time so parallel `dotnet publish` races no longer drop plugin assemblies (Bitly failed the v0.25.4 arm64 build).

---

## v0.25.4

### Fixes
- **Start minimized to tray**: Honor the Application Settings checkbox on launch, including Debug builds. The main window no longer opens normally when **Start minimized to tray** is enabled.

---

## v0.25.3

### Features
- **Updates**: Add an **Any source** option under Pre-release source. When selected, XerahS checks both ShareX and KovaForge and installs the newest usable pre-release.

---

## v0.25.2

### Features
- **Image Editor**: Localize editor UI (28 languages); smart-eraser edge-matching fills; extract built-in toolbars and host toolbar chrome from ShareX through `ebcee2a63`

### Fixes
- **GitHub Gist**: Reject invalid CustomURLAPI hosts so a crafted gist destination cannot point the uploader at an unexpected API.

---

## v0.25.1

- Keep the main window visible during hotkey and command-palette screenshots so XerahS itself can be captured. Navbar and tray captures still hide the window.

## v0.25.0

### Features
- **Core**: Add native XBackBone destination
- **Video Editor**: Seed watermarks from host image effects; close the advertised export path from the host; use the XerahS video editor title

---

## v0.24.25

### Security
- **Core**: Bump `SSH.NET` 2025.1.0 → 2026.0.0 to address CVE-2026-48798 / [GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284) (ScpClient arbitrary file write via server-controlled filenames in recursive directory download). Affects `XerahS.Uploaders` (SFTP/SCP) and `Ftp.Plugin` via central package management.

### Fixes
- **Core**: Honor minimized Windows startup; Notify on macOS capture denial; Preflight macOS screenshot permission

### Performance
- **Core**: Optimize searchable screenshot indexing

### Changed
- **KFIP0018**: x-twitter screen capture user needs research
- **Xerahs Bugfix**: drain stale submodule citation (queue 1 -> 0); empty-queue audit (00:05 AWST); and related changes
- **Xerahs Review**: backfill producer commit pointer (c40f60cd); daily producer tick (Nadia, 23:05 AWST); and related changes

---

## v0.24.23

### Features
- **Core**: Add Copy image / Copy file path quick actions in After Capture; Add global 'Disable notification window' checkbox (issue #252); and related changes

### Fixes
- **Core**: Bitly shortener surfaces network errors on UploadResult; Don't surface .deb / .rpm update assets inside a Flatpak sandbox; Synchronize HotkeySelectionControl static debug log
- **Dropbox OAuth**: stop forced refresh on bare refresh_token
- **PluginManifest**: ASCII-only PluginId whitelist
- **RandomCrypto**: prevent int.MaxValue overflow in Next range

### Documentation
- **Core**: Document fallback to plain git push when wrapper is missing

### Changed
- **Core**: [Fix] Suppress main window on SilentRun startup — raise Dispatcher priority to Send; [Nadia] xerahs-review producer tick 2026-08-01 23:10 AWST: ingest Bitly Plugin SendRequest error handling; and related changes
- **KFIP0017**: X/Twitter Capture Mode Suite — scroll capture, video clips & GIF conversion
- **Source Build Manifest**: Wayland-first finish-args, absolute OUTPUT_PATH
- **Xerahs Bugfix**: audit empty consumer queue; drain CliCaptureStrategy temp cleanup false-positive; and related changes
- **Xerahs Review**: fill in producer-tick commit SHA on tracker; fill in producer-tick commit SHA on tracker (23:11 AWST); and related changes

---

## [v0.24.12](https://github.com/ShareX/XerahS/releases/tag/v0.24.12)

### Changed
- No user-facing commits were detected in this range.

---

## [v0.24.11](https://github.com/ShareX/XerahS/releases/tag/v0.24.11)

### Changed
- No user-facing commits were detected in this range.

---

## [v0.24.10](https://github.com/ShareX/XerahS/releases/tag/v0.24.10)

### Documentation
- **Xip**: XIP, IEIP, and KFIP proposals and related documentation

### Changed
- **Xerahs Bugfix**: empty-queue audit (queue=0)

---

## v0.24.8

Special pre-release from `linux-hotkey-rewrite` (XIP0080): Linux global hotkeys via evdev, merged onto latest develop.

### Features
- **Linux — Hotkeys (XIP0080)**: New evdev-based global hotkey backend with key map, modifier tracking, and matching engine; wired into `LinuxPlatform` with GlobalShortcuts portal / X11 fallback when input access is unavailable.
- **Linux — Diagnostics**: `doctor --linux-input` reports evdev hotkey readiness (device access, group membership, and backend selection).
- **Linux — Packaging**: Ship udev rule and polkit policy so packages can grant `/dev/input/event*` access for global hotkeys; document setup steps.

### Tests
- **Linux — Hotkeys**: Unit coverage for evdev key map, modifier tracker, and hotkey matcher.

---

## v0.24.2

### Features
- **Core**: Add settings hub search with deep-link open; Add ShareX-style live search on main navigation

### Fixes
- **Core**: Auto-select first visible settings tab after search filter; Reject path traversal in ReClip set-watch-folder; and related changes

### Documentation
- **Core**: Add Cursor Cloud setup instructions to AGENTS.md; Drain stale and out-of-scope review candidates; and related changes

### Changed
- **Core**: [xerahs-review] Populate commit SHA on 23:00 AWST producer last_runs row; [xerahs-review] Populate commit SHA on 23:07 AWST producer last_runs row; and related changes
- **Xerahs Bugfix**: drain 10 stale/misleading queue items (pivot-only tick); drain 4 stale/noise items from next_candidates (skill v1.1.8); and related changes

---

## v0.23.141

### Features
- **Developers**: add clawpatch-parser dashboard

### Fixes
- **Core**: Constrain ImageEffectPreset.Effects deserialization to known ImageEffect types; Default screenshot subfolder pattern to year-month; and related changes
- **Immich**: clear stale SelectedAlbum when AlbumName diverges

### Build
- **Core**: Make release channel repo-scoped for ShareX vs KovaForge

### Documentation
- **Core**: Record dual-repo release targeting lessons

### Changed
- **Core**: [Fix] Treat .html/.htm as binary files — route to S3 not Paste2
- **Xerahs Bugfix**: backfill last_runs for 08:05 tick audit trail; update tracker and state JSON after batch

---

## v0.23.132

### Fixes
- **Linux — Flatpak startup crash (#270)**: The sandboxed build crashed ~1 second after startup on desktops with a StatusNotifierWatcher (KDE Plasma, XFCE, …). Avalonia's tray icon requests the `org.kde.StatusNotifierItem-{pid}-{id}` session-bus name, the Flatpak D-Bus proxy denied it, and the resulting `DBusErrorReplyException` escaped on the UI thread. The manifest now grants `--own-name=org.kde.*` so the tray icon works, and the dispatcher treats DBus/FreeDesktop integration failures as non-fatal (log-and-continue) so restricted sandboxes can never take the app down.
- **Linux — Startup diagnostics**: Startup failures no longer print misleading "Unable to connect to display server" / "run via flatpak-spawn" guidance for non-display errors; the real exception and the log file path are written to the console instead.
- **Linux — Flatpak plugin cleaner**: Skip plugin folder cleanup on read-only file systems (`/app`) with a single log line instead of a warning per bundled file.

---

## v0.23.131

### Fixes
- **macOS — Screenshot subfolder**: Onboarding was not syncing the "Create subfolder with today's date" toggle to the `UseSaveImageSubFolderPattern` setting; the checkbox in Settings always defaulted to `true`, overriding the user's choice on macOS.

---

## v0.23.130

### Features
- **Core**: Add checkbox to enable/disable screenshot subfolder pattern
- **Linux — Clipboard (XIP0079 P3)**: clipboard CLI probe, warnings, RPM Recommends, post-exit persistence
- **Linux — Hotkeys (XIP0079 P1)**: hotkey delivery diagnostics, settings banner, ConfigureShortcuts v2 gate
- **Linux — Notifications (XIP0079 P2)**: notification action buttons via portal and notify-send
- **macOS — App bundle (XIP0078 P1)**: render macOS Info.plist from template with stable bundle identity
- **macOS — Hotkeys (XIP0078 P4)**: Carbon RegisterEventHotKey backend, no Accessibility needed; SharpHook fallback
- **macOS — Packaging (XIP0078 P2)**: env-gated codesign/notarize/DMG pipeline, ad-hoc signing default in package-mac.sh
- **macOS — Permissions (XIP0078 P3)**: Screen Recording permission preflight, guided flow, macOS diagnostics
- **macOS — Window capture (XIP0078 P5)**: CGWindowList native window enumeration, wire sck_capture_window

### Fixes
- **Core**: Align OpenClaw manifest --json flags with pinned test contract; Keep Linux-only UI sources off macOS builds; skip X11 hotkey test on non-Linux; Remove superseded SettingsViewModel.LinuxClipboard partial
- **FileDownloader**: cancel outer loop on early HTTP EOF
- **Linux — Mixed-DPI (XIP0079 P4)**: cumulative mixed-DPI monitor layout for vertical stacks

### Refactor
- **macOS — ScreenCaptureKit (XIP0078 P8)**: rewrite ScreenCaptureKitStrategy against native bridge, fix stale ShareX.Avalonia namespaces

### Documentation
- **Core**: Add macOS paths to port-imageeditor skill; Blog drafts (2026 series, add/update); and related changes
- **Linux — Documentation (XIP0079 P5)**: Linux INSTALL parity, KNOWN_ISSUES update, implementation notes
- **macOS — Documentation (XIP0078)**: XIP0078 marked implemented with 2026-07-07 implementation notes; lessons-learnt entry

### Changed
- **Core**: mirror ExpireAfterDays<=0 clamp in ToJson (symmetry with LoadFromJson); round-trip share-security fields + SecurityMatches reconcile; and related changes

---

## v0.23.129

### Features
- **Linux — Hotkeys (XIP0079 P1)**: Surface global-hotkey delivery state in Settings → Hotkeys (portal-bound, focus-only X11 fallback, or unavailable) with a warning banner; gate “configure in DE settings” on GlobalShortcuts portal v2+.
- **Linux — Notifications (XIP0079 P2)**: After-upload toasts support real action buttons via portal `buttons` + `ActionInvoked`, with async `notify-send --action` fallback; notifications no longer block the UI thread.
- **Linux — Clipboard (XIP0079 P3)**: Probe `wl-copy` / `xclip` at startup; show settings and diagnostic warnings when CLI clipboard tools are missing; `.rpm` now recommends `wl-clipboard` and `xclip` (matching `.deb`); new **Persist clipboard after exit** setting (Wayland default) hands copies to `wl-copy` so paste survives app quit.
- **Linux — Mixed-DPI (XIP0079 P4)**: Fix vertically stacked monitors with different scale factors using cumulative physical layout; rollback via `XERAHS_LEGACY_MONITOR_NORMALIZER=1`.

### Fixes
- **Linux**: Cross-platform build fix — Linux-only UI partials excluded from macOS/Windows builds.

### Documentation
- **Linux (XIP0079 P5)**: Rewrite `developers/linux/INSTALL.md` for Ubuntu, Fedora, and Arch; update `KNOWN_ISSUES.md` Linux section; mark XIP0079 implemented with distro smoke-test checklist (manual VM runs pending).

---

## v0.23.128

### Features
- **macOS — App bundle (XIP0078 P1)**: Render `Info.plist` from template during app-bundle creation with stable bundle identity (`com.xerahs.app`).
- **macOS — Permissions (XIP0078 P3)**: Screen Recording permission preflight before capture, guided flow, and macOS diagnostic reporting.
- **macOS — Hotkeys (XIP0078 P4)**: Carbon `RegisterEventHotKey` backend (no Accessibility permission required); SharpHook remains as fallback (`XERAHS_MACOS_HOTKEY_BACKEND=sharphook` to roll back).
- **macOS — Window capture (XIP0078 P5)**: Native `CGWindowList` window enumeration and ScreenCaptureKit per-window capture (`sck_capture_window` wired end-to-end).
- **macOS — ScreenCaptureKit (XIP0078 P8)**: Rewrite capture strategy against native bridge; fix stale `ShareX.Avalonia` namespaces.

### Build
- **macOS (XIP0078 P2)**: Env-gated codesign, notarization, and DMG pipeline in `package-mac.sh`; ad-hoc signing default when no Apple Developer credentials are set.

### Documentation
- Mark XIP0078 implemented with 2026-07-07 implementation notes; lessons-learnt entry for verifying XIP claims against current source.

---

## v0.23.127

### Fixes
- **Tests**: Harden `AssistantHistoryServiceTests` teardown against flaky async cleanup.

### Plugins
- **Immich**: Round-trip share-security fields with `SecurityMatches` reconcile; mirror `ExpireAfterDays<=0` clamp in `ToJson`.

---

## v0.23.124

### Fixes
- **Core**: `FileDownloader` — cancel outer loop on early HTTP EOF instead of spinning until timeout.

---

## v0.23.121 / v0.23.120

### Changed
- Release version bumps only; no additional user-facing changes in these ranges.

---

## v0.23.119

### Changed
- **KFIP**: Add KFIP-0013 X/Twitter Smart Thumbnail Generation proposal.

---

## v0.23.118

### Documentation
- Blog drafts (2026-07-01 through 2026-07-05) and hourly-review sweep notes.

### Changed
- Hourly review sweeps (Wayland CLI capture routing, Immich album share, upstream merges).

---

## v0.23.117

Broad reliability, onboarding, CLI/OpenClaw, and platform-hardening release (aggregates work from v0.23.27 onward).

### Features
- **Capture & workflows**: Capture command palette, Send-to post-v1 policies, markdown directory index output, after-capture OCR-to-clipboard task, CLI `--randomize` upload naming.
- **Onboarding**: Wire welcome and OCR steps into the onboarding wizard; apply OCR language to default task settings.
- **CLI / OpenClaw**: Text and pipe uploads, bootstrap uploader JSON, manifest/runtime parity, plugin bundling for agent hosts.

### Fixes
- **MCP server**: History search query parsing, blob resource hardening, task identity race, thumbnail URI handling, stale-path diagnostics.
- **Linux**: Pipe-drain deadlocks across CLI tools, theme service, clipboard/monitor, input, screen capture, and PulseAudio; Oem102 backslash hotkey mapping; Wayland active-window routing for Sway; `.deb` recommends `wl-clipboard` and `xclip`.
- **macOS**: Clipboard file-path whitespace, dock hide for tray startup, upload file-picker fallback, update prompts, input/cursor helper deadlocks.
- **Uploaders & settings**: Default-instance cleanup on category change and remove; history backup toasts and failure diagnostics; settings backup retention and restore-from-zip.
- **Media / FFmpeg**: Path escaping, cancellation propagation, concat escape tests, thumbnail grid overflow guards, probe argument quoting.
- **Editor & history**: Sidecar save failure handling, annotation persist-after-continue, editor copy bitmap leak, history OCR index cleanup on delete.
- **OCR & onboarding**: Language refresh errors surfaced, regional defaults, null-selection guards, multi-language persistence.
- **Toasts & UI**: Multi-monitor toast positioning, fade resume after context-menu close, command palette keyboard selection.
- **Scrolling capture**: Guard `CurrentCapture` clear so closing an old window does not drop an active capture from a newer window.

### Build
- Bump Avalonia to 12.0.5 and SkiaSharp to stable 3.119.4; pin SQLite bundle packages; macOS `Info.plist` template and entitlements (prep for v0.23.128 wiring).

### Documentation
- Add macOS and Linux improvement plans (XIP0078, XIP0079), RELIABILITY-PLAN, KNOWN_ISSUES macOS section, XIP0080 evdev hotkeys proposal, KFIP0009/0010/0012, and 2026 blog-draft series.

---

## v0.23.107

### Changed
- No user-facing commits in this range.

---

## v0.23.105

### Features
- After-capture OCR clipboard task.

### Fixes
- CLI/OpenClaw manifest-vs-runtime parity; history backup user-visible toasts; MCP history search URI hardening; settings backup failure events.
- **Linux packaging**: `.deb` recommends `wl-clipboard` and `xclip`.

### Build
- macOS `Info.plist` template and hardened-runtime entitlements (not yet wired into packaging).

### Documentation
- Linux and macOS improvement plans; XIP0080; RELIABILITY-PLAN; KNOWN_ISSUES macOS updates; KFIP0010 scope review.

---

## v0.23.98

### Features
- CLI `--randomize` flag for upload naming (matches UI `%ra{10}` CDN-cache avoidance).

### Fixes
- **Core reliability wave**: FFmpeg path escaping and cancellation, FileDownloader chunked/early-EOF handling, uploader default-instance non-mutating reads, plugin version alignment, history/OCR index lifecycle, backup zip atomic replacement, MCP blob/URI handling, scrolling-capture lifecycle guard.
- **Linux**: Oem102 hotkey mapping, CLI runner and theme-service pipe deadlocks, Wayland active-window fallback, grim/slurp/grimblast stderr drain.
- **macOS**: Upload picker fallback, clipboard path whitespace, update prompt unblock, osascript cursor helper deadlock.
- **Platform services**: Linux input/screen capture and PulseAudio helper pipe drains; indexer enumeration exception guards.

### Documentation
- 2026-05/06 blog drafts, hourly-review tracker entries, CONTRIBUTING git-wrapper rules, FFmpeg Linux guidance.

---

## v0.23.27

### Features
- Capture command palette, Send-to post-v1 policies, markdown directory index output.

### Fixes
- **MCP**: History search parsing, blob hardening, task identity race, thumbnail resource URI.
- **CLI / OpenClaw**: Text upload, JSON validation diagnostics, bootstrap uploader JSON, bounded diagnostic keys.
- **Linux / macOS**: Clipboard stderr drain; macOS dock hide for tray; indexer long-path enumeration guards.
- **Editor / settings**: Sidecar dirty-state preservation, async settings save await, editor copy bitmap dispose, settings restore from backup zips.
- **OCR / onboarding**: Regional language matching, selection normalization, fallback language preservation.
- **Mobile**: File-scoped S3 config imports.

### Documentation
- XIP0057 implementation notes, 2026-05 blog drafts, XIP proposal status normalization.

### Changed
- Fedora VS Code updater script; command-palette minor release marker; Flathub v0.22.256 verification recorded.

---

## v0.22.239

### Fixes
- **CLI**: OpenClaw plugin JSON parsing and stdout diagnostic redaction; use core plugin SDK import.

### Build
- Attach ImageEditor during release prep.

### Documentation
- Changelog tag linking and release-prep updates.

---
## v0.22.237

### Features
- **Capture**: Command palette for quick capture actions
- **Core**: After-capture OCR clipboard task, markdown directory index output, and Send-to post-v1 policies
- **CLI**: Upload `--randomize` flag (default on) appends random suffix matching UI `%ra{10}` to avoid CDN caching

### Fixes
- **CLI/OpenClaw**: Text-upload pipeline with JSON validation, diagnostics, path normalization, bootstrap uploader JSON, manifest parity, plugin bundling for agent hosts, macOS plugin discovery, and S3 keychain credentials; skip redundant named-copy when `--name` is set
- **MCP**: History search and resources: query parsing, URI matching, thumbnail/blob paths, stale and oversized diagnostics, task identity race, error-shape alignment
- **OCR**: Onboarding language lifecycle: regional defaults, refresh and persistence, fallback when enumeration fails, assistant history stale-file guard, index schema on history delete
- **Command palette**: Keyboard selection wrap, blank-escape close, search whitespace normalization
- **Editor**: Save and sidecar reliability: distinct failure reporting, dirty-state preservation, overwrite truncation, bitmap disposal, annotation persist-after-continue; ImageEditor resource path normalization and effect browser spacing
- **FFmpeg/Media**: Path escaping, cancellation tokens, process-tree kill, CombineScreenshots guards, probe quoting, workflow override wiring; FileDownloader chunked encoding and early-EOF fix
- **Linux**: Pipe-drain deadlocks across CLI subprocesses; Wayland/X11 capture routing, Oem102 hotkey mapping, active-window fallbacks; deb packaging recommends wl-clipboard/xclip; grim/slurp null-guard
- **macOS**: Tray Dock icon hidden (#252), upload file picker fallback, front-window parsing, update prompts with manual action, clipboard path whitespace, onboarding folder-picker crash
- **Uploaders**: Default-instance lifecycle, routing conflicts, auto fallback within category, drag-drop normalization, stale-default cleanup logging
- **Settings/Backup**: Async saves, atomic zip replacement, weekly backup TOCTOU handling, restore from backups, empty-destination guards, user-visible failure toasts and diagnostics
- **History/Indexer**: OCR index cleanup on delete; enumeration resilience for long paths and I/O errors
- **UI**: Toast fade opacity, multi-monitor bounds, context-menu close resume
- **Capture**: Scrolling capture ReferenceEquals guard when closing old capture window
- **Mobile**: File-scoped S3 config and imports
- **Misc**: IsFileLocked false for missing paths, HSB alpha hash contract, SFTP invalid key reporting, silent Windows updater, GDI cursor cleanup, build guardrails against user props override, StringCollection type converter fix, EmojiCatalog search score case-insensitivity

### Build
- **Dependencies**: Avalonia 12.0.5, SkiaSharp 3.119.4, SQLite bundle pins
- **macOS**: Info.plist template and hardened-runtime entitlements (not yet wired into packaging)

### Documentation
- **Plans**: Linux and macOS improvement plans (XIP0077-XIP0079), reliability upgrade plan (U1-U10), KNOWN_ISSUES macOS section
- **Proposals**: XIP, IEIP, and KFIP proposals including XIP0080 (Linux evdev hotkeys) and KFIP0009-0012
- **Contributor**: AGENTS wrapper policy and CONTRIBUTING.md
- **Blog**: 2026 blog draft series
- **Guides**: FFmpeg Linux/override guidance, XIP proposal status normalization

### Testing
- **Core**: Guardrail coverage (Headless.NUnit, McpServer.Tests, FFmpeg concat escape regression tests)

### Changed
- **Release/CI**: Prerelease defaults, v0.22.256 workflow and Flathub verification docs, Fedora VS Code updater script
- **OCR UI**: Normalize platform language tags and display names in tool UI loader
- **ImageEditor**: Submodule updates

## v0.22.239

### Fixes
- **Core**: Resolve startup log issues
- **Core**: Parse raw OpenClaw plugin JSON output, redact stdout diagnostics, use core OpenClaw plugin SDK import

### Build
- **Core**: Attach ImageEditor during release prep

### Documentation
- **Core**: Link changelog only for existing tags; link version headings and omit per-entry hashes; update changelog for release prep

## v0.22.237
### Fixes
- Resolve startup log issues

### Build
- Attach ImageEditor during release prep

### Documentation
- Link changelog tags and omit hashes
- Update changelog for release prep

## [v0.22.236](https://github.com/ShareX/XerahS/releases/tag/v0.22.236)

### Features
- **Mobile**: Expand Android and iOS parity with native shells, About screens, hosted/custom uploader imports, upload history, privacy/store metadata, and mobile configuration flows
- **Assistant**: Add assistant provider configuration, OCR upload workflows, overlay commands, safety contracts, aliases, and current model IDs
- **CLI**: Expand CLI automation with OpenClaw compatibility, upload naming, file-forced uploads, directory indexing, and ReClip commands
- **Plugins**: Add community destination-plugin registry, installer UX, Pixelfox packaging, and KFIP0004 registry validation coverage
- **MCP**: Introduce the XerahS MCP runtime, transports, desktop settings, prompts, usage guide, and integration coverage
- **Onboarding**: Build the onboarding wizard state machine, UI, converters, trigger flow, debug launcher, and style integration
- **Capture Workflows**: Add smart-region and social-media capture workflows with after-capture task execution, OCR wiring, copy-path actions, and profile services
- **About**: Add About tab/library grouping and loaded library version display
- **Annotations**: Preserve editable annotation sidecars and standardize saved-annotation re-editing support
- **Image Effects**: Complete the SXIEF/schema-driven filter migration and harden image-effect preset handling
- **Indexer**: Improve Index Folder and watch-folder options, including ignore-empty-folder handling

### Fixes
- **Capture**: Improve capture coordinate mapping, DXGI/GDI/WinRT fallback behavior, cursor composition, region/scroll targeting, recording bounds, and monitor scaling
- **Uploaders**: Normalize uploader routing and provider behavior across FTP/SFTP, Nextcloud, Imgur, Dropbox, S3, cookies, custom uploaders, fallback paths, URLs, and result history
- **Plugins**: Harden plugin dependency resolution, package extraction, manifest validation, load/unload cleanup, diagnostics, provider IDs, and fallback assembly checks
- **Mobile**: Harden Android and iOS upload, import, share, secrets, package identity, diagnostics, and store-release flows
- **Linux**: Harden Linux Wayland/X11 capture, portal hotkeys, clipboard URIs, Flatpak/XDG state, sandbox IDs, desktop entries, and geometry parsing
- **macOS**: Stabilize macOS overlay mapping, region selection, clipboard file drops, service helpers, capture scaling, hotkeys, and release assets
- **CLI**: Stabilize CLI capture/upload/record validation, JSON output, temp-file cleanup, naming, pipe/text uploads, and task completion matching
- **Assistant**: Stabilize assistant aliases, OCR history/cache/options, copy-path privacy, clipboard handling, overlay output, and local-file lookup
- **History and Editor**: Fix history lookup, editor sessions, sidecar fallback, thumbnail refresh, bitmap lifetimes, image presets, annotations, and Pin to Screen resource cleanup
- **Media**: Fix FFmpeg, recording output, mixed thumbnail grids, random seek slots, video thumbnail leaks, and media timing behavior
- **Notifications**: Fix toast actions, timeout/process cleanup, markdown image links, severity propagation, timing validation, and menu behavior
- **Settings**: Harden settings save/reload, backups, reset cleanup, secret paths, upgrade detection, recent-task state, and config repair
- **OCR**: Normalize OCR language/options handling, reruns, whitespace results, cache reuse, and history persistence
- **Onboarding**: Repair onboarding rendering, step state, actions, destinations, hotkey parsing, trigger timing, and build errors
- **MCP**: Harden MCP parameter validation, notification responses, headless contracts, and annotation parsing
- **Indexer**: Fix Indexer traversal, extension filters, folder statistics, async output paths, and total-folder counting
- **Workflows**: Repair workflow hotkeys, duplicate task identity, stale IDs, timeout handlers, task settings, and destination category mapping
- **Paths**: Harden path normalization, filename mutation, unique suffix handling, directory collisions, URL encoding, and home-path behavior
- **Build Stability**: Resolve build, binding, DevTools, XAML, release/debug, and test isolation regressions

### Build
- **Avalonia**: Move the app and Android bootstrap through Avalonia 12, headless text shaping, view-folder, and Vortice adjustments
- **ImageEditor**: Sync ShareX.ImageEditor and supporting port tooling across Avalonia and framework updates
- **VideoEditor**: Update ShareX.VideoEditor integration, submodule revisions, and WebUI build behavior
- **Plugins**: Harden plugin copy, architecture separation, runtime restore, and solution-build behavior
- **Build**: Stabilize release, CI, deterministic dotnet, isolated output, and repository build graph behavior
- **Tooling**: Add changelog, markdown hygiene, mojibake, and BOM safeguards
- **Build**: Apply build-system maintenance and dependency updates

### Documentation
- **Blog**: Consolidate the 2026 engineering, release-readiness, stabilization, plugin, Android, Flatpak, and maintenance blog draft series
- **Proposals**: Consolidate XIP, IEIP, and KFIP proposal drafts, reviews, renames, migrations, and design/research updates
- **Mobile Release**: Document Android Play, iOS App Store, privacy, release-build, and mobile parity readiness
- **Plugins**: Refresh destination-plugin, registry, Pixelfox, and OpenClaw setup documentation
- **Workflow**: Tighten changelog, release, build, XIP sync, maintenance, and commit-policy guidance
- **ImageEditor**: Record ShareX.ImageEditor port planning, comparison, manifest, and sync guidance
- **Repository Docs**: Repair markdown encoding and normalize repository, developer, build, and README documentation
- **Maintenance**: Fold hourly review trackers, verification notes, and status snapshots into the release maintenance record
- **Documentation**: Update supporting documentation and release notes

### Testing
- **Platform**: Add platform verification coverage for macOS native crosshair capture
- **Coverage**: Restore and expand coverage for filters, editor history, after-capture workflows, proposals, and regression paths

### Changed
- **Editors**: Sync editor integrations with ShareX.ImageEditor and ShareX.VideoEditor updates
- **Branding**: Refresh logos, icons, feature graphics, and release artwork
- **Versioning**: Align version, prerelease, and origin-release synchronization metadata
- **Privacy and Signing**: Tighten privacy, signing, export-compliance, and local-signing behavior
- **General**: Apply miscellaneous release maintenance, UI polish, and compatibility updates

## [v0.21.0](https://github.com/ShareX/XerahS/releases/tag/v0.21.0)
### Features
- **Custom uploaders & Send-to**: Catalog multi-add, save-back flow, and Send-to behavior prompt
### Fixes
- **ShareX.ImageEditor**: Submodule updates for effect browser parity, categories/borders, host shortcut rows, auto-crop dialog, empty-state actions, crop dedupe, and latest-effects compatibility
- **Overlay & capture parity**: Align Linux overlay capture with Windows; fix region selector preference on hotkey-triggered captures
- **Modals & catalog**: Centralize modal opening; dispatch opens on UI thread for Add from Catalog on Linux
- **Recording & video editor**: Gate unsupported pause on Wayland; harden editor launch
- **Core**: Upload fallback File→Image; suppress AfterCapture toast on cancel; repair uploader mojibake labels
- **Hotkeys / Imgur**: X11 fallback when portal bind cancelled; cross-platform OAuth URL helpers
- **Linux (Wayland / GNOME / KDE)**: Portal retry, transparent overlay and mixed-DPI, `UseTransparentOverlay` plumbing, DBus crash guard, selector defaults, GNOME crop workflow
- **Paths / UI / upload**: User-writable plugins folder; effect browser aligned with unified editor API; auto-heal stale destination instance IDs
### Refactor
- **Core**: Remove upload destination auto-persist and simplify resolution
### Build
- **AUR & Windows**: PKGBUILD and script updates; reusable AUR packaging; permissions; MSI via WiX
- **ShareX.ImageEditor**: Submodule tracking (IEIP0004 branch, develop, parity/revert/schema fixes)
- **Tooling & quality**: Upload fallback logging/comments; default publish-release to prerelease; LF enforcement; CS8604/DBus
### Documentation
- **Blog drafts (Mar 2026)**: Annotation/IEIP/Linux/XIP/multipart/Wayland series — add and revise
- **XIPs & proposals**: XIP0054–0056 (multipart, Send-to, history); Send-to post-v1; systems-thinking prompt; workflow destination tooltip; commit prefix; proposal consolidation; IEIP0004 finalize; capture/upload XML and fallback docs
- **IEIP0004 / Linux**: Lessons from catalog browser integration; INSTALL.md; GNOME Wayland portal/overlay notes; interactive fallback explanation
- **Developers**: Move `PLUGIN_SDK.md` to `developers/guidelines/`
### Changed
- **Multipart upload**: Abstractions, coverage, and S3 multipart support
- **ShareX.ImageEditor / IEIP**: Schema-driven effects overhaul and IEIP0005 doc; effect apply, schema dialog binding/slider; ongoing submodule sync
- **Imgur**: OAuth UX, token flow, and client ID defaults
- **Custom uploaders**: Hide legacy import after first run; Save to Plugins label and XIP0056 auto-instance metadata
- **Meta**: `Directory.Build.props` and feature-systems-thinking prompt updates
## [v0.20.12](https://github.com/ShareX/XerahS/releases/tag/v0.20.12)
### Fixes
- **RegionCapture Toolbar**: Revert `RegionCaptureAnnotationViewModel` to the pre-ToolInfo adapter behavior to restore stable annotation toolbar interactions.
- **RegionCapture Icons**: Load `ImageEditorStyles.axaml` in the overlay window so toolbar buttons render distinct Lucide icons instead of fallback glyphs.
## [v0.20.11](https://github.com/ShareX/XerahS/releases/tag/v0.20.11)
### Features
- **Clipboard Monitor**: Add cross-platform clipboard monitoring with toggle in Application Settings > Integration tab; register on Windows, Linux, and macOS; suppress origin loops and harden async reads; default to disabled
- **Tool Info Panel (IEIP0002)**: Implement ToolInfo adapter in RegionCapture; update dimensions during shape resize via handles; tune visual prominence
- **Creative ImageEditor Filters**: Integrate creative image effects and filters into the ImageEditor
### Fixes
- **Menus**: Fix startup command binding regression across platforms, clipboard monitor focus-stealing, tool windows hidden behind main window, and menu dismissal on Linux
- **Annotation Toolbar**: Restore fixed-width, square, centered-split, right-side layout and tool options; share annotation toolbar with ImageEditor
- **Recording**: Apply CLI duration across recorder jobs, wire stop signal to active sessions, route start to last region, configure custom region recording fallback
- **Send To**: Wire Windows pipeline, harden Linux entry generation, make macOS fallback explicit, use native Windows shortcut
- **Theme**: Normalize effect property controls to XerahS theme; align task image effect editing with ImageEditor UX; add ShareX resource compatibility and correct surface tokens
- **ImageEditor**: Restore Task Settings Add Effect enumeration; keep effect browser dialogs visible on Linux; prevent startup crash in native theme resources
- **Scrolling Capture**: Correct stitching
- **Cross-Assembly Views**: Resolve registration and update IEIP0003
- **Linux Upload Content**: Prevent clipboard hang
- **macOS**: Skip native dylib rebuild when sources unchanged
- **History/Explorer**: Replace emoji glyphs with Lucide font icons
- **Plugins**: Clean plugin folders safely across app and user roots
- **Tools Navigation**: Improve tools navigation and upload window activation
- **Release Scripts**: Fix tag name collision and redirect `find_tag_run_id` status to stderr
### Refactor
- **Fluent Theming**: Migrate XerahS UI to native Fluent theming; adopt OS-aware accent across desktop UI and RegionCapture; align app and RegionCapture theming; defer editor accent to ImageEditor; apply ImageEditor system theme support
- **Compiled Bindings (XIP0053)**: Enable compiled bindings defaults, harden ViewLocator with explicit mappings, complete guardrails
- **DI/Host (XIP0052)**: Inject task and recording managers through host services, extract overlay capture sessions, harden MVVM workflow boundaries, finalize host startup wiring, consolidate desktop composition
- **Mobile Theming**: Add adaptive theme tokens and switch Mobile.Ava and Mobile.Maui views to shared theme resources
- **UI Polish**: Move host icon surface into XerahS UI, remove inline workflow type dropdown, center button content, standardize color swatch tile width and names, preserve previous color on selection changes
- **Annotation Toolbar**: Refactor toolbar styles in ShareX.ImageEditor
### Build
- **VideoEditor**: Update submodule for Tailwind 4.2.2 and playback/WebUI fixes
- Exclude Windows clipboard tests on non-Windows platforms
### Testing
- Add XIP0052 composition boundary and injected manager coverage; stabilize manager tests
## [v0.20.5](https://github.com/ShareX/XerahS/releases/tag/v0.20.5)
### Features
- **VideoEditor**: Integrate ShareX.VideoEditor with desktop host wiring, `open-video-editor` CLI support, diagnostics, FFmpeg/ffprobe-backed UI and headless trim, and packaged WebUI assets
- **Uploaders**: Add Nextcloud and native Immich uploader plugins with scaffolding and design notes
- **History**: Add image combine actions and multi-selection groundwork
- **Theme**: Track OS system accent colour app-wide via `SystemAccentColor`
### Fixes
- **VideoEditor**: Harden startup, dependency resolution, packaged WebUI/bootstrap, FFmpeg path propagation, playback sync, and reopen lifecycle
- **Custom Uploaders**: Inline editor in settings while preserving names, hiding duplicate labels, and making inline names read-only
- **Linux Wallpaper**: Detect wallpaper providers across desktop environments, preload and normalize sources, and restore ImageEditor wallpaper backgrounds through platform abstractions
- **UI/Theme Surfaces**: Normalize all tool window, hotkey control, card, and index folder surfaces; restore scrollbars; apply accent buttons across color picker, image splitter/combiner/thumbnailer, video converter/thumbnailer, upload content, and hash check window
- **ImageEditor**: Region capture toolbar icons, overlay alignment, pin export, pinned-window drag, preview bitmap cloning, screenshotspath picker, remembered window size, and submodule updates
- **Linux Region Capture**: Restore X11 fallbacks, enable Wayland overlay selector with portal capture, harden selector preference handling, and drain portal hotkey rebinds before dispose
- **Shell Integration**: Wire startup and shell integration entries for Windows, Linux, and macOS
- **Workflow/Editor UI**: Stage workflow editor changes until save; disable File Save/Save As when no image; sort View Zoom alphabetically; wire annotate editor task actions and hide task buttons in correct host contexts
- **Settings**: Fix ScrollViewer not scrolling to bottom; fix Destination Settings provider panel flicker; fix About view Social groupBox width
- **Linux**: Avoid Avalonia dispatcher sync-context capture in portal watchers
- **Build Targets**: Fix Windows-to-macOS packaging cross-compilation and Linux desktop build targeting
### Refactor
- **Theme (XIP0050)**: Remove FluentAvalonia package; introduce shared surface window and page base controls; centralize desktop theme styles; make accent the default button style
- **DI/MVVM (XIP0052)**: Migrate to Microsoft.Extensions.DependencyInjection; inject task and recording managers; extract pipeline from WorkerTask; consolidate desktop composition
- **Linux Capture**: Replace UseModernCapture semantics with per-selector preference plumbing and settings UI
- **Core/UI**: Share history and toast context menus; align app typography
### Build
- **Release Automation**: Normalize editor projects to Any CPU, automate and harden Chocolatey release sync, fix CRLF pack output paths, and add fresh-clone bootstrap helpers
- **VideoEditor**: Update hybrid web/native toolchain requirements for the WebUI build
### Documentation
- **Developer Workflow**: Document fresh-clone setup, shared agent workflow, shared-library commit conventions, explicit GitHub issue handling, and FFmpeg guidance
- **Architecture**: Add VEIP0001 hybrid VideoEditor direction, Immich plugin XIP, XIP0050 (FluentAvalonia removal), XIP0051 (Linux selector preferences), XIP0052 (agentic DI refactoring)
### Testing
- **Region Capture**: Add UI smoke tests for region capture flows
## [v0.19.9](https://github.com/ShareX/XerahS/releases/tag/v0.19.9)
### Features
- **Video Editor**: Integrate ShareX.VideoEditor submodule; add `WorkflowType.VideoEditor`, Tools menu and sidebar nav, `AnnotateMedia` (renamed from `AnnotateImage`) with toast dispatch to VideoEditor; open editor after recording when AnnotateMedia is set; headless stubs and IUIService wiring
- **Uploaders**: Add URL shortener foundation and Bitly URL shortener plugin support
### Fixes
- **Linux Region Capture**: Improve cropping for physical-resolution desktops, including KDE Plasma portal bitmaps and X11 overlay positioning; add diagnostics, detect XWayland vs native Wayland, and restore fast overlay region capture
- **Linux**: UseModernCapture option (XDG Portal vs overlay), Wayland region capture and mixed-DPI bounds, GNOME portal recording output, double region-selection prompt fix; KDE Spectacle and GNOME fallbacks (XIP0046-C); system tray SNI (GNOME/Wayland); systemd user unit path via UserProfile
- **Linux Recording**: Harden GStreamer pipeline by correcting region crop, removing conflicting `video/x-raw` caps before `glupload`, adding GL-to-CPU fallback, making fatal errors selectable in RecordingView, and cleaning up portal session on fatal errors
- **Core**: Validate URL before OpenURL Process.Start; SaveRequested/SaveAsRequested for embedded and standalone editor; fall back to File-category instances when no Image uploader; default white tray icon on Linux/macOS; Tools_* nav items and VideoEditor dispatch; AnnotateImage JSON deserialization; Linux portal handle format and RPM packaging; fix tray stop button behavior and hotkey recording stop flow
- **Core**: Correct DXGI capture ModeRotation mapping for DMDO_90/DMDO_270 rotations
- **ImageEditor**: Submodule updates and macOS build; add ShareX.ImageEditor at develop; Zoom to Fit in zoom picker; —7a easy wins (Random.Shared, Category overrides, Gamma LUT cache)
- **VideoEditor submodule**: Button theme isolation and ReactiveUI main thread scheduler fixes
- **Watch Folder**: Support legacy watchfolder.service
- **Core**: Hide Video Editor from Tools menu in release builds
- **PluginLoadContext**: Fix stale shared dependency name/order checks
- **Updates/Logging**: Fix reflection-disabled GitHub update JSON handling and normalize error log naming to `yyyyMMdd`
### Refactor
- **ImageEditor (EIP0001)**: Advance Phase 1 commits; migrate to new namespaces; rename submodule and sync references
- **Core (PathsManager)**: Centralize plugins path selection; centralize log and app path handling and expand path audit coverage for plugins/screenshots/tools/troubleshooting paths
- **Indexer**: Share tree helpers and settings types, collapse async adapters, and externalize HTML styles
### Build
- **ImageEditor**: Replace the redundant legacy submodule layout and update embedded ShareX.ImageEditor integration; update submodule references
- **Release Automation**: Run maintenance chores during release bump-tag flow; enforce standard release notes block
- **Developer Tooling**: Add `run-debug-app.sh` helper script
### Documentation
- **Architecture**: Move image editor refactor proposal to IEIP; move proposals into docs/proposals; Backend Porting checklist (March 2026); EIP0001 phases A/B/C; OS-specific known issues and Linux hotkey workaround; XIP0046 summary (Issues C, D, E fixed); FFMPEG.md; XIP0042/XIP0044/XIP0046 task docs; run-debug-app.ps1; VEIP0001 and XIP0046 proposal
- **XIP0047**: Summarize Linux region capture DPI and performance investigation, including X11 overlay shift and KDE physical-bitmap crop fixes
- **XIP0042**: Update the SkiaSharp hardware acceleration task document; XIP sync workflow and backups; XIP0043 complete; XIP0038/XIP0040/XIP0042 doc audits
### Performance
- **Linux**: Faster overlay and smoother crosshair on Linux (region capture)
## [v0.18.11](https://github.com/ShareX/XerahS/releases/tag/v0.18.11)
### Features
- **Mobile**: Android and iOS MVP with Share Extension and MAUI; adaptive theming, upload queue/picker/history, active destination selector, desktop-compatible upload filename pattern, broad share-intent support; Amazon S3 and Custom Uploader config UI; Swift/Kotlin native shells and share extension
- **Media Explorer**: Provider file browsing with S3 and Imgur, navigation, search, filtering, and CDN thumbnail optimization
- **Watch Folder**: Daemon with lifecycle hooks, runtime policy, settings controls, and tests
- **Indexer**: Async streaming with progress and cancellation; open in own window; file extension filters; dark theme with light-mode toggle
- **ImageEditor**: Integrate submodule; File Open choice dialog; annotation options persistence; app/editor theme sync
- **Workflows**: UploadContentWindow; AutoCapture, Pin to Screen, Ruler, MonitorTest, HashCheck; 6 media tools (ImageCombiner, ImageSplitter, ImageThumbnailer, VideoConverter, VideoThumbnailer, AnalyzeImage); OCR and ScrollingCapture end-to-end
- **Upload**: Auto destination uploader; cross-platform secrets store with diagnostics; proxy config UI
- **Amazon S3**: AWS SSO auth, region selection, CNAME, public bucket policy; redesign config to mimic Custom Uploaders
- **Plugins**: Dropbox, Paste2, GitHub Gist, FTP/FTPS/SFTP, Pastebin; XIP0040 plugin architecture; DestinationsPluginSdk
- **UI**: Copy Errors to HistoryView, AfterUploadWindow, Toast
- **Linux Capture**: DBus fallbacks, KDE permissions, decision trace orchestration, portal waterfall
- **Packaging**: Scoop, WinGet, Chocolatey support; generate-winget.ps1 enhancements
- **Misc**: Imgur album selection and GIFV; Dropbox OAuth overhaul
### Fixes
- **ImageEditor**: XAML startup crash, highlight/crop/submodule fixes, context menu, DPI and crop handles
- **Scrolling Capture**: Auto-scroll, workflow settings, hotkeys, scroll position detection
- **Media Explorer**: Harden listing, normalize URLs, error handling, copyable footer
- **Mobile**: iOS App Group for S3 config in Share Extension; unify share payload and TimeZoneInfo
- **Upload**: MainViewModel parameterless copy/upload; multi-uploader fallback, clipboard routing
- **Capture/Region**: Annotation layer rendering, crop offset, AfterCapture refresh, workflow integration
- **Workflows**: Allow OCR and scrolling workflows from tray
- **Linux**: Portal timeout, Wayland/slurp/portal fixes, GStreamer clamp, D-Bus and plugins path resolution
- **After Capture**: ShowAfterCaptureWindow persistence
- **Misc**: FAQ XerahS/ShareX Linux ref; update checker pre-releases; backup machine-specific; S3 setup reorder; macOS icon in Windows build; File Open dialog crash
- **Core**: Correct flipped monitor orientation in DXGI capture; fail fast for Linux publish and validate package payload; harden daemon bundling across desktop RIDs; marshal Avalonia clipboard access to UI thread; remove WinForms dependency from Windows platform
- **Core**: Avoid SIGPIPE in archive validation checks
- **Update Changelog Script**: Ensure entries array has Count for single-category
### Refactor
- **Core**: Split large ViewModels, WatchFolder daemon base service, ScreenRecordingManager startup; WindowState naming; GeneralHelpers split
- **Upload**: Polymorphic uploader config pilot
- **Workflows**: App workflow orchestration services
- **Linux Capture**: Modular providers, parallel lanes, coordinator, contracts
### Build
- **CI/Release**: All-platform release workflow, Linux by arch, release title, bump/tag automation
- **Android**: Mobile build infrastructure
- **Linux**: Plugin packaging, RPM strip, display diagnostics, desktop-file-utils
- **ImageEditor**: Submodule checkout, recovery hook, pre-push
- **Core**: Add changelog update automation script; validate release assets and RID metadata
- **Misc**: Version/changelog bumps, central package management, plugin DLL deduplication, cross-compilation macOS, GPL headers Swift/Kotlin
### Documentation
- **Consolidate**: Developer docs to developers/; plugins to developers/plugins and .xsdp; changelog consolidation; mobile README simplification
- **Planning**: Roadmap, XIP0033 complete, task docs
- **Misc**: Feasibility report JS/CSS; sync-submodules; build/Linux/mobile docs; XIP0040/0039; update-changelog skill in run-maintenance
- **Core**: Create XIP0043-Remove-WinForms-and-Harden-CrossRID-Daemon-Bundling.md
### Testing
- **Linux Capture**: Waterfall and lane matrix tests
### Performance
- **RegionCapture**: Reduce annotation rebuild pressure
- **Core**: Skip app-driven plugin build in solution builds; update ImageEditor submodule for TFM simplification
## [v0.17.4](https://github.com/ShareX/XerahS/releases/tag/v0.17.4)
### Features
- **Indexer**: Modernize HTML output flow and default to dark theme with light-mode toggle
### Build
- **CI**: Split Linux release builds by runner architecture and set release title metadata
- **Automation**: Add release bump/tag workflow skill for standardized release prep
## [v0.16.3](https://github.com/ShareX/XerahS/releases/tag/v0.16.3)
### Features
- **Mobile**: Add active upload destination selector and in-app destination label on Android and iOS
- **Mobile**: Use desktop-compatible upload filename pattern on Android and iOS
- **Mobile**: Add broad share-intent support for arbitrary file types on Android and iOS
- **Media Explorer**: Implement provider file browsing with S3 and Imgur support, including navigation, search, filtering, and CDN thumbnail optimization
- **Watch Folder**: Add watch-folder daemon with lifecycle hooks, runtime policy controls, and tests
- **Mobile**: Add adaptive theming infrastructure with native styling polish
- **Mobile**: Add upload queue, picker, and history screens
- **UI**: Add Copy Errors to UI (HistoryView, AfterUploadWindow, Toast)
- **ImageEditor**: Add app/editor theme synchronization with platform-aware styling
### Fixes
- **iOS**: Use App Group settings so Share Extension can read Amazon S3 configuration
- **ImageEditor**: Fix precompiled Avalonia XAML startup crash (`XamlLoadException`) in editor app initialization
- **ImageEditor**: Improve highlight rendering/fill behavior, Smart Eraser, text defaults, and canvas zoom performance
- **ImageEditor**: Restore crop UX and precision with full-image/L-shape fixes, visible handles, and DPI-aware hit zones
- **Scrolling Capture**: Improve auto-scroll behavior and workflow settings integration
- **Workflows**: Allow OCR and scrolling workflows from tray
- **Media Explorer**: Harden listing, normalize URLs, and improve error handling
- **Mobile**: Unify iOS share payload handling and TimeZoneInfo serialization
- **Upload**: Align MainViewModel helper with parameterless copy/upload events
- **ImageEditor**: Update submodule with context menu fixes
- **Capture**: Optimize annotation layer rendering and resource management
- **Documentation**: Update FAQ to correctly reference XerahS instead of ShareX in Linux screen capture section
- **Infrastructure**: Integrate update-changelog skill into run-maintenance workflow
### Refactor
- **Core**: Split large ViewModels, extract WatchFolder daemon base service, and consolidate ScreenRecordingManager startup flow
- **Core**: Remove WindowState naming collisions
- **Core**: Split GeneralHelpers into utility classes
- **Upload**: Add polymorphic uploader config pilot
- **Workflows**: Extract app workflow orchestration services
### Build
- **Infrastructure**: Add all-platform release workflow and repository sync helper script
- **Android**: Add Android mobile build infrastructure
- **Linux**: Harden plugin packaging, RPM strip behavior, and display diagnostics
- **Hooks**: Add cross-platform ImageEditor recovery and auto-push on pre-push
### Documentation
- **Maintenance**: Simplify mobile README and move refactor/hardening notes into documentation archives
- **Planning**: Update task planning docs and move completed XIP0033
- **Plugins**: Consolidate plugin documentation into 'developers/plugins' and standardize on .xsdp extension
- **Developer**: Consolidate developer documentation into 'developers' root folder
- **Architecture**: Add feasibility report for JS/CSS migration
- **Submodules**: Add sync-submodules workflow and update ImageEditor to latest develop
- **Tasks**: Add refactoring audit skill and native UI theming task
## [v0.15.5](https://github.com/ShareX/XerahS/releases/tag/v0.15.5)
### Features
- **Linux Capture**: Add DBus fallbacks, KDE desktop permissions, and decision trace orchestration
### Fixes
- **Linux Capture**: Enforce portal-only sandbox policy, unify waterfall, and improve logging
- **Builds**: Fix cross-platform build configuration and add linux-arm64 support
### Refactor
- **Linux Capture**: Modularize providers with parallel lanes, coordinator, and contracts
### Testing
- **Linux Capture**: Add Linux capture waterfall and lane matrix tests
### Documentation
- **Build System**: Rename developer README and add Linux guide
- **Roadmap**: Finalize Linux phase roadmap and release gate
## v0.15.0
### Features
- **Mobile**: Add Android and iOS MVP with Share Extension support, .NET MAUI project
- **Mobile**: Add Custom Uploader and Amazon S3 configuration UI (#124, #125, @Hexeption)
- **Indexer**: Implement async streaming indexer with progress and cancellation
### Fixes
- **Image Editor**: Share annotation preview visuals with ImageEditor to ensure consistency
### Fixes
- **Annotations**: Optimize rendering, remove draw-start dot artifact, and improve responsiveness
- **Workflow**: Complete WorkflowType end-to-end wiring
- **UX**: Hide SilentRun window on first open instead of minimizing
- **Updates**: Gracefully handle repositories with only pre-releases
- **After Capture**: Persist "Show after capture window" behavior across repeated runs
- **Upload**: Add multi-uploader auto destination fallback and wire mobile Amazon S3 and plugin integration to InstanceManager
- **Watch Folder**: Convert MOV captures to MP4
- **Settings**: Make backup and secrets filenames machine-specific
- **Amazon S3**: Reorder and renumber setup steps
- **iOS**: Improve local signing setup and share extension flow
### Build
- **Plugins**: Centralize plugin copy target and pass host TFM
- **Dependencies**: Bump Avalonia packages to 11.3.12
- **ImageEditor**: Update submodule for theme-aware view, net9 compatibility, and track develop branch
### Documentation
- **Audits**: Organize audit files and update UI control inventory snapshots
- **Tasks**: Mark XIP0030 complete and move to completed tasks
## v0.14.0
### Features
- **Monitor Test**: Implement MonitorTest workflow with diagnostic and pattern testing modes
- **Tools**: Add Ruler workflow with full RegionCapture integration
- **Indexer**: Make Index Folder open in its own window
- **Editor**: Integrate upstream ShareX.ImageEditor submodule with File Open choice dialog
- **Region Capture**: Add annotation options persistence
### Fixes
- **Logging**: Fix duplicate date in log filename on date rotation
- **Region Capture**: Improve annotation toolbar integration and reduce rebuild pressure
- **Indexer**: Enable Open in Browser button and remove WebView in favor of system browser
- **Navigation**: Enable menu navigation and update editor data transfer APIs
- **Editor**: Sync ImageEditor fixes, persist annotation options, refactor platform abstractions, enable Zoom to Fit
- **ImageEditor**: Update submodule with unified undo-redo, smart padding crop sync, clipboard fixes, z-order fixes, and dispose bug fixes
- **Packaging**: Restore macOS icon in Windows package build
- **Upload**: Delay upload progress title update until actual upload starts
- **macOS**: Harden mac packaging and cross-platform editor wiring
- **Dialogs**: Prevent File Open dialog crash and add global exception logging
### Build
- **Cross-Compilation**: Add macOS from Windows support and build system documentation
- **Infrastructure**: Fix version parsing in Windows package script
## v0.13.0
### Fixes
- **Menu Bar**: Fix hash checker routing and dynamic workflows menu
- **Upload**: Improve Upload Content workflow handling, window UX, and text upload routing
## v0.12.0
### Fixes
- **Tools**: Add media tools to navigation bar and fix DataTemplate issues
- **Proxy**: Fix custom uploader loading and add configuration UI (#77, @Hexeption)
- **Linux**: Add dark mode support, theme settings, and Wayland Hyprland screenshot support (#62, @unicxrn; #61, @unicxrn)
- **macOS**: Add native application menu (#60, @Hexeption)
- **Custom Uploaders**: Fix compatibility improvements and version compatibility (#74, @Hexeption; #71, @emmsixx)
- **Security**: Fix DPAPI platform warning (#73, @Hexeption)
### Refactor
- **Editor**: Rename namespace from ShareX.Editor to XerahS.Editor and update all references
### Build
- **Plugins**: Improve plugin copy target to only include plugin assemblies
- **Configuration**: Update build files, packaging configuration, issue templates, and .gitignore
## v0.11.0
### Features
- **Upload**: Implement UploadContentWindow and remove superseded upload WorkflowTypes
## v0.10.0
### Features
- **Workflows**: Implement AutoCapture workflows
## v0.9.0
### Features
- **Workflows**: Implement Pin to Screen workflows
- **Amazon S3**: Enhance SSO with region selection
### Fixes
- **Upload**: Improve upload error surfacing and history actions
- **Workflows**: Preserve workflow order and exclude None
- **Custom Uploaders**: Fix compatibility check for XerahS versions
### Build
- **Plugins**: Restore plugin DLL deduplication with retry logic
### Core
- **Rendering**: Remove RectangleLight; modern Skia rendering deprecated it
## v0.8.0
### Features
- **Security**: Add cross-platform secrets store with diagnostics
- **Upload**: Add auto destination uploader
- **Custom Uploaders**: Implement full support including editor UI and integration
- **Task Settings**: Redesign Task Settings UX with dedicated Image/Video tabs
- **Tray Icon**: Add recording-aware tray icon with pause/abort controls
- **Image Formats**: Add AVIF and WebP image format support
- **Linux/Wayland**: Fix screen capture on Wayland by integrating XDG Portal API
### Fixes
- **Capture**: Allow clipboard payloads in capture phase
- **Upload**: Add clipboard upload auto routing
- **Region Capture**: Correct crop offset, refresh AfterCapture UI, and fix coordinate mapping for Windows (#29)
- **Linux**: Fix active window capture hierarchy, coordinates, hotkey initialization, and Region Capture
- **UX**: Hide main window when capture triggered from tray/navbar
- **UI**: Fix update dialog layout
### Refactor
- **Editor**: Update XerahS.Editor.csproj references and docs
## [v0.7.0](https://github.com/ShareX/XerahS/releases/tag/v0.7.0) - Annotation Overlays & Packaging
### Features & Improvements
- **Annotations**: Enable Annotation Toolbar in Region Capture Overlay and refactor (#53)
- **Region Capture**: Add support for transparent background capture (RectangleTransparent)
- **macOS**: Native single-file app bundle packaging (`.app`)
- **Packaging**: Automated multi-arch Windows release builds
- **Plugins**: Support for user-installed plugins and packaging
- **Window Capture**: Add support via monitor cropping fallback
- **Media Library**: Basic implementation (#49)
### Bug Fixes
- **Annotation Layer**: Fix coordinate system for multi-monitor/high DPI and compositing
- **Exceptions**: Global exception handling implementation
- **Screen**: Fix frozen screen issue (#51)
- **Cursor**: Fix system cursor issues (#46)
## [v0.6.0](https://github.com/ShareX/XerahS/releases/tag/v0.6.0) - UI Redesign & Auto-Update
### Features & Improvements
- **UI Redesign**: Comprehensive visual overhaul of all views using Grid layout and consistent styling
- **Auto-Update**: Implement auto-update system with Avalonia UI
- **After Upload**: Add "After Upload" results window
- **Property Grid**: Add ApplicationConfig property grid
- **CLI**: Add `verify-recording` command for automated screen recording validation
- **Editor**: Unify editor undo history across different toolsets
- **Architecture**: Move Windows-specific P/Invoke types to dedicated Platform.Windows project
- **FFmpeg**: Improve FFmpeg download/config UX with progress hooks and better path resolution
- **Documentation**: Replace ShareX.Avalonia references with XerahS (#44)
- **Workflow**: Update cursor handling (#43)
### Bug Fixes
- **Recording**: Improve GIF recording quality, add clipboard support, pause, and stroke-based abort
- **After Upload**: Fix window theming and errors
- **Rendering**: Fix speech balloon tail geometry rendering
- **Region Capture**: Fix system cursor appearing in screenshots and hotkey issues (#38, #39)
## v0.5.0 - Core Capture & Editor Improvements
### Features & Improvements
- **Capture**: Add single instance enforcement for the application
- **Region Capture**: Enhance crosshair visibility, add magnifier pixel sampling, and hide system cursor when ghost cursor active
- **Editor**: Wire ImageEffectsViewModel to unified undo/redo stack
- **UX**: Set default file picker location to Desktop for easier access
### Bug Fixes
- Fix 11+ HIGH/MEDIUM priority issues including null safety and resource management
- Set RegionCaptureControl cursor to None to prevent double cursor visibility
## v0.4.0 - Image Effects & Tools
### Features & Improvements
- **Image Effects**: Refactor preset management and improve effects UI
- **Tools**: Add QR code generator/decoder and Color Picker tools with standard color name mapping
- **Watch Folders**: Implement Watch Folder system with per-folder workflow assignments
- **Indexer**: Add Index Folder preview and modernize HTML output using WebView
- **macOS**: Add native ScreenCaptureKit video recording support
### Bug Fixes
- **Capture**: Fix cursor tracking and visibility during GDI capture
- **Capture**: Fix NullReferenceException in DXGI capture by preventing premature disposal of D3D11 device context
## v0.3.0 - Modern Capture Architecture
### Features & Improvements
- **Modern Capture**: Implement DXGI-based high-performance screen capture for Windows
- **Screen Recording**: Unified recording pipeline with Windows Media Foundation and FFmpeg support
- **Workflow System**: Major overhaul of hotkeys into full Workflow system with GUID persistence
- **Toast Notifications**: New custom Avalonia-based notification system with advanced settings
- **Linux**: Initial support for Wayland via XDG Desktop Portal and native X11 capture
- **Settings**: Add weekly backup system for application settings
- **UX**: Add tray icon support with customizable click actions
### Bug Fixes
- **Modern Capture**: Fix multi-monitor blank capture issues
- **Region Capture**: Fix DPI handling, coordinate mapping, and offsets/scaling on multi-monitor setups
- **Code Quality**: Massive code audit fixing 500+ license headers and 160+ nullability issues
- **Windows**: Standardize Windows TFM and fix CsWinRT interop issues
## v0.2.0 - macOS Support & Plugin System
### Features & Improvements
- **macOS**: Initial platform support including ScreenCaptureKit, SharpHook hotkeys, and app bundling
- **Plugins**: Implement dynamic plugin system with packaging (`.sxap`), CLI tools, and `.sxadp` file association
- **History**: Switch history storage from XML to SQLite with automatic backups
- **Editor**: Integrate ShareX.Editor as core component with SkiaSharp rendering
## v0.1.0 - Initial Feature Set
### Core Features
- **UI**: Reimagined interface with two-toolbar system and modern dark theme
- **Capture**: Region, Fullscreen, and Window capture modes
- **Annotations**: Object-based editor with Rectangle, Ellipse, Arrow, Line, Text, Number, Crop tools, and full Undo/Redo support
- **Hotkeys**: Global hotkey system with Win32 registration
- **Image Effects**: Initial implementation of 40+ effects including Resize, Shadows, and Gradients
- **History**: Basic task history tracking
---
*This changelog follows Semantic Versioning while the project remains in pre-release (0.x.x).*
