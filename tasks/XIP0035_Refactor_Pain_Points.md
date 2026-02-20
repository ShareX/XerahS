# XIP0035: Top 3 Refactoring Pain Points (src)

## Summary

Document the top three structural pain points identified in `src` that require refactoring. XerahS intentionally maintains two mobile tech stacks (Avalonia and MAUI) to evaluate which is best; this XIP addresses pain point #3 (duplicate mobile config ViewModels) with a clear separation: **XerahS.Mobile.Core** for shared code, **XerahS.Mobile.Ava.*** for Avalonia-specific code (namespace `Ava`), and **XerahS.Mobile.Maui** for MAUI-specific code.

---

## Pain Point 1: God class – `LinuxScreenCaptureService` (~2,146 lines) — **Refactored (XIP0035)**

**What:** A single class implements the entire Linux capture stack (Portal, KDE, GNOME, X11, CLI tools, slurp, grim, etc.) with provider list hardcoded in the constructor and 50+ methods in one file.

**Where:** `src/XerahS.Platform.Linux/LinuxScreenCaptureService.cs`

**Why it hurts:** Every new capture path or bugfix touches the same type; unit testing individual strategies is difficult; reuse and platform abstraction are limited.

**Refactor direction (implemented):** Extracted each capture strategy into its own type. `LinuxScreenCaptureService` is now a thin coordinator (~365 lines). Shared helpers and strategies live in:
- `Capture/Helpers/LinuxCliToolRunner.cs` – run screenshot CLI tools (testable).
- `Capture/X11/X11ScreenCapture.cs` – X11 XGetImage fullscreen capture.
- `Capture/Portal/PortalScreenCapture.cs` – XDG Portal screenshot (D-Bus).
- `Capture/Kde/KdeDbusScreenCapture.cs` – KDE ScreenShot2 D-Bus.
- `Capture/Gnome/GnomeDbusScreenCapture.cs` – GNOME Shell Screenshot D-Bus.
- `Capture/Wayland/WaylandCliCapture.cs` – grim, slurp, grimblast, hyprshot.
- `Capture/Cli/CliCaptureExecutor.cs` – gnome-screenshot, spectacle, scrot, import, xfce4-screenshooter.
Provider list remains in the coordinator constructor; strategies are invoked via existing `ILinuxCaptureRuntime` and providers.

---

## Pain Point 2: Static service locator and no dependency injection

**What:** Platform and app services are exposed via static `PlatformServices` and wired by manually constructing concrete types. No DI container or constructor injection.

**Where:**  

- `src/XerahS.Platform.Abstractions/PlatformServices.cs`  
- `src/XerahS.App/Program.cs` (e.g. `InitializePlatformServices()`, `InitializeBackgroundServicesAsync()`)

**Why it hurts:** Hard to test, implicit initialization order, hidden dependencies, every new service requires changes in both `Program.cs` and `PlatformServices`.

**Refactor direction:** Introduce a DI container (e.g. `Microsoft.Extensions.DependencyInjection`) and a single composition root. Register `IScreenCaptureService`, `IClipboardService`, etc. with platform-specific implementations; inject into ViewModels and UI. Keep `PlatformServices` only as a thin bridge during migration if needed.

---

## Pain Point 3: Duplicate mobile config ViewModels (Mobile.UI vs Mobile.Maui) — **In scope for this XIP**

**What:** Large config ViewModels and supporting types are duplicated between the Avalonia-based mobile UI and the MAUI-based mobile UI: `MobileCustomUploaderConfigViewModel` (~~982 lines), `MobileAmazonS3ConfigViewModel` (~~494 lines), plus `CustomUploaderListItem`, `KeyValuePairItem`, `IMobileUploaderConfig`, `MobileUploaderConfigAttribute`. Only namespace and minor differences (e.g. CommunityToolkit.Mvvm in Maui) differ.

**Where:**  

- `src/XerahS.Mobile.UI/ViewModels/` (Avalonia)  
- `src/XerahS.Mobile.Maui/ViewModels/` (MAUI)

**Why it hurts:** Bug fixes and features must be done in two places; codebases can diverge; ~1,500+ lines duplicated.

**Refactor direction (implemented in this XIP):**

- **XerahS.Mobile.Core**: Shared ViewModels, DTOs (`CustomUploaderListItem`, `KeyValuePairItem`), interfaces (`IMobileUploaderConfig`), attributes (`MobileUploaderConfigAttribute`), and a shared `RelayCommand` for commands. No "Ava" or "Maui" in namespace; common code lives here.
- **XerahS.Mobile.Ava.***: Avalonia-specific code only; namespace **Ava** (or `XerahS.Mobile.Ava` for project). Views, themes, app bootstrap, and any Avalonia-only ViewModels. References Core.
- **XerahS.Mobile.Maui**: MAUI-specific code; references Core and uses shared ViewModels from Core. Removes duplicate ViewModels and types.

---

## Out of scope (later)

- `TaskSettingsViewModel.cs` (~~1,403 lines), `MainWindow.axaml.cs` (~~904 lines), `OverlayWindow.axaml.cs` / `RegionCaptureControl.cs` (~1,265 / ~1,063 lines).

