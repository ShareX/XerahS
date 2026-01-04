# macOS Hotkey Validation (SharpHook)

Steps to validate global hotkeys on macOS hardware:

1. Grant Accessibility: `System Settings` → `Privacy & Security` → `Accessibility` → allow the ShareX Ava app (built binary).
2. Launch ShareX Ava and configure a global hotkey (e.g., Meta+Shift+4) in settings.
3. Trigger the hotkey and confirm `HotkeyTriggered` fires (capture workflow or logs).
4. Inspect logs for `MacOSHotkeyService` entries; failures should surface if SharpHook cannot start.
5. Suspend/resume hotkeys in-app (if available) and ensure triggers stop/resume.
6. Test multiple hotkeys for conflicts; ensure only the intended binding fires.
