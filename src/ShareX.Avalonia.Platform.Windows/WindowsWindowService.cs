#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using ShareX.Ava.Common;
using ShareX.Ava.Common.Helpers;
using ShareX.Ava.Platform.Abstractions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ShareX.Ava.Platform.Windows
{
    /// <summary>
    /// Windows implementation of IWindowService using NativeMethods
    /// </summary>
    public class WindowsWindowService : IWindowService
    {
        public IntPtr GetForegroundWindow()
        {
            return NativeMethods.GetForegroundWindow();
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            return NativeMethods.SetForegroundWindow(handle);
        }

        public string GetWindowText(IntPtr handle)
        {
            return NativeMethods.GetWindowText(handle);
        }

        public string GetWindowClassName(IntPtr handle)
        {
            return NativeMethods.GetClassName(handle);
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            return CaptureHelpers.GetWindowRectangle(handle);
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
            return NativeMethods.GetClientRect(handle);
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            return NativeMethods.IsWindowVisible(handle);
        }

        public bool IsWindowMaximized(IntPtr handle)
        {
            return NativeMethods.IsZoomed(handle);
        }

        public bool IsWindowMinimized(IntPtr handle)
        {
            return NativeMethods.IsIconic(handle);
        }

        public bool ShowWindow(IntPtr handle, int cmdShow)
        {
            return NativeMethods.ShowWindow(handle, cmdShow);
        }

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
        {
            return NativeMethods.SetWindowPos(handle, handleInsertAfter, x, y, width, height, (SetWindowPosFlags)flags);
        }

        public Abstractions.WindowInfo[] GetAllWindows()
        {
            var windows = new List<Abstractions.WindowInfo>();

            // Use EnumWindows to enumerate all top-level windows
            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                // Skip invisible windows
                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                // Skip cloaked windows (Windows 10/11 virtual desktops, UWP apps, etc.)
                if (NativeMethods.IsWindowCloaked(hWnd))
                    return true;

                // Get window title
                string title = NativeMethods.GetWindowTextString(hWnd);
                
                // Skip windows with no title
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                // Get class name for filtering
                string className = NativeMethods.GetClassNameString(hWnd);

                // Skip certain system windows
                string[] ignoreClasses = { "Progman", "Button", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };
                foreach (var ignoreClass in ignoreClasses)
                {
                    if (className.Equals(ignoreClass, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // Skip child windows (no parent)
                if (NativeMethods.GetParent(hWnd) != IntPtr.Zero)
                    return true;

                // Get window bounds
                var bounds = GetWindowBounds(hWnd);
                
                // Skip windows with zero or very small size
                if (bounds.Width <= 1 || bounds.Height <= 1)
                    return true;

                windows.Add(new Abstractions.WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ClassName = className,
                    Bounds = bounds,
                    ProcessId = GetWindowProcessId(hWnd),
                    IsVisible = true,
                    IsMaximized = IsWindowMaximized(hWnd),
                    IsMinimized = IsWindowMinimized(hWnd)
                });

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return windows.ToArray();
        }

        public uint GetWindowProcessId(IntPtr handle)
        {
            NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
            return processId;
        }

        public IntPtr SearchWindow(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle))
                return IntPtr.Zero;

            // First, try exact match using FindWindow
            IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);

            if (hWnd == IntPtr.Zero)
            {
                // Fallback: iterate through all processes and find one with matching main window title
                foreach (var process in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        if (process.MainWindowTitle.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                        {
                            return process.MainWindowHandle;
                        }
                    }
                    catch
                    {
                        // Ignore access denied exceptions for system processes
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }

            return hWnd;
        }
    }
}
