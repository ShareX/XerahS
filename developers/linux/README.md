# Linux Developers README

This document is the Linux screenshot implementation guide for `XerahS`, aligned to the project waterfall model and current code.

## Scope

- Repository area: `src/XerahS.Platform.Linux`
- Primary runtime: `src/XerahS.Platform.Linux/LinuxScreenCaptureService.cs`
- Goal: consistent capture behavior across Wayland, X11, desktop DBus APIs, and sandboxed app constraints.

## Mandatory Waterfall Order

All Linux screenshot entry points use one strict order:

1. `Portal` (XDG Desktop Portal)
2. `DesktopDbus` (KDE or GNOME DBus)
3. `WaylandProtocol` (wlroots/Wayland tool or protocol path)
4. `X11` (X11/Xlib and legacy CLI fallback)

Sandboxed sessions are strict portal-only. In Flatpak/Snap/AppImage/container contexts, non-portal stages are not attempted.

This order is centralized in:

- `src/XerahS.Platform.Linux/Capture/Orchestration/WaterfallCapturePolicy.cs`

Coordinator execution is centralized in:

- `src/XerahS.Platform.Linux/Capture/Orchestration/LinuxCaptureCoordinator.cs`

The same policy is used by:

- `CaptureRegionAsync`
- `CaptureFullScreenAsync`
- `CaptureActiveWindowAsync`

All three are in:

- `src/XerahS.Platform.Linux/LinuxScreenCaptureService.cs`

## Architecture (Parallel Developer Lanes)

Linux capture is now split for parallel development:

- Contracts: `src/XerahS.Platform.Linux/Capture/Contracts`
- Runtime detection: `src/XerahS.Platform.Linux/Capture/Detection`
- Orchestration/policy: `src/XerahS.Platform.Linux/Capture/Orchestration`
- Stage providers: `src/XerahS.Platform.Linux/Capture/Providers`

Provider mapping:

- Stage 1 `Portal`: `src/XerahS.Platform.Linux/Capture/Providers/Portal/PortalCaptureProvider.cs`
- Stage 2 `DesktopDbus`: `src/XerahS.Platform.Linux/Capture/Providers/KDE/KdeDbusCaptureProvider.cs`
- Stage 2 `DesktopDbus`: `src/XerahS.Platform.Linux/Capture/Providers/GNOME/GnomeDbusCaptureProvider.cs`
- Stage 3 `WaylandProtocol`: `src/XerahS.Platform.Linux/Capture/Providers/Wlroots/WlrootsCaptureProvider.cs`
- Stage 4 `X11`: `src/XerahS.Platform.Linux/Capture/Providers/X11/X11CaptureProvider.cs`
- Stage 4 `X11`: `src/XerahS.Platform.Linux/Capture/Providers/Cli/CliCaptureProvider.cs`

Runtime context detector:

- `src/XerahS.Platform.Linux/Capture/Detection/LinuxRuntimeContextDetector.cs`
- `src/XerahS.Platform.Linux/Capture/Detection/DesktopEnvironmentDetector.cs`
- `src/XerahS.Platform.Linux/Capture/Detection/CompositorDetector.cs`
- `src/XerahS.Platform.Linux/Capture/Detection/SandboxDetector.cs`

Contract hardening:

- `src/XerahS.Platform.Linux/Capture/Contracts/ILinuxCaptureContext.cs`
- `src/XerahS.Platform.Linux/Capture/Contracts/LinuxCaptureContext.cs`

## Stage Details

### Stage 1: XDG Portal (Modern Standard)

Portal contract used:

- Service: `org.freedesktop.portal.Desktop`
- Path: `/org/freedesktop/portal/desktop`
- Interface: `org.freedesktop.portal.Screenshot`
- Method: `Screenshot(...)`

Runtime hook:

- `ILinuxCaptureRuntime.TryPortalCaptureAsync(...)`

Portal is attempted when any are true:

- Wayland session
- sandboxed session (`FLATPAK_ID`, `SNAP`, `APPIMAGE`, container)
- screenshot portal interface is present

### Stage 2: Desktop DBus Fallback

KDE fallback:

- Service/interface: `org.kde.KWin.ScreenShot2`
- Path: `/org/kde/KWin/ScreenShot2`
- Methods used:
- `CaptureWorkspace(...)` for fullscreen
- `CaptureActiveWindow(...)` for active window
- `CaptureInteractive(...)` for region-like interactive selection

GNOME fallback:

