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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkiaSharp;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Capture;

/// <summary>
/// Reads the current system cursor as an image with its screen position and hotspot.
/// </summary>
internal static class CursorImageCapture
{
    private const int DI_NORMAL = 0x0003;
    private const int DefaultCursorExtent = 32;

    public static CursorInfo? Capture()
    {
        try
        {
            var cursor = new CursorData();
            if (!cursor.IsVisible || cursor.Handle == IntPtr.Zero) return null;

            int width = cursor.Size.Width > 0 ? cursor.Size.Width : DefaultCursorExtent;
            int height = cursor.Size.Height > 0 ? cursor.Size.Height : DefaultCursorExtent;

            // Create a 32-bit ARGB bitmap for proper transparency
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);

                IntPtr hdc = graphics.GetHdc();
                try
                {
                    DrawIconEx(hdc, 0, 0, cursor.Handle, width, height, 0, IntPtr.Zero, DI_NORMAL);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Seek(0, SeekOrigin.Begin);
            var image = SKBitmap.Decode(stream);

            return new CursorInfo(image, cursor.Position, cursor.Hotspot);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);
}
