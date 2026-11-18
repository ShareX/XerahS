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

namespace XerahS.Common;

/// <summary>
/// Cross-platform helper for rotating SKBitmap instances by a clockwise degree value.
/// Extracted from WindowsModernCaptureService to allow cross-platform unit testing.
/// See: https://github.com/ShareX/XerahS/issues/148 (black screen on rotated monitors)
/// </summary>
public static class BitmapRotationHelper
{
    /// <summary>
    /// Rotates <paramref name="source"/> clockwise by <paramref name="clockwiseDegrees"/>.
    /// Supported values: 0, 90, 180, 270. Any other value is treated as 0 (identity).
    /// Returns a new SKBitmap; the caller owns it and must dispose it.
    /// </summary>
    public static SKBitmap RotateClockwise(SKBitmap source, int clockwiseDegrees)
    {
        if (clockwiseDegrees == 0)
            return source.Copy();

        int targetWidth = source.Width;
        int targetHeight = source.Height;

        if (clockwiseDegrees == 90 || clockwiseDegrees == 270)
        {
            targetWidth = source.Height;
            targetHeight = source.Width;
        }

        // Preserve the source pixel format: an fp16 scRGB capture must stay fp16 through the
        // rotation, otherwise HDR highlights are silently clipped here.
        var result = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, source.ColorType,
            source.AlphaType, source.ColorSpace));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);

        switch (clockwiseDegrees)
        {
            case 90:
                // 90° CW: translate right-edge to origin, then rotate CW 90°.
                canvas.Translate(targetWidth, 0);
                canvas.RotateDegrees(90);
                break;
            case 180:
                canvas.Translate(targetWidth, targetHeight);
                canvas.RotateDegrees(180);
                break;
            case 270:
                // 270° CW (= 90° CCW): translate bottom-edge to origin, then rotate CCW 90°.
                canvas.Translate(0, targetHeight);
                canvas.RotateDegrees(-90);
                break;
        }

        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
        return result;
    }
}
