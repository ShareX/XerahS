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
using SkiaSharp;

namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Options for region capture operations.
/// </summary>
public sealed record RegionCaptureOptions
{
    /// <summary>
    /// Image quality/compression level (0-100).
    /// Only applicable for lossy formats. Default: 95.
    /// </summary>
    public int Quality { get; init; } = 95;

    /// <summary>
    /// Whether to include the mouse cursor in the capture.
    /// Default: false.
    /// </summary>
    public bool IncludeCursor { get; init; } = false;

    /// <summary>
    /// Preferred color type for the captured bitmap.
    /// Default: Bgra8888 (32-bit with alpha).
    /// </summary>
    public SKColorType ColorType { get; init; } = SKColorType.Bgra8888;

    /// <summary>
    /// Whether to use GPU acceleration if available.
    /// Default: true.
    /// </summary>
    public bool UseHardwareAcceleration { get; init; } = true;

    /// <summary>
    /// Timeout for capture operation in milliseconds.
    /// Default: 5000 (5 seconds).
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Default options for most capture scenarios.
    /// </summary>
    public static RegionCaptureOptions Default { get; } = new();
}
