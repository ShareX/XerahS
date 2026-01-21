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
/// Represents information about a physical monitor including DPI scaling.
/// </summary>
public sealed record MonitorInfo(
    string DeviceName,
    PixelRect PhysicalBounds,
    PixelRect WorkArea,
    double ScaleFactor,
    bool IsPrimary)
{
    /// <summary>
    /// Gets the DPI value for this monitor (96 * ScaleFactor).
    /// </summary>
    public double Dpi => 96.0 * ScaleFactor;

    /// <summary>
    /// Converts physical pixels to logical (DIPs) for this monitor.
    /// </summary>
    public double PhysicalToLogical(double physical) => physical / ScaleFactor;

    /// <summary>
    /// Converts logical (DIPs) to physical pixels for this monitor.
    /// </summary>
    public double LogicalToPhysical(double logical) => logical * ScaleFactor;

    /// <summary>
    /// Converts a physical point to logical coordinates relative to this monitor.
    /// </summary>
    public PixelPoint PhysicalToLogical(PixelPoint physical) =>
        new((physical.X - PhysicalBounds.X) / ScaleFactor,
            (physical.Y - PhysicalBounds.Y) / ScaleFactor);

    /// <summary>
    /// Converts a logical point (relative to this monitor) to physical coordinates.
    /// </summary>
    public PixelPoint LogicalToPhysical(PixelPoint logical) =>
        new(logical.X * ScaleFactor + PhysicalBounds.X,
            logical.Y * ScaleFactor + PhysicalBounds.Y);
}
