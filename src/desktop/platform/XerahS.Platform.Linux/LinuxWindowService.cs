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

namespace XerahS.Platform.Linux
{
    public class LinuxWindowService : IWindowService, IDisposable
    {
        private readonly IntPtr _display;
        private readonly IntPtr _rootWindow;

        public LinuxWindowService()
        {
            try
            {
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

        public void Dispose()
        {
            if (_display != IntPtr.Zero)
            {
                NativeMethods.XCloseDisplay(_display);
            }
        }

        public IntPtr GetForegroundWindow()
        {
            DebugHelper.WriteLine("LinuxWindowService: GetForegroundWindow called");
            if (_display == IntPtr.Zero)
            {
                DebugHelper.WriteLine("LinuxWindowService: GetForegroundWindow: Display is IntPtr.Zero");
                return IntPtr.Zero;
            }

            NativeMethods.XGetInputFocus(_display, out IntPtr focus, out int revert_to);
            DebugHelper.WriteLine($"LinuxWindowService: XGetInputFocus returned: focus={focus} (0x{focus:X}), revert_to={revert_to}");

            // The focused window might be a child widget (like an input field).
            // Walk up the window tree to find the top-level window
            IntPtr topLevelWindow = GetTopLevelWindow(focus);
            if (topLevelWindow != focus)
            {
                DebugHelper.WriteLine($"LinuxWindowService: Walked up window tree: focus={focus} (0x{focus:X}) -> top-level={topLevelWindow} (0x{topLevelWindow:X})");
            }

            return topLevelWindow;
        }

        /// <summary>
        /// Traverse up the window hierarchy to find the top-level window
        /// (the window whose parent is the root window)
        /// </summary>
        private IntPtr GetTopLevelWindow(IntPtr window)
        {
            if (_display == IntPtr.Zero || window == IntPtr.Zero)
            {
                return window;
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

                    int result = NativeMethods.XQueryTree(
                        _display,
                        currentWindow,
                        out IntPtr root,
                        out IntPtr parent,
                        out IntPtr children,
                        out uint nchildren
                    );

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

                    if (result == 0)
                    {
                        // XQueryTree failed
                        DebugHelper.WriteLine($"LinuxWindowService: XQueryTree failed for window {currentWindow:X} at depth {depth}");
                        break;
                    }

                    // If parent is root or zero, currentWindow is the top-level window
                    if (parent == _rootWindow || parent == IntPtr.Zero)
                    {
                        DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: {window:X} -> {currentWindow:X} (depth={depth})");
                        return currentWindow;
                    }

                    // Move up to the parent
                    currentWindow = parent;
                }

                if (depth >= maxDepth)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: Hit max depth ({maxDepth}), returning current window");
                }

                DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: {window:X} -> {currentWindow:X} (depth={depth})");
                return currentWindow;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: Exception - {ex.Message}, returning original window");
                return window;
            }
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return false;
            NativeMethods.XSetInputFocus(_display, handle, 1 /* RevertToParent */, IntPtr.Zero /* CurrentTime */);
            NativeMethods.XRaiseWindow(_display, handle);
            return true;
        }

        public string GetWindowText(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return string.Empty;
            if (NativeMethods.XFetchName(_display, handle, out IntPtr namePtr) != 0 && namePtr != IntPtr.Zero)
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
            return string.Empty;
        }

