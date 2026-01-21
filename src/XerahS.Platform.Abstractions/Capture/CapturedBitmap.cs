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
using System;

namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Represents a captured bitmap with metadata about the capture.
/// </summary>
public sealed class CapturedBitmap : IDisposable
{
    /// <summary>
    /// The captured bitmap data.
    /// </summary>
    public SKBitmap Bitmap { get; }

    /// <summary>
    /// The physical region that was captured.
    /// </summary>
    public PhysicalRectangle Region { get; }

    /// <summary>
    /// The DPI scale factor of the monitor where the capture occurred.
    /// </summary>
    public double ScaleFactor { get; }

    /// <summary>
    /// Timestamp when the capture occurred.
    /// </summary>
    public DateTime CapturedAt { get; }

    public CapturedBitmap(SKBitmap bitmap, PhysicalRectangle region, double scaleFactor)
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        Region = region;
        ScaleFactor = scaleFactor;
        CapturedAt = DateTime.UtcNow;
    }

    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public SKColorType ColorType => Bitmap.ColorType;

    public void Dispose()
    {
        Bitmap?.Dispose();
    }
}
