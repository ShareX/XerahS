# XIP0080: Linux Global Hotkeys via Direct evdev Input Device Listening

**Status:** In Progress (branch: `linux-hotkey-rewrite`)  
**Authors:** McoreD, Aoife Brennan  
**Date:** 2026-06-14

## Summary

Replace the XDG Global Shortcuts Portal implementation for Linux global hotkeys with a direct evdev-based input device listener, modeled on the working approach in CrossMacro.

## Motivation

The current portal-based hotkey system is unreliable on Wayland (particularly GNOME). Known issues include:

- App ID mismatch causing instant rejection
- Race condition (binding attempted before window handle exists)
- Wrapper script vs symlink verification failures
- Silent retry crashes
- Incorrect Avalonia key mapping for PrintScreen

These make core capture hotkeys non-functional for many Linux users.

## Proposed Solution

Implement direct input device listening using evdev, following the `LinuxInputCapture` / `EvdevReader` pattern from CrossMacro.

### How Hotkey Ownership Works

Unlike the portal model (where you register specific shortcuts with the compositor), the evdev approach works as follows:

- XerahS opens raw keyboard input devices and receives **every** key event on the system.
- It maintains its own internal list of configured hotkeys.
- A hotkey matching engine compares incoming key combinations against this list.
- Only when a match is found does XerahS trigger an action.
- Non-matching combinations are ignored.

This means XerahS is responsible for deciding "this hotkey belongs to me" rather than relying on the compositor.

## Implementation Scope

This is a **medium-to-large** change, not a small refactor.

| Area | Effort | Notes |
|------|--------|-------|
| New low-level input layer | High | Porting `EvdevReader`, device enumeration, native interop |
| Hotkey matching engine | Medium | State tracking + combination matching |
| Platform abstraction | Medium | `ILinuxGlobalHotkeyProvider` + integration |
| Permission & diagnostics | Medium-High | `doctor` command, polkit, udev rules |
| Packaging | Medium | All Linux distribution formats |
| Testing & edge cases | High | Multiple compositors, permission scenarios |

**Estimated effort**: Several weeks of focused development.

Because of the scope, native interop, permission model, and risk to core capture functionality, this work should **not** be done directly on `develop`.

## Branching Strategy

A dedicated long-lived feature branch is strongly recommended:

```bash
git checkout -b linux-hotkey-rewrite
```

This allows the work to progress iteratively with proper testing before merging back to `develop`.

## Success Criteria

The implementation is considered successful when:

- Global hotkeys (PrintScreen, region capture, full screen, etc.) work reliably on GNOME, KDE Plasma, and Hyprland under Wayland
- No dependency on XDG Global Shortcuts Portal for hotkey registration
- `xerahs doctor --linux-input` correctly reports permission status and gives actionable guidance
- Hotkeys continue to work after logout/login and across reboots
- Packaging (deb, rpm, AppImage, Flatpak) includes necessary udev rules and polkit policies
- Existing portal code path remains functional as a legacy fallback during transition
- No regression in hotkey behavior on X11

## Benefits

- Removes all current portal failure modes
- Reliable hotkeys on all major Wayland compositors
- No dependency on window handles or exact `.desktop` naming
- Proven pattern already running in production Avalonia software
- Foundation for future low-level input features

## Risks

- Permission model complexity → mitigated by diagnostics + automated setup
- Sandboxed distribution support → handled via daemon socket + polkit paths
- Potential to swallow hotkeys intended for other applications → mitigated by conservative hotkey scope

## Implementation

Initial end-to-end implementation lives on the `linux-hotkey-rewrite` branch:

- Native interop and device handling: `src/platform/XerahS.Platform.Linux/Input/Evdev/`
  (`EvdevNative`, `InputEventCodes`, `EvdevReader`, `InputDeviceEnumerator`, `InputDeviceInfo`).
- Matching engine: `src/platform/XerahS.Platform.Linux/Input/`
  (`EvdevKeyMap`, `ModifierStateTracker`, `EvdevHotkeyMatcher`).
- Hotkey provider: `src/platform/XerahS.Platform.Linux/Services/EvdevGlobalHotkeyService.cs`,
  implementing `IHotkeyService`.
- Backend selection with portal/X11 fallback and `XERAHS_LINUX_HOTKEY_BACKEND`
  override: `LinuxPlatform.CreateHotkeyService`.
- Diagnostics: `LinuxInputDiagnostics` powering `xerahs doctor --linux-input`.
- Packaging: `build/linux/packaging/99-xerahs-input.rules`,
  `build/linux/packaging/com.xerahs.input.policy`, integrated into the deb/rpm
  packaging tool. Setup guide: `docs/linux/global-hotkeys-evdev.md`.
- Tests: `tests/XerahS.Tests/Platform/Linux/EvdevHotkeyTests.cs`.

- Quick Setup: `src/platform/XerahS.Platform.Linux/Services/QuickSetup/`
  (`LinuxInputQuickSetupService` implements `IHotkeyAccessSetupService`; the platform
  hotkey service is a `SwitchableHotkeyService` so a successful grant moves hotkeys to
  evdev without a restart). Surfaced in Settings > Advanced > Global Hotkeys.

Sandboxed (Flatpak) sessions intentionally retain the portal path because raw
`/dev/input` access is not granted there.

## References

- CrossMacro (cloned at `Projects/KovaForge/CrossMacro`)
  - `src/CrossMacro.Platform.Linux/LinuxInputCapture.cs`
  - `src/CrossMacro.Infrastructure/Linux/Native/Evdev/EvdevReader.cs`
  - `docs/linux.md`
