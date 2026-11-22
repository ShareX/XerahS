#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
using XerahS.Common;
using XerahS.Platform.Abstractions;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using XerahS.Platform.Linux.Wayland.WindowQuery;

namespace XerahS.Platform.Linux
{
    public class LinuxWindowService : IWindowService, ILogicalWindowPointQueryService, IDisposable
    {
        private const long MaxPropertyLongLength = 4096;
        private static readonly string[] ExcludedWindowTypeNames =
        [
            "_NET_WM_WINDOW_TYPE_DESKTOP",
            "_NET_WM_WINDOW_TYPE_DOCK",
            "_NET_WM_WINDOW_TYPE_DROPDOWN_MENU",
            "_NET_WM_WINDOW_TYPE_POPUP_MENU",
            "_NET_WM_WINDOW_TYPE_TOOLTIP",
            "_NET_WM_WINDOW_TYPE_NOTIFICATION",
            "_NET_WM_WINDOW_TYPE_COMBO",
            "_NET_WM_WINDOW_TYPE_DND",
            "_NET_WM_WINDOW_TYPE_SPLASH"
        ];
        private static readonly string[] ExcludedWindowStateNames =
        [
            "_NET_WM_STATE_HIDDEN",
            "_NET_WM_STATE_SKIP_PAGER",
            "_NET_WM_STATE_SKIP_TASKBAR"
        ];
        private readonly IntPtr _display;
        private readonly IntPtr _rootWindow;
        private readonly object _x11Lock = new();
        private bool _disposed;
        private readonly Dictionary<string, IntPtr> _atomCache = new(StringComparer.Ordinal);
        private readonly IWaylandWindowPointQueryHelper _waylandWindowPointQueryHelper;

        public LinuxWindowService()
        {
            _waylandWindowPointQueryHelper = WaylandWindowPointQueryHelperFactory.Create();

            try
            {
                NativeMethods.EnsureX11ThreadsInitialized();
                _display = NativeMethods.XOpenDisplay(null);
                if (_display != IntPtr.Zero)
                {
                    _rootWindow = NativeMethods.XDefaultRootWindow(_display);
                }
                else
                {
                    DebugHelper.WriteLine("LinuxWindowService: XOpenDisplay returned null (display not available).");
                    DebugHelper.WriteLine("  This is normal on Wayland without XWayland or in restricted environments.");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "LinuxWindowService: Failed to open X display");
                DebugHelper.WriteLine("  Window management features may be limited.");
                _display = IntPtr.Zero;
            }
        }

        private bool IsDisplayReady => !_disposed && _display != IntPtr.Zero;

        public WindowPointQueryCapability GetLogicalWindowPointQueryCapability()
        {
            return _waylandWindowPointQueryHelper.Capability;
        }

        public WindowInfo? GetWindowAtLogicalPoint(System.Drawing.Point logicalPoint)
        {
            return _waylandWindowPointQueryHelper.GetWindowAtPoint(logicalPoint);
        }

        public void Dispose()
        {
            lock (_x11Lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                if (_display != IntPtr.Zero)
                {
                    NativeMethods.XCloseDisplay(_display);
                }
            }
        }

        public IntPtr GetForegroundWindow()
        {
            DebugHelper.WriteLine("LinuxWindowService: GetForegroundWindow called");
            lock (_x11Lock)
            {
                if (!IsDisplayReady)
                {
                    DebugHelper.WriteLine("LinuxWindowService: GetForegroundWindow: Display is IntPtr.Zero");
                    return IntPtr.Zero;
                }

                (IntPtr focus, int revert_to) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    NativeMethods.XGetInputFocus(_display, out IntPtr f, out int rt);
                    return (f, rt);
                }, out int errorCode);

                DebugHelper.WriteLine($"LinuxWindowService: XGetInputFocus returned: focus={focus} (0x{focus:X}), revert_to={revert_to}");

                if (errorCode != 0 || NativeMethods.IsInvalidWindowHandle(focus) || focus == _rootWindow)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: Ignoring non-window X11 focus handle {focus} (0x{focus:X}, error={errorCode}).");
                    return IntPtr.Zero;
                }

