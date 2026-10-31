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
using ShareX.ImageEditor.Hosting;
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
    /// Draw the magnifier as a square instead of a circle. Default: false
    /// </summary>
    public bool UseSquareMagnifier { get; init; } = false;

    /// <summary>
    /// Odd number of source pixels shown in the magnifier. Default: 15
    /// </summary>
    public int MagnifierPixelCount { get; init; } = 15;

    /// <summary>
    /// Show X/Y (and sampled hex) under the magnifier. Default: true
    /// </summary>
    public bool ShowInfo { get; init; } = true;

    /// <summary>
    /// Custom HUD info text using pixel-info tokens ($x, $y, $r, $g, $b, $hex, $HEX, $n, ...).
    /// Null keeps the built-in "X/Y + hex" text.
    /// </summary>
    public string? CustomInfoFormat { get; init; }

    /// <summary>
    /// Draw crosshair lines across the whole screen through the cursor. When false only the
    /// short crosshair around the cursor is drawn. Default: true
    /// </summary>
    public bool ShowScreenCrosshair { get; init; } = true;

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

    /// <summary>Keep hovered windows dimmed; only an actual selection is undimmed.</summary>
    public bool DimAllExceptSelection { get; init; } = false;

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

    /// <summary>
    /// Color of the crosshair (near cursor, 32px). Default: Cyan (0xFF00C8FF)
    /// Format: ARGB (Alpha, Red, Green, Blue)
    /// </summary>
    public uint CrosshairColor { get; init; } = 0xDC00C8FF; // Semi-transparent cyan

    /// <summary>
    /// Color of the full-screen crosshair lines. Default: High-visibility yellow (0xC8FFFF00)
    /// Format: ARGB (Alpha, Red, Green, Blue)
    /// </summary>
    public uint CrosshairLineColor { get; init; } = 0xC8FFFF00; // Semi-transparent yellow

    /// <summary>
    /// Background image for magnifier pixel sampling. When provided, the magnifier
    /// will display actual pixel data instead of placeholder content.
    /// This should be captured before the overlay is displayed.
    /// </summary>
    public SkiaSharp.SKBitmap? BackgroundImage { get; init; } = null;

    /// <summary>
    /// When true, the overlay is transparent showing the live desktop behind it (RectangleTransparent workflow).
    /// When false, the overlay displays a frozen screenshot background (like original ShareX).
    /// Default: false (frozen screenshot background).
    /// Note: This is different from CaptureTransparent in CaptureOptions, which controls window capture transparency.
    /// </summary>
    public bool UseTransparentOverlay { get; init; } = false;

    /// <summary>
    /// When true, clicking completes the selection immediately (quick crop).
    /// When false, user must manually confirm selection (used for Ruler mode).
    /// Default: true
    /// </summary>
    public bool QuickCrop { get; init; } = true;

    /// <summary>
    /// When true, enumerate child controls and client rectangles for hover snap.
    /// Windows only. Default: true
    /// </summary>
    public bool DetectControls { get; init; } = true;

    /// <summary>
    /// Size presets that the drag rectangle snaps to when within <see cref="SnapDistance"/>.
    /// </summary>
    public IReadOnlyList<CaptureSnapSize> SnapSizes { get; init; } = CaptureSnapSize.DefaultPresets;

    /// <summary>
    /// Maximum Euclidean distance in physical pixels for size-preset snapping.
    /// </summary>
    public double SnapDistance { get; init; } = 30;

    /// <summary>
    /// When true, resize handles are rendered with lighter/simpler styling.
    /// Used for Ruler mode to reduce visual clutter.
    /// Default: false
    /// </summary>
    public bool UseLightResizeNodes { get; init; } = false;

    /// <summary>
    /// Editor options for persisting tool selection and styling preferences.
    /// These settings are saved between sessions.
    /// </summary>
    public ImageEditorOptions EditorOptions { get; init; } = new();

    /// <summary>
    /// When set, used to log elapsed ms at milestones (overlay shown, mouse down/up, etc.) for bottleneck diagnosis.
    /// Set by ScreenCaptureService at the start of region capture UI.
    /// </summary>
    public DateTime? SessionStartUtc { get; init; }
}
