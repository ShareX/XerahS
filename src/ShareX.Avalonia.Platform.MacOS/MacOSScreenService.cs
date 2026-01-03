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
    /// macOS screen information service (stub for MVP).
    /// </summary>
    public class MacOSScreenService : IScreenService
    {
        private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);
        private static readonly object WarnLock = new();

        public Rectangle GetVirtualScreenBounds()
        {
            LogNotImplemented(nameof(GetVirtualScreenBounds));
            return Rectangle.Empty;
        }

        public Rectangle GetWorkingArea()
        {
            LogNotImplemented(nameof(GetWorkingArea));
            return Rectangle.Empty;
        }

        public Rectangle GetActiveScreenBounds()
        {
            LogNotImplemented(nameof(GetActiveScreenBounds));
            return Rectangle.Empty;
        }

        public Rectangle GetActiveScreenWorkingArea()
        {
            LogNotImplemented(nameof(GetActiveScreenWorkingArea));
            return Rectangle.Empty;
        }

        public Rectangle GetPrimaryScreenBounds()
        {
            LogNotImplemented(nameof(GetPrimaryScreenBounds));
            return Rectangle.Empty;
        }

        public Rectangle GetPrimaryScreenWorkingArea()
        {
            LogNotImplemented(nameof(GetPrimaryScreenWorkingArea));
            return Rectangle.Empty;
        }

        public ScreenInfo[] GetAllScreens()
        {
            LogNotImplemented(nameof(GetAllScreens));
            return Array.Empty<ScreenInfo>();
        }

        public ScreenInfo GetScreenFromPoint(System.Drawing.Point point)
        {
            LogNotImplemented(nameof(GetScreenFromPoint));
            return CreateDefaultScreenInfo();
        }

        public ScreenInfo GetScreenFromRectangle(System.Drawing.Rectangle rectangle)
        {
            LogNotImplemented(nameof(GetScreenFromRectangle));
            return CreateDefaultScreenInfo();
        }

        private static ScreenInfo CreateDefaultScreenInfo()
        {
            return new ScreenInfo
            {
                Bounds = Rectangle.Empty,
                WorkingArea = Rectangle.Empty,
                IsPrimary = true,
                DeviceName = "Unknown",
                BitsPerPixel = 0
            };
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

            DebugHelper.WriteLine($"MacOSScreenService: {memberName} is not implemented yet.");
        }
    }
}