                // The focused window might be a child widget (like an input field).
                // Walk up the window tree to find the top-level window
                IntPtr topLevelWindow = GetTopLevelWindowLocked(focus);
                if (topLevelWindow != focus)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: Walked up window tree: focus={focus} (0x{focus:X}) -> top-level={topLevelWindow} (0x{topLevelWindow:X})");
                }

                return topLevelWindow;
            }
        }

        /// <summary>
        /// Traverse up the window hierarchy to find the top-level window
        /// (the window whose parent is the root window)
        /// </summary>
        private IntPtr GetTopLevelWindowLocked(IntPtr window)
        {
            if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(window))
            {
                return IntPtr.Zero;
            }

            // If the window is already the root window, return it
            if (window == _rootWindow)
            {
                return window;
            }

            IntPtr currentWindow = window;
            int maxDepth = 50; // Prevent infinite loops
            int depth = 0;

            try
            {
                // Walk up the window tree until we find a window whose parent is the root window
                while (depth < maxDepth)
                {
                    depth++;

                    (int result, IntPtr parent, IntPtr children) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                    {
                        int r = NativeMethods.XQueryTree(
                            _display,
                            currentWindow,
                            out IntPtr _,
                            out IntPtr p,
                            out IntPtr c,
                            out uint _
                        );
                        return (r, p, c);
                    }, out int errorCode);

                    // Free the children list if allocated
                    if (children != IntPtr.Zero)
                    {
                        try
                        {
                            NativeMethods.XFree(children);
                        }
                        catch
                        {
                            // Ignore errors when freeing
                        }
                    }

                    if (result == 0 || errorCode != 0)
                    {
                        // XQueryTree failed
                        DebugHelper.WriteLine($"LinuxWindowService: XQueryTree failed for window {currentWindow:X} at depth {depth} (result={result}, error={errorCode})");
                        return depth == 1 ? IntPtr.Zero : currentWindow;
                    }

                    // If parent is root or zero, currentWindow is the top-level window
                    if (parent == _rootWindow || NativeMethods.IsInvalidWindowHandle(parent))
                    {
                        DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: {window:X} -> {currentWindow:X} (depth={depth})");
                        return currentWindow;
                    }

                    // Move up to the parent
                    currentWindow = parent;
                }

                DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: Hit max depth ({maxDepth}), returning current window");
                DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: {window:X} -> {currentWindow:X} (depth={depth})");
                return currentWindow;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: Exception - {ex.Message}, returning IntPtr.Zero");
                return IntPtr.Zero;
            }
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            lock (_x11Lock)
            {
                if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(handle)) return false;

                NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    NativeMethods.XSetInputFocus(_display, handle, 1 /* RevertToParent */, IntPtr.Zero /* CurrentTime */);
                    NativeMethods.XRaiseWindow(_display, handle);
                    return true;
                }, out int errorCode);

                return errorCode == 0;
            }
        }

        public string GetWindowText(IntPtr handle)
        {
            lock (_x11Lock)
            {
                if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(handle)) return string.Empty;

                if (TryGetUtf8StringProperty(handle, "_NET_WM_VISIBLE_NAME", out string visibleName))
                {
                    return visibleName;
                }

                if (TryGetUtf8StringProperty(handle, "_NET_WM_NAME", out string title))
                {
                    return title;
                }

                (int fetchResult, IntPtr namePtr) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    int r = NativeMethods.XFetchName(_display, handle, out IntPtr p);
                    return (r, p);
                }, out int errorCode);

                if (errorCode == 0 && fetchResult != 0 && namePtr != IntPtr.Zero)
                {
                    try
                    {
                        return Marshal.PtrToStringAnsi(namePtr) ?? string.Empty;
                    }
                    finally
                    {
                        NativeMethods.XFree(namePtr);
                    }
                }
                else if (namePtr != IntPtr.Zero)
                {
                    NativeMethods.XFree(namePtr);
                }
                return string.Empty;
            }
        }

        public string GetWindowClassName(IntPtr handle)
        {
            lock (_x11Lock)
            {
                if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(handle)) return string.Empty;

                (int result, XClassHint hint) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    int r = NativeMethods.XGetClassHint(_display, handle, out XClassHint h);
                    return (r, h);
                }, out int errorCode);

                if (errorCode == 0 && result != 0)
                {
                    string resClass = Marshal.PtrToStringAnsi(hint.res_class) ?? string.Empty;
                    if (hint.res_class != IntPtr.Zero) NativeMethods.XFree(hint.res_class);
                    if (hint.res_name != IntPtr.Zero) NativeMethods.XFree(hint.res_name);
                    return resClass;
                }
                return string.Empty;
            }
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            return GetWindowBoundsCore(handle, logDiagnostics: true);
        }

        private Rectangle GetWindowBoundsCore(IntPtr handle, bool logDiagnostics)
        {
            if (logDiagnostics)
            {
                DebugHelper.WriteLine($"LinuxWindowService: GetWindowBounds called for handle {handle} (0x{handle:X})");
            }

            lock (_x11Lock)
            {
                if (!IsDisplayReady)
                {
                    if (logDiagnostics)
                    {
                        DebugHelper.WriteLine("LinuxWindowService: GetWindowBounds: Display is IntPtr.Zero");
                    }

                    return Rectangle.Empty;
                }

                if (NativeMethods.IsInvalidWindowHandle(handle))
                {
                    if (logDiagnostics)
                    {
                        DebugHelper.WriteLine($"LinuxWindowService: GetWindowBounds: Ignoring invalid window handle {handle} (0x{handle:X})");
                    }

                    return Rectangle.Empty;
                }

                (int result, XWindowAttributes attrs) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    var a = new XWindowAttributes();
                    int r = NativeMethods.XGetWindowAttributes(_display, handle, ref a);
                    return (r, a);
                }, out int attrError);

                if (logDiagnostics)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: XGetWindowAttributes returned: {result} (error={attrError})");
                }

                if (attrError == 0 && result != 0)
                {
                    if (logDiagnostics)
                    {
                        DebugHelper.WriteLine($"LinuxWindowService: XWindowAttributes (relative): x={attrs.x}, y={attrs.y}, width={attrs.width}, height={attrs.height}, map_state={attrs.map_state}, border_width={attrs.border_width}");
                    }

                    // Check if window is actually viewable
                    if (logDiagnostics)
                    {
                        string mapStateStr = attrs.map_state switch
                        {
                            0 => "IsUnviewable",
                            1 => "IsViewable",
                            2 => "IsUnmapped",
                            _ => $"Unknown({attrs.map_state})"
                        };
                        DebugHelper.WriteLine($"LinuxWindowService: Window map state: {mapStateStr}");
                    }

                    // Translate coordinates to root window (absolute screen coordinates)
                    // The coordinates from XGetWindowAttributes are relative to the parent window
                    (int translateResult, int absoluteX, int absoluteY) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                    {
                        int t = NativeMethods.XTranslateCoordinates(
                            _display,
                            handle,           // source window
                            _rootWindow,      // destination (root window)
                            0, 0,             // source coordinates (0,0 of the window)
                            out int ax,
                            out int ay,
                            out IntPtr _
                        );
                        return (t, ax, ay);
                    }, out int translateError);

                    if (logDiagnostics)
                    {
                        DebugHelper.WriteLine($"LinuxWindowService: XTranslateCoordinates returned: {translateResult} (error={translateError}), absolute: x={absoluteX}, y={absoluteY}");
                    }

                    if (translateError != 0 || translateResult == 0)
                    {
                        return Rectangle.Empty;
                    }

                    // Use the absolute coordinates instead of the relative ones
                    var rect = new Rectangle(absoluteX, absoluteY, attrs.width, attrs.height);
                    if (TryGetFrameExtents(handle, out var frameExtents))
                    {
                        rect = ApplyFrameExtents(rect, frameExtents.Left, frameExtents.Right, frameExtents.Top, frameExtents.Bottom);
                    }

                    if (logDiagnostics)
                    {
                        DebugHelper.WriteLine($"LinuxWindowService: GetWindowBounds returning: {rect}");
                    }

                    // Sanity check
                    if (logDiagnostics && (attrs.width <= 0 || attrs.height <= 0))
                    {
                        DebugHelper.WriteLine("LinuxWindowService: WARNING: Window has invalid dimensions!");
                    }
                    if (logDiagnostics && (absoluteX < -10000 || absoluteY < -10000 || absoluteX > 10000 || absoluteY > 10000))
                    {
                        DebugHelper.WriteLine("LinuxWindowService: WARNING: Window coordinates seem out of reasonable range!");
                    }

                    return rect;
                }

                if (logDiagnostics)
                {
                    DebugHelper.WriteLine("LinuxWindowService: XGetWindowAttributes failed, returning Rectangle.Empty");
                }

                return Rectangle.Empty;
            }
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
            return GetWindowBounds(handle);
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            return TryGetWindowAttributes(handle, out var attrs) && attrs.map_state == NativeMethods.IsViewable;
        }

        public bool IsWindowMaximized(IntPtr handle)
        {
            // Not implemented in MVP
            return false;
        }

        public bool IsWindowMinimized(IntPtr handle)
        {
            // Not implemented in MVP
            return false;
        }

        public bool ShowWindow(IntPtr handle, int cmdShow)
        {
            lock (_x11Lock)
            {
                if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(handle)) return false;

                NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    if (cmdShow == 0)
                        NativeMethods.XIconifyWindow(_display, handle, 0);
                    else
                        NativeMethods.XRaiseWindow(_display, handle);
                    return true;
                }, out int errorCode);

                return errorCode == 0;
            }
        }

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
        {
            lock (_x11Lock)
            {
                if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(handle)) return false;

                NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    NativeMethods.XMoveResizeWindow(_display, handle, x, y, width, height);
                    return true;
                }, out int errorCode);

                return errorCode == 0;
            }
        }

        public WindowInfo[] GetAllWindows()
        {
            lock (_x11Lock)
            {
                if (!IsDisplayReady) return Array.Empty<WindowInfo>();

                var list = new List<WindowInfo>();
                var seenHandles = new HashSet<IntPtr>();

                foreach (var handle in EnumerateCandidateWindowsLocked())
                {
                    if (NativeMethods.IsInvalidWindowHandle(handle) || handle == _rootWindow || !seenHandles.Add(handle))
                        continue;

                    if (!TryCreateWindowInfo(handle, out var windowInfo))
                        continue;

                    list.Add(windowInfo);
                }

                return list.ToArray();
            }
        }

        private IEnumerable<IntPtr> EnumerateCandidateWindowsLocked()
        {
            if (TryGetManagedWindowHandles(out var managedWindows))
            {
                for (int i = managedWindows.Length - 1; i >= 0; i--)
                {
                    yield return managedWindows[i];
                }

                yield break;
            }

            if (!TryGetRootChildWindowsLocked(out var rootChildren))
                yield break;

            for (int i = rootChildren.Length - 1; i >= 0; i--)
            {
                yield return rootChildren[i];
            }
        }

        private bool TryGetManagedWindowHandles(out IntPtr[] handles)
        {
            return TryGetWindowHandleArrayProperty(_rootWindow, "_NET_CLIENT_LIST_STACKING", out handles) && handles.Length > 0;
        }

        private bool TryGetRootChildWindowsLocked(out IntPtr[] handles)
        {
            handles = Array.Empty<IntPtr>();
            if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(_rootWindow))
                return false;

            (int result, IntPtr children, uint childCount) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
            {
                int r = NativeMethods.XQueryTree(_display, _rootWindow, out _, out _, out IntPtr ch, out uint cnt);
                return (r, ch, cnt);
            }, out int errorCode);

            try
            {
                if (errorCode != 0 || result == 0 || childCount == 0 || children == IntPtr.Zero)
                {
                    return false;
                }

                handles = new IntPtr[childCount];
                Marshal.Copy(children, handles, 0, (int)childCount);
                return true;
            }
            finally
            {
                if (children != IntPtr.Zero)
                {
                    NativeMethods.XFree(children);
                }
            }
        }

        private bool TryCreateWindowInfo(IntPtr handle, out WindowInfo windowInfo)
        {
            windowInfo = default!;

            if (!TryGetWindowAttributes(handle, out var attrs))
                return false;

            if (attrs.map_state != NativeMethods.IsViewable || attrs.override_redirect)
                return false;

            if (HasAnyPropertyAtom(handle, "_NET_WM_WINDOW_TYPE", ExcludedWindowTypeNames) ||
                HasAnyPropertyAtom(handle, "_NET_WM_STATE", ExcludedWindowStateNames))
            {
                return false;
            }

            string title = GetWindowText(handle);
            if (string.IsNullOrWhiteSpace(title))
                return false;

            var bounds = GetWindowBoundsCore(handle, logDiagnostics: false);
            if (bounds.Width <= 1 || bounds.Height <= 1)
                return false;

            windowInfo = new WindowInfo
            {
                Handle = handle,
                Title = title,
                ClassName = GetWindowClassName(handle),
                Bounds = bounds,
                IsVisible = true
            };

            return true;
        }

        private bool TryGetWindowAttributes(IntPtr handle, out XWindowAttributes attributes)
        {
            attributes = new XWindowAttributes();
            lock (_x11Lock)
            {
                if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(handle))
                    return false;

                (int result, XWindowAttributes attrs) = NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                {
                    var a = new XWindowAttributes();
                    int r = NativeMethods.XGetWindowAttributes(_display, handle, ref a);
                    return (r, a);
                }, out int errorCode);

                if (errorCode != 0 || result == 0)
                    return false;

                attributes = attrs;
                return true;
            }
        }

        private bool HasAnyPropertyAtom(IntPtr handle, string propertyName, IEnumerable<string> atomNames)
        {
            if (!TryGetWindowHandleArrayProperty(handle, propertyName, out var propertyAtoms) || propertyAtoms.Length == 0)
                return false;

            foreach (var atomName in atomNames)
            {
                IntPtr atom = GetAtom(atomName);
                if (atom == IntPtr.Zero)
                    continue;

                for (int i = 0; i < propertyAtoms.Length; i++)
                {
                    if (propertyAtoms[i] == atom)
                        return true;
                }
            }

            return false;
        }

        private bool TryGetWindowHandleArrayProperty(IntPtr handle, string propertyName, out IntPtr[] values)
        {
            values = Array.Empty<IntPtr>();

            if (!TryGetProperty(handle, propertyName, out var property))
                return false;

            using (property)
            {
                if (property.Format != 32 || property.ItemCount <= 0)
                    return false;

                values = ReadIntPtrArray(property.Data, property.ItemCount);
                return values.Length > 0;
            }
        }

        private bool TryGetUtf8StringProperty(IntPtr handle, string propertyName, out string value)
        {
            value = string.Empty;

            if (!TryGetProperty(handle, propertyName, out var property))
                return false;

            using (property)
            {
                if (property.Format != 8 || property.ItemCount <= 0)
                    return false;

                int length = checked((int)property.ItemCount);
                var bytes = new byte[length];
                Marshal.Copy(property.Data, bytes, 0, length);
                value = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        private bool TryGetFrameExtents(IntPtr handle, out FrameExtents extents)
        {
            extents = default;

            if (!TryGetProperty(handle, "_NET_FRAME_EXTENTS", out var property))
                return false;

            using (property)
            {
                if (property.Format != 32 || property.ItemCount < 4)
                    return false;

                var values = ReadIntPtrArray(property.Data, property.ItemCount);
                if (values.Length < 4)
                    return false;

                int left = ToInt32(values[0]);
                int right = ToInt32(values[1]);
                int top = ToInt32(values[2]);
                int bottom = ToInt32(values[3]);

                if (left < 0 || right < 0 || top < 0 || bottom < 0)
                    return false;

                extents = new FrameExtents(left, right, top, bottom);
                return true;
            }
        }

        private bool TryGetProperty(IntPtr handle, string propertyName, out XProperty property)
        {
            property = default;

            lock (_x11Lock)
            {
                if (!IsDisplayReady || NativeMethods.IsInvalidWindowHandle(handle))
                    return false;

                IntPtr propertyAtom = GetAtom(propertyName);
                if (propertyAtom == IntPtr.Zero)
                    return false;

                (int result, IntPtr actualType, int actualFormat, IntPtr itemCount, IntPtr data) =
                    NativeMethods.ExecuteWithX11ErrorGuard(_display, () =>
                    {
                        int r = NativeMethods.XGetWindowProperty(
                            _display,
                            handle,
                            propertyAtom,
                            0,
                            MaxPropertyLongLength,
                            false,
                            IntPtr.Zero,
                            out IntPtr type,
                            out int format,
                            out IntPtr count,
                            out _,
                            out IntPtr propData);
                        return (r, type, format, count, propData);
                    }, out int errorCode);

                if (errorCode != 0 || result != 0 || actualType == IntPtr.Zero || data == IntPtr.Zero)
                {
                    if (data != IntPtr.Zero)
                    {
                        NativeMethods.XFree(data);
                    }

                    return false;
                }

                long count = itemCount.ToInt64();
                if (count <= 0)
                {
                    NativeMethods.XFree(data);
                    return false;
                }

                property = new XProperty(data, count, actualFormat);
                return true;
            }
        }

        private IntPtr GetAtom(string atomName)
        {
            lock (_x11Lock)
            {
                if (!IsDisplayReady)
                    return IntPtr.Zero;

                if (_atomCache.TryGetValue(atomName, out var atom))
                    return atom;

                atom = NativeMethods.ExecuteWithX11ErrorGuard(
                    _display,
                    () => NativeMethods.XInternAtom(_display, atomName, only_if_exists: false),
                    out int errorCode);

                if (errorCode != 0)
                    return IntPtr.Zero;

                _atomCache[atomName] = atom;
                return atom;
            }
        }

        private static IntPtr[] ReadIntPtrArray(IntPtr data, long itemCount)
        {
            int length = checked((int)itemCount);
            var values = new IntPtr[length];
            Marshal.Copy(data, values, 0, length);
            return values;
        }

        private static int ToInt32(IntPtr value)
        {
            long intValue = value.ToInt64();
            return checked((int)intValue);
        }

        internal static Rectangle ApplyFrameExtents(Rectangle clientBounds, int left, int right, int top, int bottom)
        {
            if (left == 0 && right == 0 && top == 0 && bottom == 0)
                return clientBounds;

            long x = (long)clientBounds.X - left;
            long y = (long)clientBounds.Y - top;
            long width = (long)clientBounds.Width + left + right;
            long height = (long)clientBounds.Height + top + bottom;

            if (x < int.MinValue || x > int.MaxValue ||
                y < int.MinValue || y > int.MaxValue ||
                width <= 0 || width > int.MaxValue ||
                height <= 0 || height > int.MaxValue)
            {
                return clientBounds;
            }

            return new Rectangle((int)x, (int)y, (int)width, (int)height);
        }

        internal static bool ContainsExcludedWindowTypeName(IEnumerable<string> windowTypes)
        {
            ArgumentNullException.ThrowIfNull(windowTypes);

            return windowTypes.Any(windowType =>
                ExcludedWindowTypeNames.Contains(windowType, StringComparer.Ordinal));
        }

        internal static bool ContainsExcludedWindowStateName(IEnumerable<string> windowStates)
        {
            ArgumentNullException.ThrowIfNull(windowStates);

            return windowStates.Any(windowState =>
                ExcludedWindowStateNames.Contains(windowState, StringComparer.Ordinal));
        }

        public uint GetWindowProcessId(IntPtr handle)
        {
            return 0;
        }

        public IntPtr SearchWindow(string windowTitle)
        {
            // TODO: Implement proper X11 window search
            if (string.IsNullOrEmpty(windowTitle) || _display == IntPtr.Zero)
                return IntPtr.Zero;

            // Fallback: iterate through all windows and find one with matching title
            var windows = GetAllWindows();
            foreach (var w in windows)
            {
                if (!string.IsNullOrEmpty(w.Title) && w.Title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return w.Handle;
                }
            }
            return IntPtr.Zero;
        }

        public bool ActivateWindow(IntPtr handle)
        {
            // Use SetForegroundWindow which does XSetInputFocus + XRaiseWindow
            return SetForegroundWindow(handle);
        }

        public bool SetWindowClickThrough(IntPtr handle)
        {
            // Click-through windows are not easily supported on X11/Wayland without compositor extensions.
            // This is a no-op for Linux; recording borders will still be visible but interactable.
            return false;
        }

        private readonly struct XProperty : IDisposable
        {
            public XProperty(IntPtr data, long itemCount, int format)
            {
                Data = data;
                ItemCount = itemCount;
                Format = format;
            }

            public IntPtr Data { get; }

            public long ItemCount { get; }

            public int Format { get; }

            public void Dispose()
            {
                if (Data != IntPtr.Zero)
                {
                    NativeMethods.XFree(Data);
                }
            }
        }

        private readonly struct FrameExtents
        {
            public FrameExtents(int left, int right, int top, int bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public int Left { get; }

            public int Right { get; }

            public int Top { get; }

            public int Bottom { get; }
        }
    }
}
