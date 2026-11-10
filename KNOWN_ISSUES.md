# Known Issues

## Windows

### Region Capture
- **DPI Scaling Issue on Region Capture Background:** The dark background behind the region capture tool is not rendering correctly when any monitor connected to the system is set to a DPI scale greater than 100%. The background overlay appears shifted or misaligned in these high-DPI scenarios.

## Linux

> Prioritized fixes and the current Linux state assessment are tracked in
> [docs/proposals/xip/XIP0079-linux-improvement-plan.md](docs/proposals/xip/XIP0079-linux-improvement-plan.md).

### Region Capture / Screenshot
- **XDG Portal vs in-app overlay:** Region capture defaults to the in-app overlay with crosshair. The overlay can be sluggish on Wayland (pointer-event delay) or misaligned on mixed-DPI setups. **Option:** enable **Use modern capture** for the XDG Portal / system dialog path, or pick a Linux region selector in application settings.
- **Mixed-DPI vertical stacks (XIP0079 P4, v0.23.129):** Vertically stacked monitors with different scale factors are normalized with cumulative physical layout. Set `XERAHS_LEGACY_MONITOR_NORMALIZER=1` to revert to the pre-v0.23.129 formula if a regression appears.
- **Fedora GNOME mixed-DPI routing:** On Fedora GNOME mixed-DPI setups, region selection/capture may misalign unless `UseTransparentOverlay` is enabled. Runtime forces transparent overlay on this platform combination.

### Global Hotkeys
- **Delivery state is now surfaced (XIP0079 P1, v0.23.129):** Open **Settings → Hotkeys** to see whether shortcuts are portal-bound, focus-only (X11 fallback), or unavailable. When the GlobalShortcuts portal is missing or bind fails, hotkeys only fire while XerahS is focused — the banner explains this instead of failing silently.
- **Portal bind still requires a matching `.desktop` entry (XIP0044):** Packaged `.deb`/`.rpm` installs satisfy this; `dotnet run` debug builds on Wayland need a local `~/.local/share/applications/xerahs.desktop` workaround (documented in [developers/linux/INSTALL.md](developers/linux/INSTALL.md)).
- **End-to-end verification matrix:** GNOME/KDE/wlroots manual verification is still pending on issue trackers; see XIP0044 and XIP0079 §3.1.

### Clipboard
- **Background CLI clipboard (XIP0079 P3, v0.23.129):** Non-UI paths (`wl-copy`/`xclip`) log a warning and show a settings hint when tools are missing. `.deb`/`.rpm` packages recommend `wl-clipboard` and `xclip`. UI copies can persist after exit on Wayland via **Persist clipboard after exit** (uses `wl-copy` owner process).

### Notifications
- **Action buttons (XIP0079 P2, v0.23.129):** After-upload toasts support portal `buttons` + `ActionInvoked` and `notify-send --action` fallback. Sandboxed Flatpak builds use the portal path only.

## macOS

> Prioritized fixes and the current macOS state assessment are tracked in
> [docs/proposals/xip/XIP0078-macos-improvement-plan.md](docs/proposals/xip/XIP0078-macos-improvement-plan.md).

### Distribution
- **"XerahS is damaged and can't be opened" on downloaded builds:** release archives are unsigned and not notarized, so Gatekeeper rejects quarantined downloads until the user runs `xattr -cr` (workaround documented in README). Unsigned builds also have an unstable TCC identity, so Screen Recording / Accessibility grants can reset after updates. Tracked as P1/P2 in the improvement plan.

### Permissions
- **Missing Screen Recording permission yields wallpaper-only screenshots instead of a prompt:** there is no `CGPreflightScreenCaptureAccess` preflight; the native capture path silently falls back to the `screencapture` CLI, which renders only the desktop wallpaper when permission is missing. Tracked as P3.
- **Global hotkeys require Accessibility permission:** hotkeys are powered by a SharpHook event tap, which needs Accessibility (see FAQ). A Carbon `RegisterEventHotKey` path that needs no permission is tracked as P4.

### Window capture
- **Window list shows only the frontmost window:** window enumeration uses AppleScript and returns a single window; background-window capture falls back to the interactive CLI. Tracked as P5.
