# XerahS Lessons Learnt

This document serves as a centralized knowledge base for technical challenges, architectural decisions, and platform-specific quirks encountered during the development of XerahS.

When a task produces a durable correction or preventive rule, capture it here or in the closest topic-specific lessons file using this format:

```md
- Never ...; always ... because ...
```

Promote only repository-wide policy changes to `AGENTS.md`.

- When slimming agent instructions, preserve explicit identity mappings and operational requirements; verify that moved rules exist at their destination because shorter wording can silently weaken mandatory Git wrappers or lose verification exceptions.

## table of Contents

1.  [UI & Theming](#ui--theming)
2.  [Changelog & Documentation Tooling](#changelog--documentation-tooling)
3.  [Build & Configuration](#build--configuration)
4.  [Plugin System](#plugin-system)
5.  [Android / Avalonia](#android--avalonia)

---

## UI & Theming

- Never fix post-migration dark-surface regressions one view at a time; always start by fixing the first painted host surface (`SurfaceWindow` / `PageView`) and use a separate `OverlayWindow` base for transparent cases, then extend `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` only for missing neutral compatibility brushes because Avalonia templates can still fall back to black even when child layouts look correct.
- Never assume explicit `TextBox.Background` is enough for read-only previews; always map the `TextControl*ReadOnly` and related Fluent resource keys in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` because Avalonia's read-only text templates can bypass the normal editable text brushes and fall back to black.
- Never use outer `Margin` on the first child of a `UserControl` to create themed gutters; always use a painted root `Border` with `Padding` because `UserControl` itself does not own a background and transparent gutter space will fall through to the host surface.
- Never rely on `VerticalScrollBarVisibility="Visible"` by itself when a scrollbar must stay fully shown; always pair it with `AllowAutoHide="False"` and prefer setting that once in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` because the Fluent `ScrollViewer` template can still collapse the bar until hover.
- Never put `Padding` on a `ScrollViewer` that wraps a `StackPanel`; always move it to the inner element as `Margin` because `ScrollViewer.Padding` shrinks the *viewport* but is **not** added to the scroll extent — so the bottom `padding`-worth of content becomes permanently unreachable no matter how far the user drags the scrollbar.
- Never use `SplitView` as a two-column shell when the content column must have a bounded (finite) height; always replace it with a plain `Grid ColumnDefinitions="auto,*"` because `SplitView` derives from `ContentControl` whose default `VerticalContentAlignment=Top` causes the internal `ContentPresenter` to pass an infinite (`∞`) height constraint to its child — which prevents any nested `ScrollViewer` from activating.
- Never use `TransitioningContentControl` as a page host when the page contains a `ScrollViewer`; the animation `Panel` inside `TransitioningContentControl` passes `∞` height during its measure pass, breaking the bounded constraint that `ScrollViewer` requires to activate — use a plain `ContentControl` with `HorizontalContentAlignment="Stretch" VerticalContentAlignment="Stretch"` instead.
- Never leave `TabControl.VerticalContentAlignment` at its default (`Top`) when tab content contains a `ScrollViewer`; always set `VerticalContentAlignment="Stretch"` on the `TabControl` because the internal `ContentPresenter` templates it to `{TemplateBinding VerticalContentAlignment}` — without `Stretch`, the presenter passes `∞` height to each `TabItem` body and the `ScrollViewer` never activates.
- Never rely on `Classes="accent"` being added manually to every new button; always make accent the default in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` and use semantic opt-out classes such as `NoAccent`, `SettingsRow`, or `ColorSwatchButton` because Avalonia Fluent keeps ordinary buttons neutral unless the app supplies a shared default.
- Never bind XerahS views directly to raw `SystemAccentColor` when a shared app accent brush already exists; always consume `AccentFillColorDefaultBrush` / `XerahS.Brush.Accent.*` from `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` because that keeps future accent-foreground and opacity tuning centralized instead of scattered through individual controls.
- Never hardcode accent colours or reference `ShareX.Color.Accent.Start` / `ShareX.Color.Accent.End` in new brush definitions; always use `SystemAccentColor` / `SystemAccentColorLight1` / `SystemAccentColorDark1` from Avalonia's platform resources because those resolve to the user's OS accent colour on every platform (Windows personalisation, macOS accent, Linux default blue fallback) and keep all accent-coloured controls — buttons, checkboxes, focus borders, list highlights — visually consistent regardless of which machine the app runs on.
- Never apply XerahS-wide control styles directly to every `Window` or `Button`; always scope them through a root class such as `xerahs-surface` on `PageView` / `SurfaceWindow` and explicitly remove that class from `EditorWindow` because the embedded `ShareX.ImageEditor` owns its own theme contract and app-level selectors will otherwise bleed into the editor.
- Never drive XerahS app-level resources with `ShareX.ImageEditor` custom theme variants; always set `Application.RequestedThemeVariant` to Avalonia's built-in `ThemeVariant.Light` / `ThemeVariant.Dark` and feed `ShareXDark` / `ShareXLight` only into the editor theme manager, because XerahS theme dictionaries and third-party controls expect the standard Avalonia variants.
- Never redefine `ShareX.ImageEditor` theme resource keys such as `ShareX.Brush.Accent` or `ShareX.FontFamily.*` in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml`; always keep XerahS-owned palette tokens under `XerahS.*` and let editor hosts load `ImageEditorTheme.axaml` because upstream editor changes can alter those `ShareX.*` resource contracts and types (for example the accent brush becoming a gradient driven by platform accent tracking).
- Never mirror `ShareX.ImageEditor` theme changes at the XerahS window level once `EditorView` owns system theme and accent tracking; always pass the current app theme mode through `ImageEditorOptions.UseSystemTheme` / `UseSystemAccentColor` and let the editor refresh its own `ThemeVariantScope`, because duplicate host subscriptions drift out of sync with upstream editor theme behavior.
- Never merge `ImageEditorTheme.axaml` into a non-editor XerahS surface without also setting that host's `RequestedThemeVariant` from `ShareX.ImageEditor.Presentation.Theming.ThemeManager`; those `ShareX.*` resources live under `ShareXDark` / `ShareXLight`, so RegionCapture-style hosts must bridge the editor theme variant explicitly or neutral brushes can fall back to the wrong palette.
- Never duplicate semantic control classes like `section-header`, `caption`, `readonly`, or status colors inside individual views; always define them once in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml` and back them with palette tokens in `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Theming/ImageEditorTheme.axaml` because local copies stop whole-app theme changes from propagating consistently.
- Never store a border thickness token as `x:Double` when it will feed `BorderThickness`; always declare it as `Thickness` because Avalonia style setters will reject a numeric resource value for a `Thickness` property at runtime even when inline `BorderThickness="1"` looks valid.
- Never bind workflow-edit dialogs directly to the live `WorkflowSettings` instance; always edit a working copy, apply it only on `OK`, and show the real job separately from the custom description because otherwise `Cancel` is not real and workflow names can silently drift away from the task they actually execute.
- Never use decorative Unicode glyphs in button labels, status text, or debug prefixes unless the file already intentionally depends on them and the round-trip encoding has been verified; always prefer ASCII-safe labels such as `...`, `[OK]`, `[ERROR]`, and `[FAIL]` because editor and PowerShell write paths can silently turn those glyphs into mojibake in source and UI.
- Never make update prompts or installers depend on the main window being visible; always fall back to an ownerless update window and expose a manual `Update Now` action in Application Settings because tray-only and startup timing flows can legitimately have no visible main window when an update is ready.

### ContextMenu vs. ContextFlyout

**Issue**: The old warning against `ContextMenu` was specific to `FluentAvaloniaTheme`. XerahS now uses the official Avalonia `FluentTheme`, so standard `ContextMenu` rendering is no longer blocked by that theme-specific limitation.

**Solution**: Use `ContextMenu` for ordinary context menus. Keep `ContextFlyout` with `MenuFlyout` for cases that need richer flyout behavior, shared popup content, or a flyout attached to a non-standard host (e.g., a button that always opens its flyout below).

**✅ Plain context menu**:
```xml
<Border.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Action" Command="{Binding MyCommand}"/>
    </ContextMenu>
</Border.ContextMenu>
```

**✅ Flyout for richer behavior**:
```xml
<Button.Content>
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="More" />
        <Path Data="{StaticResource IconMore}" />
    </StackPanel>
</Button.Content>

<Button.ContextFlyout>
    <MenuFlyout>
        <MenuItem Header="Advanced action" Command="{Binding AdvancedCommand}" />
    </MenuFlyout>
</Button.ContextFlyout>
```

### Binding in DataTemplates with Flyouts

**Issue**: When using `ContextFlyout` or `ContextMenu` inside a `DataTemplate`, bindings to the parent logic (ViewModel) fail because Popups/Flyouts exist in a separate visual tree, detached from the `DataTemplate`'s hierarchy.

**Solution**: Use the `$parent[UserControl]` reflection binding syntax to reach the main view's DataContext.

```xml
<DataTemplate x:DataType="local:MyItem">
    <Border>
        <Border.ContextFlyout>
            <MenuFlyout>
                <!-- Bind to parent UserControl's DataContext -->
                <MenuItem Header="Edit" 
                          Command="{Binding $parent[UserControl].DataContext.EditCommand}"
                          CommandParameter="{Binding}"/>
            </MenuFlyout>
        </Border.ContextFlyout>
    </Border>
</DataTemplate>
```

**Key Points**:
- Use `$parent[UserControl].DataContext` to access the View's ViewModel from within a flyout.
- `CommandParameter="{Binding}"` passes the current data item (the DataTemplate's DataContext).
- For shared flyouts, define them in `UserControl.Resources` and reference via `{StaticResource}`.

### WebView Helper

**Context**: Rendering HTML content within the application (e.g., for Indexer previews).

**Issue**: The standard `WebView.Avalonia` package is insufficient on its own for desktop applications. It provides the controls but may lack the necessary desktop-specific native bindings or initialization logic required for Windows/Linux/macOS.

**Solution**: You must reference **`WebView.Avalonia.Desktop`** in addition to the base package.

**❌ Incorrect**:
```xml
<PackageReference Include="WebView.Avalonia" Version="11.0.0.1" />
```

**✅ Correct**:
```xml
<PackageReference Include="WebView.Avalonia" Version="11.0.0.1" />
<PackageReference Include="WebView.Avalonia.Desktop" Version="11.0.0.1" />
```

Without the `.Desktop` package, the `WebView` control may fail to initialize or render, often silently or with generic "type not found" errors when using reflection to locate it.

### RegionCapture and ImageEditor Resource Contracts

- Never walk the Avalonia visual/logical tree on every settings search keystroke; always debounce and search an immutable cached index (catalog + one-time visual checkbox scan) because tree walks on the UI thread make typing feel laggy as settings pages grow.
- Never bind Avalonia buttons to `RelayCommand<T>` with `CommandParameter="{Binding SelectedItem}"` when enablement depends on that selection; always use a parameterless command that reads the selected property and call `NotifyCanExecuteChanged` on selection change because Avalonia often does not re-query `CanExecute` when only the parameter changes.
- Never host `AnnotationToolbar` against a custom `IAnnotationToolbarAdapter` without supplying `VisibleToolbarItems` (and handling tool clicks for non-`EditorToolbarAdapter` hosts); always mirror the editor's toolbar item list for region-capture overlays because the shared toolbar binds tools via `ItemsSource`/`ReflectionBinding`, not hardcoded buttons.
- Never host `AnnotationToolbar` as a centered size-to-content control without a finite width cap on Avalonia 12.1+; always stretch the host (or bind `MaxWidth` to the overlay/editor root `Bounds.Width`) and cap the toolbar chrome `ScrollViewer` to that width because otherwise trailing tool buttons clip with no horizontal scrollbar.
- Never leave the `ShareX.ImageEditor` submodule on a detached/older HEAD after `git pull`; always `git submodule update --init` (or checkout the parent gitlink SHA) because app hosts compile against the pointer API (`BorderStyle`, `VisibleToolbarItems`, etc.) and a stale checkout silently empties or breaks the annotation toolbar.
- Never use Avalonia's fake headless drawing for icon-font smoke tests; always use Skia-backed headless mode (`UseSkia()` and `UseHeadlessDrawing = false`) because glyph resource failures only surface when the font pipeline is actually exercised.
- Never apply an icon font to every `TextBlock` below an Avalonia toolbar button; always scope icon typography to the button's content presenter because tooltip text can share the button's logical ancestry and must retain a normal text font.
- Never rely on Button `FontFamily` inheritance for Lucide/Content icon glyphs when a global `TextBlock` style sets `FontFamily`; always restyle `Button TextBlock` (bind to `$parent[Button].FontFamily` or set `ShareX.FontFamily.Icon` on `Button.toolbar-button TextBlock`) because Style priority outranks Inherited and blank PUA glyphs look like invisible toolbar buttons.
- Never let feature work alter or bypass existing `ShareX.ImageEditor` theme resources, variants, or bindings unless the task explicitly targets them; always treat theme behavior and visual resource contracts as non-regression requirements because unrelated UI changes can silently break dark/light presentation across the editor.
- Never make XerahS host startup responsible for prewarming editor wallpaper conversions; always let `ShareX.ImageEditor` request the desktop wallpaper during `MainViewModel` initialization because Linux wallpaper conversion/caching belongs to the editor integration contract and must work consistently across every host, not just XerahS.
- Never collapse Linux modern region-capture failure and user cancellation into the same `null` outcome; always preserve cancellation separately and fall back to the XerahS overlay only for unsupported or failing backends because otherwise `UseModernCapture=true` can block X11 region capture on older desktops.
- Never force `UseModernCapture=false` for every Linux `CaptureRectAsync`; always scope that downgrade to the overlay fallback flow because direct rect capture on capable X11 desktops should preserve the native portal path.
- Never move the XDG portal to the front of every X11 region-capture waterfall; always require a desktop-native backend signal (for example KDE, GNOME, LXQt, or XApp) because generic GTK-backed X11 portal sessions can still hang or misroute captures.
- Never define Tmds.DBus proxy interfaces as nested or inaccessible types; always expose them as top-level public interfaces because the dynamic proxy assembly cannot implement inaccessible interfaces.
- Never trust region-capture modifier updates to key events alone; always resample the current `KeyModifiers` from pointer movement/release while dragging because modifier-only transitions can be missed under pointer capture and leave the selection geometry stuck in the wrong mode.
- Never advertise Linux selector modes that the current session cannot actually honor, and never let an explicit selector silently fall through to a different interactive backend; always filter the UI using live selector diagnostics and keep `Automatic` as the only cross-backend fallback mode because otherwise specific selector choices become misleading and bug reports get polluted by fallback behavior.
- Never let `src/desktop/app/XerahS.RegionCapture/Platform/Windows/NativeWindowService.cs` use a weaker inclusion filter than `src/platform/XerahS.Platform.Windows/WindowsWindowService.cs`; always exclude cloaked, no-activate, disabled, and known system-class surfaces because hidden Windows shells like Settings hosts or Windows Input Experience can still report `IsWindowVisible=true` and steal crosshair window preselection.


---

## Changelog & Documentation Tooling

- Never document distro-default FFmpeg capabilities or GUI setting locations from assumption; always verify the shipped code path and the current upstream/package reality first because FFmpeg device support and XerahS settings entry points drift independently and user-facing docs can become false even when the feature itself exists.
- Never use `git tag -l | Sort-Object -Descending` to find the latest release tag; always use `git tag -l --sort=-version:refname` or (preferred) `mcp_io_github_git_list_releases` filtering for `prerelease:false, draft:false` because plain lexicographic sort puts `v0.7.7` after `v0.20.5`.
- Never attempt `replace_string_in_file` on multi-line changelog blocks; always use PowerShell `[System.IO.File]::ReadAllText` + `[System.Text.RegularExpressions.Regex]::Replace` with `(?s)` dotall mode because the changelog can contain multi-byte UTF-8 sequences such as `§` that get rewritten into mojibake text during a bad encode/decode round-trip, breaking exact-text matching.
- Never forget a mojibake normalization pass after a PowerShell `WriteAllText` to a changelog; always run `$c = $c.Replace([char]0x00C2 + [char]0x00A7, [char]0x00A7)` before writing because mojibake text for the section-sign character can slip through even when the source text looked correct.
- Never leave raw `\n{3,}` runs in CHANGELOG.md after regex block removal; always normalize with `-replace "\n{3,}", "\n\n"` (on LF-normalized content) because removing multi-line sections leaves stray blank lines that accumulate across consolidations.
- Never create separate changelog headings for each prerelease tag between two stable releases; always consolidate all prerelease sections into a single heading for the stable tag, using `git log <prev_stable>..<latest_stable> --oneline --no-decorate` to enumerate commits that belong under that heading.
- Never audit a `ShareX.ImageEditor` gitlink target from a separately assumed upstream clone alone; always initialize the workspace submodule and inspect its configured remote first because XerahS can pin commits that are reachable from the actual submodule remote even when a different GitHub mirror or fork does not advertise them.

---

## Build & Configuration

- Never assume `XerahS.Platform.Windows` changes are unverifiable on Linux; always compile them with `-p:OS=Windows_NT -p:EnableWindowsTargeting=true -p:CsWinRTGenerateProjection=false` (restore first with the same properties) because the project otherwise skips compilation off Windows and `cswinrt.exe` cannot run there.
- Never register Windows autostart in both the installer Startup folder and the runtime Run key; keep one authoritative entry, migrate the legacy entry, and treat its single-instance relay as passive because duplicate login launches can otherwise restore a window that startup just hid to the tray.
- Never keep the existing patch/minor version when implementing a brand-new product feature that did not previously exist; always bump the app minor version in root `Directory.Build.props` first and use that bumped version in the commit prefix because new feature surfaces should start a new minor release line.
- Never infer the GitHub release target with bare `gh repo view` on a KovaForge fork checkout; always resolve from the `origin` remote URL (including `git@github-<alias>:Owner/Repo.git`) or pass `--repo owner/name`, because `gh` often returns upstream `ShareX/XerahS` instead of `KovaForge/XerahS`.
- Never apply one release-channel policy to both remotes; always treat `ShareX/XerahS` as pre-release and `KovaForge/XerahS` as full latest unless an explicit `--set-prerelease` / `--no-prerelease` override is requested.
- Never let interim macOS ad-hoc codesign hard-fail the release matrix on unsigned nested managed DLLs; always use `--deep` and non-fatal verify for the ad-hoc path, because a single macos-15 codesign failure skips asset upload even when Windows/Linux builds succeeded.

### Windows TFM & CsWinRT Behavior (Net10.0-windows)

**Context**: When implementing modern Windows features using `Microsoft.Windows.CsWinRT` in a project targeting .NET 8/9/10.

**Issue**: Using the generic `net10.0-windows` TFM combined with a separate `<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>` property works for **individual** project builds but fails during **full solution** builds with "Windows Metadata not provided" errors. This is due to a transitive dependency resolution issue in the CsWinRT targets file.

**Solution**: Use the **explicit TFM** string which combines the framework and the platform version.

**❌ Incorrect configuration for solution builds**:
```xml
<TargetFramework>net10.0-windows</TargetFramework>
<TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>
```

**✅ Correct configuration**:
```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

This forces the build system to include the correct Windows SDK reference assemblies natively, avoiding the metadata resolution failure. This is required for reliable solution-wide builds when using WinRT APIs like `Windows.Graphics.Capture`.

- Never assume `npm ci` can always clear `ShareX.VideoEditor/frontend/node_modules` on Windows; always delete that folder and rerun the build when `ENOTEMPTY` appears because file locks in `node_modules` can make the first clean fail even though the project itself is valid.
- Never let `XerahS.App` or `XerahS.CLI` publish transitive `ShareX.VideoEditor/frontend/dist` assets directly; always remove those `ResolvedFileToPublish` entries and copy the Web UI once after `Publish` because duplicate Video Editor frontend publish items trigger `NETSDK1152` on Windows and macOS release packaging.
- Never pair `.WithDeveloperTools()` with `AttachDeveloperTools()` in XerahS DEBUG startup; always keep exactly one developer-tools attachment path in the application layer because Avalonia 12 throws when DevTools are attached twice.
- Never call `UseSkia()` in an Avalonia 12 headless/test host without also configuring `Avalonia.HarfBuzz` and `.UseHarfBuzz()` because Skia-only builders no longer get text shaping automatically.
- Never pin managed `SkiaSharp` to a major version higher than the corresponding `SkiaSharp.NativeAssets.*` transitive; always pin every `SkiaSharp.NativeAssets.<rid>` package consumed (Linux at minimum, plus Win32/macOS/WASM if used) in `Directory.Packages.props` so the native `libSkiaSharp.so` matches the managed assembly's expected native range, otherwise Avalonia's transitives will pull the older 3.x native and crash with `SkiaSharpVersion.CheckNativeLibraryCompatible` at startup (XIP-0081).
- Never assume Photino `file://` video playback works with default web security, FFmpeg `drawtext` works without a `fontfile` on Windows, or UI export always preserves source FPS; always disable Photino web security for local recordings, inject a resolved system font into watermark filters, treat `OutputFps <= 0` as preserve-source, and drive advertised trim/crop/convert/watermark through `VideoEditorAutomationService` before adding new Photino launch tests.
- Never drop FFmpeg stderr or advertise WebM/GIF/WebP without probing encoders; always keep a short stderr tail in export failures, probe `-encoders` once per FFmpeg path, fall back WebM from `libvpx-vp9` to `libvpx`, and burn image watermarks with `-filter_complex` overlay rather than leaving `WatermarkSettings.ImagePath` unused.

---

## Plugin System

### Pure Dynamic Loading

**Context**: Implementing a plugin architecture where extensions are loaded at runtime without compile-time references.

**Lessons Learned**:
1.  **Don't mix paradigms**: Attempts to mix static compilation (direct project references) with dynamic loading (`AssemblyLoadContext`) cause type identity conflicts. Types loaded via ALC are distinct from the "same" types loaded via normal reference, even if the DLL is identical.
2.  **Keep contexts alive**: The `PluginLoader` must maintain a static reference to the created `AssemblyLoadContexts`. If these are garbage collected, the plugin assemblies will be unloaded, causing crashes or missing functionality.
3.  **Share framework dependencies**: Plugins must not ship with their own copies of framework assemblies (e.g., `Avalonia.dll`, `CommunityToolkit.Mvvm`). The `PluginLoadContext` must be configured to return `null` for these shared assemblies, forcing the runtime to resolve them from the Host application's context. This ensures that `Plugin.Button` is compatible with `Host.Button`.
4.  **Templating limitations**: In Avalonia, overriding `ControlTemplate` in a plugin requires careful Command wiring, as standard resource lookup chains may specific to the load context.
5.  **Plugin TFM must match Host TFM**: Plugin projects must use the **exact same Target Framework Moniker (TFM)** as the host application. If the host targets `net10.0-windows10.0.19041.0` on Windows, plugins must also use conditional TFM matching:

    ```xml
    <!-- Plugins must match host TFM exactly -->
    <TargetFramework Condition="'$(OS)' == 'Windows_NT'">net10.0-windows10.0.19041.0</TargetFramework>
    <TargetFramework Condition="'$(OS)' != 'Windows_NT'">net10.0</TargetFramework>
    ```

    **Why**: Plugin build targets that copy outputs to the host's bin folder (e.g., `$(TargetFramework)\Plugins\`) will use the plugin's TFM in the path. If the plugin targets `net10.0` but the host outputs to `net10.0-windows10.0.19041.0`, plugins end up in the wrong folder and fail to load at runtime. This causes provider settings UI to not appear.

- Never turn the characters before a filename token into an S3 explorer folder by blindly appending `/`; always scope ListObjectsV2 to the last complete directory before the token, expose paths relative to that logical root, and retain the full object key in item metadata because embedded tokens otherwise produce nonexistent prefixes and absolute paths duplicate the generic explorer's Root breadcrumb.

---

## Android / Avalonia

### Avalonia Android: App Stuck at "Initializing..." or Blank Screen

**Context**: XerahS.Mobile.Ava (Avalonia UI on Android) showed a perpetual loading screen or blank screen even though initialization and navigation logic ran correctly.

**Root cause**: In `MainActivity.OnCreate`, code was setting `parent.Content = null` where `parent` was the host `ContentControl` that contains Avalonia's `MainView`. That removed the entire Avalonia UI from the visual tree, so nothing (loading view or main view) was visible.

**Lesson**: Do **not** clear the content of the control that hosts `ISingleViewApplicationLifetime.MainView`. If the app seems stuck on loading or blank but logs show init and navigation completing, look for platform code (e.g. in the Activity) that modifies the host's `Content`.

**MAUI**: MAUI has no equivalent host-Content bug. For MAUI white screen / loading not visible, defer starting `InitializeCoreAsync` by ~150 ms in `MainActivity.OnCreate` so the loading page can render before background init runs. See [android_avalonia_init_fix.md](android_avalonia_init_fix.md#maui-equivalent-no-host-content-bug).

---

## Image / Preview Ownership

### Clone Task Bitmaps Before `UpdatePreview`

**Context**: `ShareX.ImageEditor.Presentation.ViewModels.MainViewModel.UpdatePreview` is used to show captured task images in the desktop editor surface.

**Lesson**: Treat `UpdatePreview` as an ownership transfer. Do not read from or keep sharing the same `SKBitmap` after calling it. `UpdatePreview` can trigger property-change flows that dispose or replace the supplied bitmap during the same call. When the source bitmap still belongs to a task or another component, clone it first and hand the clone to the view model.

### ImageEditor Host Export Wiring

- Never partially wire a hosted component's host-facing commands/events; always audit the full host contract and connect every supported action because UI enablement and behavior can depend on subscriber presence, making omissions look like broken features instead of integration gaps.
- Never put shared editor wallpaper lookup or prewarm behind `XerahS.Platform.Abstractions`; always keep the default Windows/Linux/macOS wallpaper services in `ShareX.ImageEditor.Hosting` and let hosts opt into them through `EditorServices.EnsureDefaultDesktopWallpaperService()` because the editor is shared across standalone hosts and third-party apps, not just XerahS.
- Never use the XerahS `[vX.Y.Z]` commit prefix when committing inside `ShareX.ImageEditor` or other shared library submodules; always use `[Type] Use concise description` there because those libraries are versioned independently of the XerahS app.
- Never design a new `.sxie` loading path from scratch without first checking `src/desktop/core/XerahS.Core/Helpers/ImageEffectPresetSerializer.cs`, `src/desktop/core/XerahS.Common/Helpers/LegacyImageEffectImporter.cs`, and `src/desktop/app/XerahS.UI/ViewModels/ImageEffectsViewModel.cs`; always reuse the existing `.xsie`/legacy `.sxie` preset pipeline where possible because XerahS already serializes, imports, and instantiates `ShareX.ImageEditor.Core.ImageEffects.ImageEffect` objects.

### RegionCapture Toolbar Parity

- Never model the RegionCapture overlay toolbar as one shared annotation-options state. Mirror `ShareX.ImageEditor`'s per-tool option matrix instead, including `Select` reflecting the currently selected annotation type, or tools like Highlight, Smart Eraser, Rectangle, Text, and Step will silently expose the wrong controls and create annotations with mismatched defaults.
- Never reimplement RegionCapture effect-tool behavior with host-only state when `ShareX.ImageEditor.Core.Editor.EditorCore` already exposes the needed pipeline hooks; always reuse `EditorCore.SampleCanvasColor`, `Annotation.HitTest`, and `BaseEffectAnnotation.UpdateEffect(...)` for Smart Eraser, Spotlight, Blur, Pixelate, and Magnify because local one-off paths drift from ImageEditor and silently break tool-specific editing behavior.
- Never keep duplicate annotation toolbar controls or view-owned effect fill logic in `XerahS.RegionCapture` when `ShareX.ImageEditor` is the lower shared dependency. Put the shared toolbar in `ShareX.ImageEditor.Presentation.Controls` and the bitmap-backed effect brush updater in `ShareX.ImageEditor.Presentation.Rendering`, then have both `EditorView` and `OverlayWindow` consume those shared pieces so button availability and Blur/Pixelate/Magnify rendering stay aligned.

---

## Linux Capture UX

### Separate Linux Selector Preference From `UseModernCapture`

**Context**: Linux region capture can succeed through several different interactive selectors depending on the session stack: XerahS overlay, XDG portal, desktop-native D-Bus selectors, or `slurp`.

**Lesson**: Do not treat `UseModernCapture` as the only Linux UX decision. Keep it as the broad capture-engine toggle, but layer any user-facing Linux selector choice on top as a more specific preference. Runtime code should:

- allow explicit selector preferences to opt into a native selector even when `UseModernCapture` is off for the general workflow,
- preserve safe overlay fallback on X11 when the chosen native path is unavailable or fails,
- stamp overlay follow-up rect/fullscreen captures with `LinuxRegionSelectorPreference = XerahSOverlay` so later Linux crop steps stay on the legacy path instead of accidentally re-entering portal/native logic,
- expose live diagnostics in the UI (`session`, `portal backend`, `available selectors`, `automatic will prefer`) so users can make informed choices without understanding the full Linux capture stack.

### Drain Portal Hotkey Rebind Work Before Dispose

**Context**: Editing workflows or hotkeys on Wayland can trigger debounce-driven portal rebinds while the `WaylandPortalHotkeyService` is also being torn down.

**Lesson**: Never dispose portal hotkey D-Bus state while debounce or rebind work can still be running. Mark the service as disposed first, cancel the debounce token, and wait for in-flight rebind tasks to drain before releasing the connection, session, or semaphore. Otherwise workflow edits can surface unobserved `ObjectDisposedException` failures against `Tmds.DBus.Connection`.

- Never keep `Material.Avalonia` or `Material.Icons.Avalonia` referenced in `XerahS.Mobile.Ava` unless the app actually imports and uses that theme stack; always remove dead UI packages because stale theme dependencies create fake Avalonia upgrade work and complicate Android validation for no runtime benefit.

### Predict Portal Request Paths Before Waiting

**Context**: Some `xdg-desktop-portal` calls can publish their `Request.Response` signal quickly enough that a watcher attached only after the method returns will miss the signal and leave the app waiting forever.

**Lesson**: Never wait on an XDG portal request only after the call returns its request handle; always provide a `handle_token`, derive the expected request object path from the D-Bus unique name, and attach the response watcher first because fast portal responses can beat a post-call subscription.

### Surface The Last Linux Selector Decision In Diagnostics

**Context**: Static Linux selector diagnostics explain what should be available in the current session, but they do not show which selector actually handled the last capture.

**Lesson**: When exposing Linux selector diagnostics, always carry the most recent runtime decision as well as the static capability snapshot. Native providers should record their exact winning provider ID, overlay fallbacks should record their own win in the UI wrapper, and diagnostics should surface whichever decision happened most recently so a stale native result cannot survive after an overlay fallback.

- Never build Linux hover-snap window lists from raw X11 root children when KDE/GNOME parity matters; always prefer EWMH managed-window metadata such as `_NET_CLIENT_LIST_STACKING`, `_NET_WM_NAME`, and `_NET_FRAME_EXTENTS` because KWin and Mutter can expose undecorated client windows there while frame/root-child heuristics silently drop titles or return the wrong bounds.
- Never let a Linux XerahS overlay follow-up capture reopen the XDG portal after the user already drew a region on GNOME Wayland; always stamp that post-selection capture with an explicit no-portal-reentry guard and route it through GNOME D-Bus area/full-screen fallbacks because a second portal dialog breaks the crosshair-overlay contract and regresses region capture UX.

### Keep Scrolling Capture Target Selection Consistent

**Context**: Windows scrolling capture now routes `WM_VSCROLL` commands to the actual child window that owns the content scrollbar instead of blindly sending messages to the top-level window.

**Lesson**: Once scrolling capture resolves a child scroll target, every related scrollbar query must use that same resolved handle as well. If scrolling commands hit the child window but `GetScrollInfo` still reads the parent handle, bottom detection can trip early and truncate the stitched capture before the real scroller reaches the end of the page.

- Never pick the first descendant with `WS_VSCROLL` as the scrolling-capture target on Windows; some apps expose standalone `ScrollBar` controls before the real content pane, so target resolution must prefer the largest visible non-`ScrollBar` scroller and only fall back to a scrollbar control when no better candidate exists.
- Never deliver scrolling-capture wheel input to the geometric center of the selected window by default; browser-based UIs can place nested scroll regions there, so wheel-based scrolling should use a capture-region-aware anchor point that is biased toward the primary content lane instead of the most central child surface.
- Keep Flatpak CI validation aligned with the manifest-installed icon bucket. If `flatpak-builder` exports `share/icons/hicolor/512x512/apps/com.xerahs.XerahS.png`, the release workflow must validate that exact path instead of an older `256x256` path.

### Verify XIP Claims Against Current Source Before Implementing

**Context**: XIP0078 (written 2026-06-13) claimed the macOS bridge lacked `SCScreenshotManager` and that `sck_capture_window` had no `[LibraryImport]`. By implementation time (2026-07-07) both were already present; the real gap was that nothing *called* the window-capture import.

**Lesson**: Always re-verify an improvement plan's file:line claims against the current branch before implementing it; plans go stale fast in an active repo. Implementation-specific notes: macOS `CFStringRef` framework constants (CGWindow keys, AX options) should be resolved via `dlsym` + `Marshal.ReadIntPtr` rather than hardcoding their string contents; `codesign` cannot sign managed PE assemblies, so bundle-signing loops must filter to Mach-O via `file(1)`; and MSBuild targets that must also run during Windows cross-compilation cannot shell out to `sed` - use `$([System.IO.File]::ReadAllText(...).Replace(...))` property functions instead.

### Linux UI Features That Need Platform.Linux Must Use Conditional Compile

**Context**: XIP0079 P3 post-exit clipboard persistence and settings hints live in `Platform.Linux`, but `XerahS.UI` is built on macOS/Windows without that project reference.

**Lesson**: Use `IsLinuxUiBuild` plus conditional `<Compile Include=...>` for Linux-only partials (`AvaloniaClipboardService.LinuxPersistence.cs`, `SettingsViewModel.LinuxClipboard.cs`) and `#if LINUX` in shared view models. Default-interface methods on cross-platform abstractions (`IHotkeyService.GetDiagnostics()`) keep Windows/macOS builds unaffected without extra references.

### Flatpak Sandbox Denials Must Never Escape On The Avalonia UI Thread

**Context**: Issue #270 — the Flatpak build crashed ~1 second after startup on KDE Plasma (Bazzite) while the same binary ran fine unsandboxed. Avalonia's `DBusTrayIconImpl` owns `org.kde.StatusNotifierItem-{pid}-{id}` before registering with `org.kde.StatusNotifierWatcher`; the Flatpak session-bus proxy denied `RequestName` (manifest only granted `--talk-name`), and the `DBusErrorReplyException` from the resulting `async void` continuation crashed the process. GNOME validation VMs never caught it because without a StatusNotifierWatcher on the bus the tray code path never runs.

**Lesson**: Avalonia's dispatcher swallows an exception only when `UnhandledExceptionFilter` sets `RequestCatch = true` AND an `UnhandledException` handler sets `Handled = true`; subscribing to the filter alone is a no-op. Treat Linux desktop-integration failures (Tmds.DBus exceptions, `Avalonia.FreeDesktop` frames) as non-fatal log-and-continue. Reproduce Flatpak issues against a session bus that actually has a StatusNotifierWatcher (KDE/XFCE) — its absence silently disables the failing code path. Also never classify a startup exception as a "display error" by matching `Avalonia.X11` in the stack trace: every UI-thread exception unwinds through those frames.

### Compile New NUnit Tests Before Broad Verification

- Never assume NUnit attributes are globally imported in `XerahS.Tests`; always include `using NUnit.Framework;` in a new test file and run its focused filter first because otherwise the full dependency build finishes before revealing a trivial test-compilation error.
- Never put backslash-escaped `TimeSpan` patterns directly inside an interpolated format item; call `ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)` first because the containing C# string still treats those backslashes as escape sequences.
- Never leave shared AWS and plugin S3 model names unqualified in tests or make `IProviderContext.Secrets` nullable; qualify colliding SDK types and match the interface nullability exactly because warnings are errors and the test project reveals these issues only after its broad dependency build.
- Never combine a fixed probe clock with a ViewModel that filters short monitoring ranges against wall-clock time; align the sample timestamp with the active clock or inject the clock because an otherwise valid sample will correctly fall outside the five-minute window and produce a misleading aggregate-test failure.
- Never run a `--no-restore` solution build after pulling central package-version changes; always restore the solution first because stale project assets can mix incompatible managed assembly versions and produce misleading compiler failures.
- Never remove a project reference based only on `using`-directive searches; search fully qualified namespace expressions and build the affected project directly because expression-qualified calls can hide a real dependency without importing its namespace.
- Never use a product executable project as a bounded compile check unless recursive staging is explicitly disabled; route agent checks through `build/verify.ps1` so plugin builds, daemon staging, and VideoEditor frontend work happen only in product-assembly lanes.

### Keep Subdaily Jobs Portable Across Hosting Plans

**Context**: XIP0085 needs one-minute ledger dispatch and five-minute account-deletion processing, but Vercel Hobby rejects Cron Jobs that run more than once per day.

**Lesson**: Treat subdaily scheduling as an independently deployable adapter. Keep the business logic behind idempotent, secret-authenticated internal routes, and let a versioned Cloudflare Cron Worker invoke the exact allowlisted paths when the Vercel plan cannot provide the required cadence. Store the shared secret independently in both providers, generate Worker binding types from `wrangler.jsonc`, configure staging and production as explicit Wrangler environments, and keep the Worker out of the application request-delivery path.

### Confirm the Canonical Hostname Before Provisioning Exact-Origin Integrations

**Context**: XIP0085 was first provisioned at the temporary `staging.xerahs.com` hostname before `cloud.xerahs.com` was confirmed as the stable application origin.

**Lesson**: Never infer a stable public hostname from an environment label. Confirm and record the canonical application origin before provisioning DNS, TLS, authentication callbacks, OAuth clients, billing webhooks, schedulers, and desktop defaults because exact-origin integrations make a later migration coordinated and compatibility-sensitive. Preserve a narrowly scoped legacy callback window for already-shipped native clients instead of either silently breaking them or accepting wildcard redirects.

### Portable Release Contracts

- Never assume a ZIP makes XerahS portable; include `portable.txt` beside `XerahS.exe` and verify that the marker routes default settings to the adjacent `XerahS` folder, because classic ShareX's extensionless `Portable` marker is a different contract.
- Never add the portable marker to the shared installer publish directory; add it only while writing the ZIP, because EXE/MSI packaging consumes the same payload.
- Never let the updater's generic `portable.zip` fallback select a different architecture's archive; prefer the exact `-win-<arch>-portable.zip` suffix and keep CI upload lists, archive validation, and post-release asset checks synchronized.