- Service/interface: `org.gnome.Shell.Screenshot`
- Path: `/org/gnome/Shell/Screenshot`
- Methods used:
- `Screenshot(...)` for fullscreen
- `ScreenshotWindow(...)` for active window
- `SelectArea(...)` + `ScreenshotArea(...)` for region

Runtime hook:

- `ILinuxCaptureRuntime.TryKdeDbusCaptureAsync(...)`
- `ILinuxCaptureRuntime.TryGnomeDbusCaptureAsync(...)`

### Stage 3: Wayland Protocol / wlroots Fallback

Current implementation is tool-driven (not in-process Wayland protocol client):

- wlroots/Sway/Hyprland paths use `grim`, `slurp`, `grimblast`, `hyprshot`
- Fullscreen: `grim`
- Region: `grim+slurp` or Hyprland wrappers
- Active window: Hyprland tool path, or wlroots interactive region fallback

Runtime hook:

- `ILinuxCaptureRuntime.TryWlrootsCaptureAsync(...)`

### Stage 4: X11 Legacy Fallback

Primary X11 implementation:

- `XGetImage` capture path in `LinuxScreenCaptureService`

Supporting files:

- `src/XerahS.Platform.Linux/Capture/X11GetImageStrategy.cs`
- `src/XerahS.Platform.Linux/LinuxWindowService.cs`
- `src/XerahS.Platform.Linux/NativeMethods.cs`

Legacy CLI fallbacks are still used where needed:

- `gnome-screenshot`
- `spectacle`
- `scrot`
- `import`

Runtime hook:

- `ILinuxCaptureRuntime.TryX11NativeCaptureAsync(...)`
- `ILinuxCaptureRuntime.TryCliCaptureAsync(...)`

## Decision Trace

Capture orchestration records each provider decision and final outcome.

- Trace model: `src/XerahS.Platform.Linux/Capture/Orchestration/CaptureDecisionTrace.cs`
- Execution wrapper: `src/XerahS.Platform.Linux/Capture/Orchestration/LinuxCaptureExecutionResult.cs`
- Coordinator trace path: `src/XerahS.Platform.Linux/Capture/Orchestration/LinuxCaptureCoordinator.cs`
- Service logging integration: `src/XerahS.Platform.Linux/LinuxScreenCaptureService.cs`

## Arch Linux Developer Focus Paths

If you are contributing from Arch Linux, pick one lane and own that lane end-to-end.

1. Portal lane (Wayland + sandbox correctness)
- `src/XerahS.Platform.Linux/Capture/Providers/Portal/PortalCaptureProvider.cs`
- `src/XerahS.Platform.Linux/Capture/IScreenshotPortal.cs`
- `src/XerahS.Platform.Linux/Capture/IPortalRequest.cs`
- `src/XerahS.Platform.Linux/Capture/PortalRequestExtensions.cs`
- `src/XerahS.Platform.Linux/Capture/PortalScreenshotFallback.cs`
- `src/XerahS.Platform.Linux/Services/PortalInterfaceChecker.cs`

2. KDE Plasma lane
- `src/XerahS.Platform.Linux/Capture/Providers/KDE/KdeDbusCaptureProvider.cs`
- `src/XerahS.Platform.Linux/LinuxScreenCaptureService.cs` (KWin ScreenShot2 calls and decode path)
- `src/XerahS.Platform.Linux/Services/LinuxStartupService.cs`
- `build/linux/XerahS.Packaging/Program.cs`

3. GNOME lane
- `src/XerahS.Platform.Linux/Capture/Providers/GNOME/GnomeDbusCaptureProvider.cs`
- `src/XerahS.Platform.Linux/LinuxScreenCaptureService.cs` (GNOME Shell DBus methods)

4. wlroots lane (Sway/Hyprland/Wayfire style environments)
- `src/XerahS.Platform.Linux/Capture/Providers/Wlroots/WlrootsCaptureProvider.cs`
- `src/XerahS.Platform.Linux/LinuxScreenCaptureService.cs` (grim/slurp/grimblast/hyprshot)

5. X11 lane
- `src/XerahS.Platform.Linux/Capture/Providers/X11/X11CaptureProvider.cs`
- `src/XerahS.Platform.Linux/Capture/Providers/Cli/CliCaptureProvider.cs`
- `src/XerahS.Platform.Linux/Capture/X11GetImageStrategy.cs`
- `src/XerahS.Platform.Linux/LinuxWindowService.cs`
- `src/XerahS.Platform.Linux/NativeMethods.cs`

6. Orchestration lane (cross-lane stability)
- `src/XerahS.Platform.Linux/Capture/Contracts`
- `src/XerahS.Platform.Linux/Capture/Detection`
- `src/XerahS.Platform.Linux/Capture/Orchestration`

