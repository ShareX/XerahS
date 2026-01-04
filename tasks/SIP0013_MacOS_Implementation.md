# CX07: MacOS Implementation

## Priority
**HIGH** - Major cross-platform milestone.

## Assignee
**Codex**

## Branch
`feature/macos-implementation`

---

## Current Status: 100% Complete (2026-01-04 10:00) — code complete; manual macOS validation pending

> [!NOTE]
> The build-breaking `IInputService` gap has been fixed. macOS now has all 8 service files matching Windows.

---

## Guiding Principle

> [!IMPORTANT]
> **For each function in the Windows implementation, implement the corresponding macOS function.**
> 
> This ensures feature parity across platforms. Use the Windows implementation as your reference.

---

## Service File Parity Checklist

| Windows File | macOS File | Status |
|--------------|------------|--------|
| `WindowsClipboardService.cs` | `MacOSClipboardService.cs` | Complete |
| `WindowsScreenCaptureService.cs` | `MacOSScreenshotService.cs` | Complete |
| `WindowsScreenService.cs` | `MacOSScreenService.cs` | Complete |
| `WindowsWindowService.cs` | `MacOSWindowService.cs` | Complete |
| `WindowsPlatformInfo.cs` | `MacOSPlatformInfo.cs` | Complete |
| `WindowsHotkeyService.cs` | `MacOSHotkeyService.cs` | Complete |
| `WindowsInputService.cs` | `MacOSInputService.cs` | Complete |
| `WindowsPlatform.cs` | `MacOSPlatform.cs` | Complete |

---

## Remaining Work: MacOSWindowService.cs

### Function-by-Function Comparison

| Windows Method | Windows Implementation | macOS Status | Action Required |
|----------------|------------------------|--------------|-----------------|
| `GetForegroundWindow()` | `NativeMethods.GetForegroundWindow()` | Returns Zero | OK for macOS (no HWND) |
| `SetForegroundWindow(handle)` | `NativeMethods.SetForegroundWindow(handle)` | AppleScript | Done |
| `GetWindowText(handle)` | `NativeMethods.GetWindowText(handle)` | osascript | Done |
| `GetWindowClassName(handle)` | `NativeMethods.GetClassName(handle)` | osascript | Done |
| `GetWindowBounds(handle)` | `CaptureHelpers.GetWindowRectangle(handle)` | osascript | Done |
| `GetWindowClientBounds(handle)` | `NativeMethods.GetClientRect(handle)` | osascript | Done |
| `IsWindowVisible(handle)` | `NativeMethods.IsWindowVisible(handle)` | AppleScript | Done |
| `IsWindowMaximized(handle)` | `NativeMethods.IsZoomed(handle)` | AppleScript | Done |
| `IsWindowMinimized(handle)` | `NativeMethods.IsIconic(handle)` | AppleScript | Done |
| `ShowWindow(handle, cmdShow)` | `NativeMethods.ShowWindow(handle, cmdShow)` | AppleScript | Done |
| `SetWindowPos(...)` | `NativeMethods.SetWindowPos(...)` | AppleScript | Done |
| `GetAllWindows()` | EnumWindows (simplified) | Front only | Acceptable MVP |
| `GetWindowProcessId(handle)` | `NativeMethods.GetWindowThreadProcessId` | osascript | Done |

---

## Implementation Guide for Remaining Methods

**File**: `src/ShareX.Avalonia.Platform.MacOS/MacOSWindowService.cs`

Use AppleScript via `osascript`. The file already has a `RunOsaScriptWithOutput` helper.

### 1. SetForegroundWindow (Priority: HIGH)

Activates an application by bringing it to front.

```csharp
public bool SetForegroundWindow(IntPtr handle)
{
    // Get app name from cached front window info or parameter
    if (!TryGetFrontWindowInfo(out var appName, out _, out _))
        return false;
    
    var script = $"tell application \\\"{appName}\\\" to activate";
    var output = RunOsaScriptWithOutput(script);
    return output != null; // null means error
}
```

### 2. IsWindowMaximized (Priority: MEDIUM)

```csharp
public bool IsWindowMaximized(IntPtr handle)
{
    const string script =
        "tell application \\\"System Events\\\"\\n" +
        "set frontApp to first application process whose frontmost is true\\n" +
        "return zoomed of front window of frontApp\\n" +
        "end tell";
    
    var output = RunOsaScriptWithOutput(script);
    return output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
}
```

### 3. IsWindowMinimized (Priority: MEDIUM)

```csharp
public bool IsWindowMinimized(IntPtr handle)
{
    const string script =
        "tell application \\\"System Events\\\"\\n" +
        "set frontApp to first application process whose frontmost is true\\n" +
        "return miniaturized of front window of frontApp\\n" +
        "end tell";
    
    var output = RunOsaScriptWithOutput(script);
    return output?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
}
```

### 4. ShowWindow (Priority: LOW)

Map Windows `cmdShow` values to AppleScript commands.

```csharp
public bool ShowWindow(IntPtr handle, int cmdShow)
{
    // SW_MINIMIZE = 6, SW_RESTORE = 9, SW_MAXIMIZE = 3
    string? script = cmdShow switch
    {
        6 => "tell application \"System Events\" to set miniaturized of front window of (first process whose frontmost is true) to true",
        9 => "tell application \"System Events\" to set miniaturized of front window of (first process whose frontmost is true) to false",
        3 => "tell application \"System Events\" to set zoomed of front window of (first process whose frontmost is true) to true",
        _ => null
    };
    
    if (script == null) return false;
    return RunOsaScriptWithOutput(script.Replace("\"", "\\\"")) != null;
}
```

### 5. SetWindowPos (Priority: LOW)

```csharp
public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
{
    var script =
        "tell application \\\"System Events\\\"\\n" +
        "set frontApp to first application process whose frontmost is true\\n" +
        $"set position of front window of frontApp to {{{x}, {y}}}\\n" +
        $"set size of front window of frontApp to {{{width}, {height}}}\\n" +
        "end tell";
    
    return RunOsaScriptWithOutput(script) != null;
}
```

---

## Verification Plan

### Build Verification
```bash
cd src/ShareX.Avalonia.Platform.MacOS
dotnet build
```

### Manual Testing on macOS
1. Run the app on macOS
2. Register a hotkey (e.g., Cmd+Shift+4)
3. Press hotkey to verify screenshot captured
4. Verify image appears in editor/history

---

## Task Checklist

- [x] Create `ShareX.Avalonia.Platform.MacOS` project
- [x] Implement `MacOSScreenshotService` (screencapture CLI)
- [x] Implement `MacOSHotkeyService` (SharpHook)
- [x] Implement `MacOSClipboardService` (pbcopy/pbpaste + osascript)
- [x] Implement `MacOSScreenService` (Avalonia Screens)
- [x] Implement `MacOSPlatformInfo`
- [x] Implement `MacOSInputService` (GetCursorPosition)
- [x] Wire up `MacOSPlatform.Initialize()` with all services
- [x] **Implement `SetForegroundWindow`** - HIGH PRIORITY
- [x] **Implement `IsWindowMaximized`**
- [x] **Implement `IsWindowMinimized`**
- [x] Implement `ShowWindow` (optional)
- [x] Implement `SetWindowPos` (optional)
- [x] Build verification (dotnet build)
- [ ] Manual testing on macOS hardware

---

## Estimated Remaining Effort
**0 hours** - Code complete; awaiting manual macOS validation

## Success Criteria
- Project builds without errors
- All required interface methods have implementations (not stubs)
- Hotkey triggers capture flow on macOS
- App can bring itself to foreground after capture








