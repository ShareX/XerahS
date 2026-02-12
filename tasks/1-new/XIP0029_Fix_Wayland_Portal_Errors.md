# Fix Wayland Portal DBus Errors

## Context
When running on Linux Wayland, the application crashes or fails to initialize services with `Tmds.DBus` errors.

## Errors Observed

1. **LinuxScreenCaptureService**:
   ```
   System.ArgumentException: Duplicate type name within an assembly.
   at Tmds.DBus.CodeGen.DBusObjectProxyTypeBuilder.Build(Type interfaceType)
   at XerahS.Platform.Linux.LinuxScreenCaptureService.CaptureWithPortalAsync()
   ```

2. **WaylandPortalHotkeyService / WaylandPortalInputService**:
   ```
   System.ArgumentException: Signal System.Threading.Tasks.Task`1[System.IDisposable] WatchActivatedAsync(...) must accept an argument of Type 'Action'/'Action<>' and optional argument of Type 'Action<Exception>'
   ```

3. **WaylandPortalSystemService**:
   ```
   System.TypeLoadException: Type '...' is attempting to implement an inaccessible interface.
   ```

## Root Cause Analysis
- **Duplicate Type Name**: Likely caused by multiple interfaces sharing the same name or potentially `IScreenshotPortal` being generated twice in the dynamic assembly without proper caching/namespacing by `Tmds.DBus`.
- **Signal Mismatch**: The `WatchActivatedAsync` delegate signature in the interface does not match what `Tmds.DBus` expects for a signal handler. It likely needs `Action<...>` instead of just the values, or the error message is specific about the error handler argument.
- **Inaccessible Interface**: The interface `IOpenUriPortalProxy` (or similar) is likely `internal` or `private` but the generated proxy in a dynamic assembly cannot access it. `Tmds.DBus` requires interfaces to be `public` or `[assembly: InternalsVisibleTo(Tmds.DBus.Emit)]`.

## Implementation Plan

1. **Fix Visibility**: Ensure all DBus interfaces (`IScreenshotPortal`, `IGlobalShortcuts`, `IInputCapture`, `IOpenURI`) are `public`.
2. **Fix Signal Signatures**: Update `WatchActivatedAsync` signatures to change from receiving a Tuple to receiving individual arguments, which `Tmds.DBus` expects for Signals.
   - From: `Action<(ObjectPath, string, ulong, IDictionary<string, object>)>`
   - To: `Action<ObjectPath, string, ulong, IDictionary<string, object>>`
3. **Resolve Duplicate Types**:
   - Check for duplicate interface definitions.
   - If `IScreenshotPortal` is defined in multiple files/namespaces, consolidate them.
   - Investigate if `CaptureWithPortalAsync` creating a new connection/proxy every time triggers this. (Ideally it shouldn't, but we can try to reuse the connection/proxy).

## Verification
- Run `dotnet run` on Wayland.
- Verify `WaylandPortalHotkeyService`, `WaylandPortalInputService`, and `WaylandPortalSystemService` initialize without errors.
- Verify Screen Capture works via Portal.
