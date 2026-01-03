#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using System;
using System.Collections.Generic;
using System.Drawing;
using ShareX.Ava.Platform.Abstractions;
using DebugHelper = ShareX.Ava.Common.DebugHelper;

namespace ShareX.Ava.Platform.MacOS
{
    /// <summary>
    /// macOS window management service (stub for MVP).
    /// </summary>
    public class MacOSWindowService : IWindowService
    {
        private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);
        private static readonly object WarnLock = new();

        public IntPtr GetForegroundWindow()
        {
            LogNotImplemented(nameof(GetForegroundWindow));
            return IntPtr.Zero;
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            LogNotImplemented(nameof(SetForegroundWindow));
            return false;
        }

        public string GetWindowText(IntPtr handle)
        {
            LogNotImplemented(nameof(GetWindowText));
            return string.Empty;
        }

        public string GetWindowClassName(IntPtr handle)
        {
            LogNotImplemented(nameof(GetWindowClassName));
            return string.Empty;
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            LogNotImplemented(nameof(GetWindowBounds));
            return Rectangle.Empty;
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
            LogNotImplemented(nameof(GetWindowClientBounds));
            return Rectangle.Empty;
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            LogNotImplemented(nameof(IsWindowVisible));
            return false;
        }

        public bool IsWindowMaximized(IntPtr handle)
        {
            LogNotImplemented(nameof(IsWindowMaximized));
            return false;
        }

        public bool IsWindowMinimized(IntPtr handle)
        {
            LogNotImplemented(nameof(IsWindowMinimized));
            return false;
        }

        public bool ShowWindow(IntPtr handle, int cmdShow)
        {
            LogNotImplemented(nameof(ShowWindow));
            return false;
        }

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
        {
            LogNotImplemented(nameof(SetWindowPos));
            return false;
        }

        public ShareX.Ava.Platform.Abstractions.WindowInfo[] GetAllWindows()
        {
            LogNotImplemented(nameof(GetAllWindows));
            return Array.Empty<ShareX.Ava.Platform.Abstractions.WindowInfo>();
        }

        public uint GetWindowProcessId(IntPtr handle)
        {
            LogNotImplemented(nameof(GetWindowProcessId));
            return 0;
        }

        private static void LogNotImplemented(string memberName)
        {
            lock (WarnLock)
            {
                if (!Warned.Add(memberName))
                {
                    return;
                }
            }

            DebugHelper.WriteLine($"MacOSWindowService: {memberName} is not implemented yet.");
        }
    }
}
