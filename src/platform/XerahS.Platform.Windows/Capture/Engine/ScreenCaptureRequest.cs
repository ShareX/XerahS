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
using SkiaSharp;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Capture.Engine;

/// <summary>
/// The part of the desktop a capture request covers.
/// </summary>
internal enum CaptureArea
{
    /// <summary>Every monitor. The result spans the whole virtual desktop.</summary>
    VirtualDesktop,

    /// <summary>A caller-selected rectangle, clamped to the virtual desktop.</summary>
    Rectangle,

    /// <summary>A window's bounds, captured as-is even where they leave the virtual desktop.</summary>
    WindowBounds
}

/// <summary>
/// Immutable description of one screenshot. Backends read everything they need from here,
/// so the facade, the engine and the backends share no other state.
/// </summary>
internal sealed record ScreenCaptureRequest(CaptureArea Area, SKRect Bounds, CaptureOptions? Options)
{
    public static ScreenCaptureRequest ForVirtualDesktop(CaptureOptions? options) =>
        new(CaptureArea.VirtualDesktop, SKRect.Empty, options);

    public static ScreenCaptureRequest ForRectangle(SKRect rect, CaptureOptions? options) =>
        new(CaptureArea.Rectangle, rect, options);

    public static ScreenCaptureRequest ForWindowBounds(Rectangle bounds, CaptureOptions? options) =>
        new(CaptureArea.WindowBounds, new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Bottom), options);

    /// <summary>True only when the caller explicitly asked for the cursor.</summary>
    public bool DrawCursor => Options?.ShowCursor == true;

    /// <summary>True only when the caller explicitly asked for the cursor to be left out.</summary>
    public bool CursorExplicitlyExcluded => Options?.ShowCursor == false;

    public bool HdrColorCorrection => Options?.HDRScreenshotColorCorrection ?? true;

    public bool ModernCaptureRequested => CaptureBackendPolicy.ShouldUseModernCapture(Options);
}
