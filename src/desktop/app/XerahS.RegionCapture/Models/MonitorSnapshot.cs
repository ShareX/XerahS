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

namespace XerahS.RegionCapture.Models;

/// <summary>
/// Immutable snapshot of all connected monitors at a specific moment in time.
/// </summary>
public sealed class MonitorSnapshot
{
    /// <summary>
    /// Gets the timestamp when this snapshot was taken.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the collection of all monitors in this snapshot.
    /// </summary>
    public IReadOnlyList<MonitorInfo> Monitors { get; }

    /// <summary>
    /// Gets the virtual desktop bounds encompassing all monitors.
    /// </summary>
    public PixelRect VirtualDesktopBounds { get; }

    /// <summary>
    /// Gets the primary monitor, or null if none is marked as primary.
    /// </summary>
    public MonitorInfo? PrimaryMonitor => Monitors.FirstOrDefault(m => m.IsPrimary);

    /// <summary>
    /// Gets the total number of monitors.
    /// </summary>
    public int MonitorCount => Monitors.Count;

    public MonitorSnapshot(IReadOnlyList<MonitorInfo> monitors)
    {
        Timestamp = DateTime.Now;
        Monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        VirtualDesktopBounds = CalculateVirtualBounds(monitors);
    }

    private static PixelRect CalculateVirtualBounds(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
            return new PixelRect(0, 0, 0, 0);

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (var monitor in monitors)
        {
            minX = Math.Min(minX, monitor.PhysicalBounds.X);
            minY = Math.Min(minY, monitor.PhysicalBounds.Y);
            maxX = Math.Max(maxX, monitor.PhysicalBounds.X + monitor.PhysicalBounds.Width);
            maxY = Math.Max(maxY, monitor.PhysicalBounds.Y + monitor.PhysicalBounds.Height);
        }

        return new PixelRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Generates a formatted text report of all monitor properties.
    /// </summary>
    public string GenerateReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Monitor Diagnostic Report ===");
        sb.AppendLine($"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Monitor Count: {MonitorCount}");
        sb.AppendLine($"Virtual Desktop: {VirtualDesktopBounds.X}, {VirtualDesktopBounds.Y}, {VirtualDesktopBounds.Width}x{VirtualDesktopBounds.Height}");
        sb.AppendLine();

        for (int i = 0; i < Monitors.Count; i++)
        {
            var monitor = Monitors[i];
            sb.AppendLine($"Monitor {i + 1}: {monitor.DeviceName}");
            sb.AppendLine($"  Bounds: X={monitor.PhysicalBounds.X}, Y={monitor.PhysicalBounds.Y}, W={monitor.PhysicalBounds.Width}, H={monitor.PhysicalBounds.Height}");
            sb.AppendLine($"  Work Area: X={monitor.WorkArea.X}, Y={monitor.WorkArea.Y}, W={monitor.WorkArea.Width}, H={monitor.WorkArea.Height}");
            sb.AppendLine($"  Scale Factor: {monitor.ScaleFactor:F2}x");
            sb.AppendLine($"  DPI: {monitor.Dpi:F0}");
            sb.AppendLine($"  Primary: {(monitor.IsPrimary ? "Yes" : "No")}");
            sb.AppendLine($"  Orientation: {(monitor.PhysicalBounds.Width >= monitor.PhysicalBounds.Height ? "Landscape" : "Portrait")}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