7. Linux orchestration tests lane
- `tests/XerahS.Tests/Platform/Linux/LinuxCaptureOrchestrationTests.cs`
- `tests/XerahS.Tests/XerahS.Tests.csproj`

## KDE Desktop Entry Permission Requirement

KDE ScreenShot2 calls can fail without restricted DBus interface permission in desktop entries.

Required key:

- `X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2`

Current writers:

- `src/XerahS.Platform.Linux/Services/LinuxStartupService.cs`
- `build/linux/XerahS.Packaging/Program.cs`

## Compliance Against Linux Screenshot Implementation Guide

1. XDG Portal first: implemented.
2. Desktop-specific DBus fallback (KDE and GNOME): implemented.
3. Wayland compositor fallback:
- Implemented via CLI tools (`grim`, `slurp`, `grimblast`, `hyprshot`).
- Native in-process `wlr-screencopy-v1` client: not implemented yet.
4. X11 fallback: implemented with `XGetImage` plus CLI fallbacks.

## Remaining Opportunities

1. Implement native `wlr-screencopy-v1` client to remove tool dependency for wlroots compositors.
2. Add optional `XShm` path for faster X11 capture on high-resolution or multi-monitor setups.
3. Add automated Linux integration tests that assert provider order for region, fullscreen, and active-window capture.
4. Add CI matrix jobs for GNOME Wayland, KDE Wayland, Hyprland/Sway, and X11 sessions.

## Implementation Phases (1-10)

1. Phase 1: Waterfall Unification (`Completed`)
- Unified `CaptureRegionAsync`, `CaptureFullScreenAsync`, and `CaptureActiveWindowAsync` to run through one coordinator and one waterfall policy.

2. Phase 2: KDE DBus Fallback + Permissions (`Completed`)
- Added KWin ScreenShot2 fallback paths and ensured KDE desktop entry restricted interface key is written.

3. Phase 3: Modular Core Skeleton (`Completed`)
- Introduced modular Linux capture structure with `Contracts`, `Detection`, `Orchestration`, and `Providers`.

4. Phase 4: Strict Sandbox Rule (`Completed`)
- Enforced portal-only behavior for sandboxed sessions (Flatpak/Snap/AppImage/container); non-portal stages are skipped.

5. Phase 5: Provider Split to Parallel Lanes (`Completed`)
- Split provider implementation into lane folders:
- `Providers/Portal`
- `Providers/KDE`
- `Providers/GNOME`
- `Providers/Wlroots`
- `Providers/X11`
- `Providers/Cli`

6. Phase 6: Detection Split (`Completed`)
- Split runtime detection into:
- `DesktopEnvironmentDetector`
- `CompositorDetector`
- `SandboxDetector`
- Composed by `LinuxRuntimeContextDetector`.

7. Phase 7: Capture Decision Trace (`Completed`)
- Added `CaptureDecisionTrace` so each capture attempt records stage/provider decisions and final outcome.

8. Phase 8: Interface Hardening (`Completed`)
- Introduced `ILinuxCaptureContext` and updated provider/policy/coordinator contracts to consume the interface.

9. Phase 9: Test Matrix (`Completed`)
- Added automated tests for waterfall order, sandbox constraints, provider lane behavior, and orchestration trace sequencing.

10. Phase 10: Final Docs + Release Gate (`Completed`)
- Linux developer documentation is being maintained in this file.
- Final gate includes `dotnet build` success and Linux orchestration test execution.

## Build and Verification Commands

Linux platform project:

```powershell
dotnet build src/XerahS.Platform.Linux/XerahS.Platform.Linux.csproj -c Debug -v minimal
```

Linux packaging project:

```powershell
dotnet build build/linux/XerahS.Packaging/XerahS.Packaging.csproj -c Debug -v minimal
```

Solution build:

```powershell
dotnet build -c Debug -v minimal
```

Targeted Linux orchestration tests:

```powershell
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter "FullyQualifiedName~LinuxCaptureOrchestrationTests" -v minimal
```

## Contributor Checklist

Before opening a PR for Linux capture:

1. Keep stage order unchanged (`Portal -> DesktopDbus -> WaylandProtocol -> X11`).
2. Keep `CaptureRegionAsync`, `CaptureFullScreenAsync`, and `CaptureActiveWindowAsync` on the same coordinator/policy path.
3. Add/update provider-level logging for new fallback behavior.
4. Test at least one Wayland compositor path and one X11 path.
5. If touching KDE DBus capture, validate desktop entry permission key remains present.