        public string GetWindowClassName(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return string.Empty;
            if (NativeMethods.XGetClassHint(_display, handle, out XClassHint hint) != 0)
            {
                string resClass = Marshal.PtrToStringAnsi(hint.res_class) ?? string.Empty;
                if (hint.res_class != IntPtr.Zero) NativeMethods.XFree(hint.res_class);
                if (hint.res_name != IntPtr.Zero) NativeMethods.XFree(hint.res_name);
                return resClass;
            }
            return string.Empty;
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            DebugHelper.WriteLine($"LinuxWindowService: GetWindowBounds called for handle {handle} (0x{handle:X})");
            if (_display == IntPtr.Zero)
            {
                DebugHelper.WriteLine("LinuxWindowService: GetWindowBounds: Display is IntPtr.Zero");
                return Rectangle.Empty;
            }

            var attrs = new XWindowAttributes();
            int result = NativeMethods.XGetWindowAttributes(_display, handle, ref attrs);
            DebugHelper.WriteLine($"LinuxWindowService: XGetWindowAttributes returned: {result}");

            if (result != 0)
            {
                DebugHelper.WriteLine($"LinuxWindowService: XWindowAttributes (relative): x={attrs.x}, y={attrs.y}, width={attrs.width}, height={attrs.height}, map_state={attrs.map_state}, border_width={attrs.border_width}");

                // Check if window is actually viewable
                string mapStateStr = attrs.map_state switch
                {
                    0 => "IsUnviewable",
                    1 => "IsViewable",
                    2 => "IsUnmapped",
                    _ => $"Unknown({attrs.map_state})"
                };
                DebugHelper.WriteLine($"LinuxWindowService: Window map state: {mapStateStr}");

                // Translate coordinates to root window (absolute screen coordinates)
                // The coordinates from XGetWindowAttributes are relative to the parent window
                int absoluteX, absoluteY;
                IntPtr child;
                int translateResult = NativeMethods.XTranslateCoordinates(
                    _display,
                    handle,           // source window
                    _rootWindow,      // destination (root window)
                    0, 0,             // source coordinates (0,0 of the window)
                    out absoluteX,
                    out absoluteY,
                    out child
                );

                DebugHelper.WriteLine($"LinuxWindowService: XTranslateCoordinates returned: {translateResult}, absolute: x={absoluteX}, y={absoluteY}");

                // Use the absolute coordinates instead of the relative ones
                var rect = new Rectangle(absoluteX, absoluteY, attrs.width, attrs.height);
                DebugHelper.WriteLine($"LinuxWindowService: GetWindowBounds returning: {rect}");

                // Sanity check
                if (attrs.width <= 0 || attrs.height <= 0)
                {
                    DebugHelper.WriteLine("LinuxWindowService: WARNING: Window has invalid dimensions!");
                }
                if (absoluteX < -10000 || absoluteY < -10000 || absoluteX > 10000 || absoluteY > 10000)
                {
                    DebugHelper.WriteLine("LinuxWindowService: WARNING: Window coordinates seem out of reasonable range!");
                }

                return rect;
            }

            DebugHelper.WriteLine("LinuxWindowService: XGetWindowAttributes failed, returning Rectangle.Empty");
            return Rectangle.Empty;
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
            return GetWindowBounds(handle);
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return false;
            var attrs = new XWindowAttributes();
            if (NativeMethods.XGetWindowAttributes(_display, handle, ref attrs) != 0)
            {
                return attrs.map_state == NativeMethods.IsViewable;
            }
            return false;
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
            if (_display == IntPtr.Zero) return false;

            if (cmdShow == 0)
                NativeMethods.XIconifyWindow(_display, handle, 0);
            else
                NativeMethods.XRaiseWindow(_display, handle);

            return true;
        }

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
        {
            if (_display == IntPtr.Zero) return false;
            NativeMethods.XMoveResizeWindow(_display, handle, x, y, width, height);
            return true;
        }

        public WindowInfo[] GetAllWindows()
        {
            if (_display == IntPtr.Zero) return Array.Empty<WindowInfo>();

            var list = new List<WindowInfo>();
            if (NativeMethods.XQueryTree(_display, _rootWindow, out IntPtr root, out IntPtr parent, out IntPtr children, out uint nchildren) != 0)
            {
                if (nchildren > 0 && children != IntPtr.Zero)
                {
                    IntPtr[] windowHandles = new IntPtr[nchildren];
                    Marshal.Copy(children, windowHandles, 0, (int)nchildren);
                    NativeMethods.XFree(children); // Must free the list

                    // In X11, children are ordered bottom-to-top.
                    // Reverse to get Z-order top-to-bottom if needed, but GetAllWindows usually doesn't imply order.

                    foreach (var handle in windowHandles)
                    {
                        var bounds = GetWindowBounds(handle);
                        // Basic filter: Check if visible? Or just return all?
                        // Windows implementation returns active only? No, it returns foreground.
                        // But intent is all.

                        list.Add(new WindowInfo
                        {
                            Handle = handle,
                            Title = GetWindowText(handle),
                            ClassName = GetWindowClassName(handle),
                            Bounds = bounds,
                            IsVisible = IsWindowVisible(handle)
                        });
                    }
                }
            }
            return list.ToArray();
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
    }
}
