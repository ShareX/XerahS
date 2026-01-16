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

using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Basic screen enumeration using xrandr output. Provides best-effort data for multi-monitor setups.
/// </summary>
public sealed class LinuxScreenService : IScreenService
{
    private readonly List<ScreenInfo> _screens;

    public LinuxScreenService()
    {
        _screens = ParseScreens();
    }

    public bool UsePerScreenScalingForRegionCaptureLayout => false;
    public bool UseWindowPositionForRegionCaptureFallback => false;
    public bool UseLogicalCoordinatesForRegionCapture => false;

    public Rectangle GetVirtualScreenBounds()
    {
        if (_screens.Count == 0) return Rectangle.Empty;

        var left = _screens.Min(s => s.Bounds.Left);
        var top = _screens.Min(s => s.Bounds.Top);
        var right = _screens.Max(s => s.Bounds.Right);
        var bottom = _screens.Max(s => s.Bounds.Bottom);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public Rectangle GetWorkingArea() => GetVirtualScreenBounds();

    public Rectangle GetActiveScreenBounds()
    {
        var cursor = new LinuxInputService().GetCursorPosition();
        var screen = GetScreenFromPoint(cursor);
        return screen.Bounds;
    }

    public Rectangle GetActiveScreenWorkingArea()
    {
        var cursor = new LinuxInputService().GetCursorPosition();
        var screen = GetScreenFromPoint(cursor);
        return screen.WorkingArea;
    }

    public Rectangle GetPrimaryScreenBounds() => _screens.FirstOrDefault(s => s.IsPrimary)?.Bounds ?? Rectangle.Empty;

    public Rectangle GetPrimaryScreenWorkingArea() => _screens.FirstOrDefault(s => s.IsPrimary)?.WorkingArea ?? Rectangle.Empty;

    public ScreenInfo[] GetAllScreens() => _screens.ToArray();

    public ScreenInfo GetScreenFromPoint(Point point)
    {
        return _screens.FirstOrDefault(s => s.Bounds.Contains(point)) ?? _screens.FirstOrDefault() ?? new ScreenInfo();
    }

    public ScreenInfo GetScreenFromRectangle(Rectangle rectangle)
    {
        return _screens.FirstOrDefault(s => s.Bounds.IntersectsWith(rectangle)) ?? _screens.FirstOrDefault() ?? new ScreenInfo();
    }

    private static List<ScreenInfo> ParseScreens()
    {
        var screens = new List<ScreenInfo>();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xrandr",
                Arguments = "--current",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return screens;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            var regex = new Regex(@"^(?<name>\S+)\s+connected\s+(?<primary>primary\s+)?(?<width>\d+)x(?<height>\d+)\+(?<x>-?\d+)\+(?<y>-?\d+)", RegexOptions.Compiled | RegexOptions.Multiline);
            foreach (Match match in regex.Matches(output))
            {
                var name = match.Groups["name"].Value;
                var width = int.Parse(match.Groups["width"].Value);
                var height = int.Parse(match.Groups["height"].Value);
                var x = int.Parse(match.Groups["x"].Value);
                var y = int.Parse(match.Groups["y"].Value);
                var isPrimary = !string.IsNullOrEmpty(match.Groups["primary"].Value);

                var bounds = new Rectangle(x, y, width, height);
                screens.Add(new ScreenInfo
                {
                    DeviceName = name,
                    Bounds = bounds,
                    WorkingArea = bounds,
                    IsPrimary = isPrimary,
                    BitsPerPixel = 24,
                    ScaleFactor = 1.0
                });
            }
        }
        catch
        {
            // Ignore parsing failures; return whatever we gathered
        }

        if (screens.Count == 0)
        {
            // Fallback to a single 1920x1080 screen at origin
            screens.Add(new ScreenInfo
            {
                DeviceName = "Virtual",
                Bounds = new Rectangle(0, 0, 1920, 1080),
                WorkingArea = new Rectangle(0, 0, 1920, 1080),
                IsPrimary = true,
                BitsPerPixel = 24,
                ScaleFactor = 1.0
            });
        }

        return screens;
    }
}
