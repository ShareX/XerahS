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
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;

namespace XerahS.RegionCapture;

/// <summary>
/// High-level service for initiating region capture operations.
/// Provides industry-leading mixed-DPI handling using per-monitor overlays.
/// </summary>
public sealed class RegionCaptureService
{
    /// <summary>
    /// Configuration options for region capture.
    /// </summary>
    public RegionCaptureOptions Options { get; init; } = new();

    /// <summary>
    /// Initiates a region capture operation and returns the selected region in physical pixels.
    /// </summary>
    /// <returns>The captured region, or null if cancelled.</returns>
    /// <summary>
    /// Initiates a region capture operation and returns the selected region in physical pixels.
    /// </summary>
    /// <returns>The captured region, or null if cancelled.</returns>
    public async Task<RegionSelectionResult?> CaptureRegionAsync(XerahS.Platform.Abstractions.CursorInfo? initialCursor = null)
    {
        using var manager = new OverlayManager();
        return await manager.ShowOverlaysAsync(null, initialCursor, Options);
    }

    /// <summary>
    /// Initiates a region capture with a callback for real-time selection updates.
    /// </summary>
    public async Task<RegionSelectionResult?> CaptureRegionAsync(Action<PixelRect>? onSelectionChanged, XerahS.Platform.Abstractions.CursorInfo? initialCursor = null)
    {
        using var manager = new OverlayManager();
        return await manager.ShowOverlaysAsync(onSelectionChanged, initialCursor, Options);
    }
}

/// <summary>
/// Configuration options for region capture behavior.
/// </summary>
public sealed record RegionCaptureOptions
{
    /// <summary>
    /// Sets the capture mode (e.g., ScreenColorPicker).
    /// </summary>
    public RegionCaptureMode Mode { get; init; } = RegionCaptureMode.Default;

    /// <summary>
    /// Enable window snapping on hover. Default: true
    /// </summary>
    public bool EnableWindowSnapping { get; init; } = true;

    /// <summary>
    /// Enable magnifier for pixel-perfect precision. Default: true
    /// </summary>
    public bool EnableMagnifier { get; init; } = true;

    /// <summary>
    /// Magnifier zoom level. Default: 4x
    /// </summary>
    public int MagnifierZoom { get; init; } = 4;

    /// <summary>
    /// Enable keyboard arrow nudging of selection. Default: true
    /// </summary>
    public bool EnableKeyboardNudge { get; init; } = true;

    /// <summary>
    /// Dim overlay opacity (0.0-1.0). Default: 0.7
    /// </summary>
    public double DimOpacity { get; init; } = 0.7;

    /// <summary>
    /// Color of the selection border.
    /// </summary>
    public uint SelectionBorderColor { get; init; } = 0xFFFFFFFF; // White

    /// <summary>
    /// Color of the window snap highlight.
    /// </summary>
    public uint WindowSnapColor { get; init; } = 0xFF00AEFF; // Blue

    /// <summary>
    /// Whether to show the mouse cursor during selection. Default: false
    /// </summary>
    public bool ShowCursor { get; init; } = false;
}
