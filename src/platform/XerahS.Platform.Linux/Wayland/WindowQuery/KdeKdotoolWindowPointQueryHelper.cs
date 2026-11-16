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

using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Wayland.WindowQuery;

internal sealed partial class KdeKdotoolWindowPointQueryHelper : IWaylandWindowPointQueryHelper
{
    [GeneratedRegex(@"^WINDOW=(?<window>.+)$", RegexOptions.Multiline)]
    private static partial Regex MouseLocationRegex();

    [GeneratedRegex(@"Position:\s*(?<x>-?\d+),(?<y>-?\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PositionRegex();

    [GeneratedRegex(@"Geometry:\s*(?<width>\d+)x(?<height>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GeometryRegex();

    // kdotool WINDOW ids are KWin hex handles (0x...) or braced UUIDs.
    // Reject anything else before interpolating into process arguments.
    [GeneratedRegex(@"^[A-Za-z0-9{}\-]{1,64}$")]
    private static partial Regex ValidWindowIdRegex();

    public WindowPointQueryCapability Capability { get; } =
        WaylandWindowPointQueryCommandRunner.CommandExists("kdotool")
            ? new WindowPointQueryCapability(WindowPointQuerySupportLevel.Full, null)
            : new WindowPointQueryCapability(
                WindowPointQuerySupportLevel.Unsupported,
                "Wayland session: install kdotool for KDE window snapping.");

    public WindowInfo? GetWindowAtPoint(Point logicalPoint)
    {
        if (!Capability.IsEnabled)
            return null;

        CommandRunResult mouseResult = WaylandWindowPointQueryCommandRunner.Run("kdotool", "getmouselocation --shell");
        if (!mouseResult.Success || !TryParseMouseLocationWindowId(mouseResult.StandardOutput, out string windowId))
            return null;

        CommandRunResult geometryResult = WaylandWindowPointQueryCommandRunner.Run("kdotool", $"getwindowgeometry \"{windowId}\"");
        if (!geometryResult.Success || !TryParseWindowGeometry(geometryResult.StandardOutput, out Rectangle bounds))
            return null;

        string title = ReadWindowProperty(windowId, "getwindowname");
        if (string.Equals(title, PlatformWindowTitles.RegionCaptureOverlay, StringComparison.Ordinal))
            return null;

        string className = ReadWindowProperty(windowId, "getwindowclassname");
        return new WindowInfo
        {
            Handle = (nint)windowId.GetHashCode(StringComparison.Ordinal),
            Title = title,
            ClassName = className,
            Bounds = bounds,
            IsVisible = true
        };
    }

    private static string ReadWindowProperty(string windowId, string command)
    {
        CommandRunResult result = WaylandWindowPointQueryCommandRunner.Run("kdotool", $"{command} \"{windowId}\"");
        return result.Success
            ? result.StandardOutput.Trim()
            : string.Empty;
    }

    internal static bool TryParseMouseLocationWindowId(string output, out string windowId)
    {
        windowId = string.Empty;
        if (string.IsNullOrWhiteSpace(output))
            return false;

        Match match = MouseLocationRegex().Match(output);
        if (!match.Success)
            return false;

        windowId = match.Groups["window"].Value.Trim();
        if (!ValidWindowIdRegex().IsMatch(windowId))
        {
            windowId = string.Empty;
            return false;
        }

        return true;
    }

    internal static bool TryParseWindowGeometry(string output, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (string.IsNullOrWhiteSpace(output))
            return false;

        if (TryParseShellGeometry(output, out bounds))
            return true;

        Match positionMatch = PositionRegex().Match(output);
        Match geometryMatch = GeometryRegex().Match(output);
        if (!positionMatch.Success || !geometryMatch.Success)
            return false;

        if (!TryParseInt32(positionMatch.Groups["x"].Value, out int x) ||
            !TryParseInt32(positionMatch.Groups["y"].Value, out int y) ||
            !TryParseInt32(geometryMatch.Groups["width"].Value, out int width) ||
            !TryParseInt32(geometryMatch.Groups["height"].Value, out int height))
        {
            return false;
        }

        bounds = new Rectangle(x, y, width, height);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static bool TryParseShellGeometry(string output, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;

        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            string key = line[..separatorIndex];
            string value = line[(separatorIndex + 1)..];
            if (TryParseInt32(value, out int numericValue))
            {
                values[key] = numericValue;
            }
        }

        if (!values.TryGetValue("X", out int x) ||
            !values.TryGetValue("Y", out int y) ||
            !values.TryGetValue("WIDTH", out int width) ||
            !values.TryGetValue("HEIGHT", out int height))
        {
            return false;
        }

        bounds = new Rectangle(x, y, width, height);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static bool TryParseInt32(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }
}
