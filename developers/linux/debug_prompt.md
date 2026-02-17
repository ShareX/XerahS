You are an expert .NET/Avalonia developer with deep knowledge of Linux (X11/Wayland) window management.
Your task is to add comprehensive debug logging to the XerahS codebase to diagnose specific issues reported by a Linux tester on Arch (Wayland + XWayland).

## The Issues to Diagnose
1. **Hotkeys not working**: Verify if the application detects Wayland correctly and which HotkeyService is initialized. Check for DBus/X11 registration errors.
2. **Active Window Capture fails**: The user reports the window minimizes, then re-opens with no image. This suggests `CaptureActiveWindowAsync` fails to get the window handle or bounds, or the capture returns null.
3. **Region Capture input issues**: Use reports generic cursor outside "active window" and crosshair inside, with drag failing. This implies overlay window positioning, sizing, or input handling issues on Wayland.

## Specific Changes Required

### 1. `src/XerahS.Platform.Linux/LinuxPlatform.cs`
- In `Initialize()`, explicit log the value of `LinuxScreenCaptureService.IsWayland` and which services (Hotkey, Input, System) are being instantiated.
- Example: `DebugHelper.WriteLine($"LinuxPlatform: IsWayland={LinuxScreenCaptureService.IsWayland}. Initializing {hotkeyService.GetType().Name}...");`

### 2. `src/XerahS.Platform.Linux/Services/WaylandPortalHotkeyService.cs`
- Add aggressive logging to the constructor to confirm successful DBus connection.
- Log the result of `RegisterHotkey` and `UnregisterHotkey` with detailed error messages from the DBus calls if they fail.
- Catch and log specific exceptions in `BindShortcutsAsync` and `CreateSessionAsync`.

### 3. `src/XerahS.RegionCapture/Services/OverlayManager.cs`
- In `ShowOverlaysAsync`:
    - Log the number of monitors detected (`monitors.Count`).
    - Inside the loop creating `OverlayWindow`, log the calculated bounds for each overlay (`monitor.PhysicalBounds`).
    - Log whether `primaryOverlay?.Focus()` was called and on which overlay.
- Add a log in `CloseAllOverlays` to track when/why overlays are closed.

### 4. `src/XerahS.RegionCapture/UI/RegionCaptureWindow.axaml.cs` (if it exists) or `OverlayWindow` class
- Find where `OverlayWindow` is defined (likely inheriting from `Window`).
- Add event listeners for `PointerMoved`, `PointerPressed`, `PointerReleased`, `KeyDown` in the constructor or `OnOpened`.
- Log these events to trace if the overlay is receiving input.
    - `DebugHelper.WriteLine($"OverlayWindow: PointerMoved at {e.GetPosition(this)}");`
    - `DebugHelper.WriteLine($"OverlayWindow: PointerPressed. IsLeft={e.GetCurrentPoint(this).Properties.IsLeftButtonPressed}");`
- This will confirm if the overlay covers the whole screen or just a part of it (the "active window" issue).

### 5. `src/XerahS.Platform.Linux/LinuxWindowService.cs`
- (Note: Some logs already exist, but ensure they are sufficient).
- In `GetWindowBounds`, if `XGetWindowAttributes` fails or returns weird values (like 0x0 or negative coords), log a high-visibility WARNING.
- In `GetForegroundWindow`, log if it returns `IntPtr.Zero`.

## Output
Please apply these changes to the codebase. Ensure all logs use `XerahS.Common.DebugHelper.WriteLine` so they appear in standard debug output.
