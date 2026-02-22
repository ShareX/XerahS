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
namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Describes the capabilities of a region capture backend.
/// Used for feature detection and UI adaptation.
/// </summary>
public sealed record BackendCapabilities
{
    /// <summary>
    /// Whether the backend supports hardware-accelerated capture.
    /// </summary>
    public bool SupportsHardwareAcceleration { get; init; }

    /// <summary>
    /// Whether the backend can capture the mouse cursor.
    /// </summary>
    public bool SupportsCursorCapture { get; init; }

    /// <summary>
    /// Whether the backend can capture HDR content (high dynamic range).
    /// </summary>
    public bool SupportsHDR { get; init; }

    /// <summary>
    /// Whether the backend supports per-monitor DPI awareness.
    /// </summary>
    public bool SupportsPerMonitorDpi { get; init; }

    /// <summary>
    /// Whether the backend can detect monitor configuration changes at runtime.
    /// </summary>
    public bool SupportsMonitorHotplug { get; init; }

    /// <summary>
    /// Maximum capture resolution (width or height).
    /// 0 means unlimited (system memory is the limit).
    /// </summary>
    public int MaxCaptureResolution { get; init; }

    /// <summary>
    /// Backend implementation name for diagnostics.
    /// Examples: "DXGI", "ScreenCaptureKit", "X11 XGetImage", "Wayland Portal"
    /// </summary>
    public required string BackendName { get; init; }

    /// <summary>
    /// Backend version or API level.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Whether this backend requires user permissions/approval for capture.
    /// Common on Wayland and macOS.
    /// </summary>
    public bool RequiresPermission { get; init; }

    public override string ToString() => $"{BackendName} {Version}".Trim();
}
