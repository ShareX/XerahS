# CX07: MacOS Implementation

## Priority
**HIGH** - Major cross-platform milestone.

## Assignee
**Codex**

## Branch
`feature/macos-implementation`

## Objective
Implement full support for macOS in ShareX Avalonia. This includes creating a dedicated platform project, implementing native services (Screen Capture, Hotkeys, Clipboard), and ensuring the application packages and runs correctly on macOS environments.

## Background
ShareX Avalonia is designed to be cross-platform. Currently, it has a `ShareX.Avalonia.Platform.Windows` project for Windows-specific implementations. To support macOS, we need a parallel `ShareX.Avalonia.Platform.MacOS` project that implements the abstract services defined in `ShareX.Avalonia.Platform.Abstractions`.

## Scope

### Phase 1: Project Structure
1. **Create Project**: `src/ShareX.Avalonia.Platform.MacOS` (Class Library).
2. **References**:
   - Add reference to `ShareX.Avalonia.Platform.Abstractions`.
   - Add reference to `ShareX.Avalonia.Common`.
   - Add `Avalonia` packages (if native UI integration is needed).

### Phase 2: Platform Services Implementation
Implement the following interfaces located in `Abstractions`:

#### 1. IScreenshotService
**File**: `src/ShareX.Avalonia.Platform.MacOS/MacOSScreenshotService.cs`
- **Mechanism**:
  - **Option A (Simpler)**: Wrapper around the native `screencapture` CLI tool (pre-installed on macOS).
    - `screencapture -i [file]` for interactive.
    - `screencapture -c` for clipboard.
    - `screencapture -x [file]` for silent capture.
  - **Option B (Advanced)**: Native usage of `ScreenCaptureKit` (likely requires bindings, maybe too complex for MVP).
- **Recommendation for MVP**: Start with `screencapture` CLI wrapper.
- **Status**: MVP implemented with `screencapture` and temp-file loading.

#### 2. IHotkeyService
**File**: `src/ShareX.Avalonia.Platform.MacOS/MacOSHotkeyService.cs`
- **Challenge**: Global hotkeys on macOS require generic event handling or Carbon/Cocoa APIs.
- **Implementation**:
  - Use `SharpHook` (if already in use) or native P/Invoke (ObjC runtime) to register global shortcuts.
  - *Note*: Ensure "Accessibility" permissions are handled/requested.
- **Status**: SharpHook-based global hotkeys implemented with Accessibility permission check (runtime validation pending).

#### 3. IClipboardService (if not fully covered by Avalonia)
**File**: `src/ShareX.Avalonia.Platform.MacOS/MacOSClipboardService.cs`
- Avalonia 11 handles standard text/images well.
- Implement specialized handling if file drops or specific ShareX formats aren't working.
- **Status**: Text via `pbcopy`/`pbpaste`, PNG image + file list via `osascript` (more formats TODO).

#### 4. IWindowService / ISystemInfo
- **Window Management**: Focus stealing, bringing windows to front (often requires `NSRunningApplication`).
- **Startup**: "Open at Login" logic (Launch Agents).
- **Status**: Screen service uses Avalonia `Screens` when a main window is available; window service uses `osascript` for frontmost window info (focus/positioning still TODO).

### Phase 3: Dependency Injection
**File**: `src/ShareX.Avalonia.App/Program.cs` or `Startup.cs`
- Detect OS: `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)`.
- Register `MacOS` implementations instead of `Windows` ones.

```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    services.AddSingleton<IScreenshotService, MacOSScreenshotService>();
    services.AddSingleton<IHotkeyService, MacOSHotkeyService>();
    // ...
}
```

### Phase 4: Packaging & Permissions
- **Info.plist**: Configure properly.
- **Entitlements**:
  - `com.apple.security.device.camera` (if webcam needed)
  - `com.apple.security.device.microphone` (if audio needed)
  - Screen Recording permission (handled by OS prompt, but app must handle denial gracefully).
- **Bundle**: Ensure `dotnet publish -r osx-arm64` creates a valid `.app` structure (Avalonia typically handles basic structure, but verify).

## Integration Steps
1. [x] Create `feature/macos-implementation` branch.
2. [x] Scaffold `ShareX.Avalonia.Platform.MacOS`.
3. [x] Implement `MacOSScreenshotService` (MVP: `screencapture` -x /tmp/temp.png).
4. [x] Wire up DI in `Program.cs`.
5. [ ] Test build on simple macOS environment (github actions or local).

## Deliverables
- [x] `ShareX.Avalonia.Platform.MacOS` project.
- [x] Ability to take a region screenshot on macOS.
- [x] Global Hotkeys implemented on macOS (SharpHook; validation pending).
- [ ] Clipboard upload (text/image) working.
- [x] Instructions for granting permissions (Screen Recording) in `README.md`.

## Estimated Effort
**High** - 5-7 days for full feature parity (MVP: 2-3 days).

## Success Criteria
- User can run ShareX Avalonia on a Mac.
- Cmd+Shift+4 (or mapped hotkey) triggers capture.
- Image is captured and uploaded/saved.


