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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
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

        public bool UsePerScreenScalingForRegionCaptureLayout => true;

        public bool UseWindowPositionForRegionCaptureFallback => true;

        public bool UseLogicalCoordinatesForRegionCapture => true;

        public Rectangle GetVirtualScreenBounds()
        {
            var screens = TryGetScreens();
            if (screens == null || screens.ScreenCount == 0)
            {
                LogNotImplemented(nameof(GetVirtualScreenBounds));
                return Rectangle.Empty;
            }

            return CombineRectangles(GetScreenBounds(screens));
        }

        public Rectangle GetWorkingArea()
        {
            var screens = TryGetScreens();
            if (screens == null || screens.ScreenCount == 0)
            {
                LogNotImplemented(nameof(GetWorkingArea));
                return Rectangle.Empty;
            }

            return CombineRectangles(GetScreenWorkingAreas(screens));
        }

        public Rectangle GetActiveScreenBounds()
        {
            var screen = GetPrimaryScreen();
            if (screen == null)
            {
                LogNotImplemented(nameof(GetActiveScreenBounds));
                return Rectangle.Empty;
            }

            return ToDrawingRect(screen.Bounds);
        }

        public Rectangle GetActiveScreenWorkingArea()
        {
            var screen = GetPrimaryScreen();
            if (screen == null)
            {
                LogNotImplemented(nameof(GetActiveScreenWorkingArea));
                return Rectangle.Empty;
            }

            return ToDrawingRect(screen.WorkingArea);
        }

        public Rectangle GetPrimaryScreenBounds()
        {
            var screen = GetPrimaryScreen();
            if (screen == null)
            {
                LogNotImplemented(nameof(GetPrimaryScreenBounds));
                return Rectangle.Empty;
            }

            return ToDrawingRect(screen.Bounds);
        }

        public Rectangle GetPrimaryScreenWorkingArea()
        {
            var screen = GetPrimaryScreen();
            if (screen == null)
            {
                LogNotImplemented(nameof(GetPrimaryScreenWorkingArea));
                return Rectangle.Empty;
            }

            return ToDrawingRect(screen.WorkingArea);
        }

        public ScreenInfo[] GetAllScreens()
        {
            var screens = TryGetScreens();
            if (screens == null || screens.ScreenCount == 0)
            {
                LogNotImplemented(nameof(GetAllScreens));
                return Array.Empty<ScreenInfo>();
            }

            var screenList = new List<ScreenInfo>();
            foreach (var screen in screens.All)
            {
                screenList.Add(ToScreenInfo(screen));
            }

            return screenList.ToArray();
        }

        public ScreenInfo GetScreenFromPoint(System.Drawing.Point point)
        {
            var screens = TryGetScreens();
            if (screens == null || screens.ScreenCount == 0)
            {
                LogNotImplemented(nameof(GetScreenFromPoint));
                return CreateDefaultScreenInfo();
            }

            foreach (var screen in screens.All)
            {
                var bounds = ToDrawingRect(screen.Bounds);
                if (bounds.Contains(point))
                {
                    return ToScreenInfo(screen);
                }
            }

            var primary = GetPrimaryScreen();
            return primary == null ? CreateDefaultScreenInfo() : ToScreenInfo(primary);
        }

        public ScreenInfo GetScreenFromRectangle(System.Drawing.Rectangle rectangle)
        {
            var screens = TryGetScreens();
            if (screens == null || screens.ScreenCount == 0)
            {
                LogNotImplemented(nameof(GetScreenFromRectangle));
                return CreateDefaultScreenInfo();
            }

            Screen? bestScreen = null;
            long bestArea = -1;
            foreach (var screen in screens.All)
            {
                var bounds = ToDrawingRect(screen.Bounds);
                var intersection = Rectangle.Intersect(bounds, rectangle);
                long area = (long)intersection.Width * intersection.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestScreen = screen;
                }
            }

            if (bestScreen == null)
            {
                LogNotImplemented(nameof(GetScreenFromRectangle));
                return CreateDefaultScreenInfo();
            }

            return ToScreenInfo(bestScreen);
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

        private static Screens? TryGetScreens()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                return lifetime.MainWindow?.Screens;
            }

            return null;
        }

        private static Screen? GetPrimaryScreen()
        {
            var screens = TryGetScreens();
            if (screens == null || screens.ScreenCount == 0)
            {
                return null;
            }

            return screens.Primary;
        }

        private static Rectangle CombineRectangles(IEnumerable<Rectangle> rectangles)
        {
            var rectList = new List<Rectangle>(rectangles);
            if (rectList.Count == 0)
            {
                return Rectangle.Empty;
            }

            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            foreach (var rect in rectList)
            {
                left = Math.Min(left, rect.Left);
                top = Math.Min(top, rect.Top);
                right = Math.Max(right, rect.Right);
                bottom = Math.Max(bottom, rect.Bottom);
            }

            if (left == int.MaxValue || top == int.MaxValue)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(left, top, right - left, bottom - top);
        }

        private static IEnumerable<Rectangle> GetScreenBounds(Screens screens)
        {
            foreach (var screen in screens.All)
            {
                yield return ToDrawingRect(screen.Bounds);
            }
        }

        private static IEnumerable<Rectangle> GetScreenWorkingAreas(Screens screens)
        {
            foreach (var screen in screens.All)
            {
                yield return ToDrawingRect(screen.WorkingArea);
            }
        }

        private static Rectangle ToDrawingRect(PixelRect rect)
        {
            return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static ScreenInfo ToScreenInfo(Screen screen)
        {
            return new ScreenInfo
            {
                Bounds = ToDrawingRect(screen.Bounds),
                WorkingArea = ToDrawingRect(screen.WorkingArea),
                IsPrimary = screen.IsPrimary,
                DeviceName = screen.DisplayName ?? string.Empty,
                BitsPerPixel = 0
            };
        }
    }
}
