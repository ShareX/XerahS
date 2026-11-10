# Linux Portal Behavior

XerahS uses XDG Desktop Portals where Linux desktops expect permissioned desktop integration. Portal dialogs and selectors are owned by the user's desktop portal backend, not by XerahS, so their UI can differ across GNOME, KDE Plasma, GTK, wlroots, and compositor-specific backends.

## GNOME Wayland

- Screenshot and screencast prompts are provided by `xdg-desktop-portal-gnome`.
- Global shortcut support depends on the portal backend version and GNOME settings integration.
- The tray icon needs AppIndicator/KStatusNotifier support, usually through a GNOME Shell extension.

## KDE Plasma Wayland

- Screenshot and screencast prompts are provided by `xdg-desktop-portal-kde`.
- On first use the KDE portal shows "Screenshot Requested / Allow <app> to take a screenshot?" with **Allow disabled**. This is by design in Plasma 6.4+: Allow only enables after you click **Take Screenshot** and a preview exists. Clicking Allow then stores the permission for the app. XerahS avoids this prompt for full-screen and window captures by trying KWin's `org.kde.KWin.ScreenShot2` first; that needs the installed desktop entry to carry `X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2` (the .deb, .rpm, AUR and AppImage packages and the autostart entry do). Running a bare tarball binary without a desktop entry falls back to the portal prompt.
- Region capture: the portal dialog has no rectangular-region option, so Automatic on non-sandboxed KDE Wayland uses the XerahS overlay for selection and grabs the screen through KWin ScreenShot2 (portal only as a fallback). Choosing **XDG portal dialog** explicitly still calls the portal; Flatpak always uses the portal.
- Remembering Allow: before the Screenshot call XerahS registers the app id `xerahs` through `org.freedesktop.host.portal.Registry.Register` (xdg-desktop-portal 1.19+, unsandboxed only, once per D-Bus connection). Without a stable app id (a systemd unit such as `app-XerahS@autostart.service`, or a launch from a terminal or hotkey) the KDE backend cannot store the permission and asks every time. Registration failure is logged and ignored. ScreenCast and GlobalShortcuts connections are not registered yet, because changing their app id could orphan stored restore tokens and shortcut bindings.
- KDE may not expose every optional GlobalShortcuts method. Missing `ConfigureShortcuts` must be treated as a graceful fallback, not a fatal error.
- Current KDE portal stacks should be used for validation. Prefer `xdg-desktop-portal-kde >= 6.4.2` for the known KDE/Nobara stress cases.

## wlroots, Sway, Hyprland

- Use the compositor-specific portal backend where available, such as `xdg-desktop-portal-wlr` or `xdg-desktop-portal-hyprland`.
- Backend coverage varies more than GNOME/KDE. Screenshot and screencast support should be verified on the exact compositor and portal backend combination.
- Native CLI capture fallbacks are only used outside Flatpak/sandboxed environments.

## Expected Differences

- Portal selectors can look different from XerahS UI.
- Backend routing follows session configuration such as `XDG_CURRENT_DESKTOP` and portal configuration files.
- User cancellation is expected and should appear as cancellation, not a crash.

## Troubleshooting Commands

```bash
echo "$XDG_SESSION_TYPE"
echo "$XDG_CURRENT_DESKTOP"
busctl --user list | grep -E 'portal|xdg-desktop'
busctl --user introspect org.freedesktop.portal.Desktop /org/freedesktop/portal/desktop
systemctl --user status xdg-desktop-portal
systemctl --user status xdg-desktop-portal-gnome
systemctl --user status xdg-desktop-portal-kde
systemctl --user status xdg-desktop-portal-wlr
systemctl --user status xdg-desktop-portal-hyprland
```

Use XerahS Linux recording diagnostics for a structured report. It records the runtime environment, portal interface probes, PipeWire state, command availability, and the selected recording backend.

