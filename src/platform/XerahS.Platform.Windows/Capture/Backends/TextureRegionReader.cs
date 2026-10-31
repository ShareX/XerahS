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
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace XerahS.Platform.Windows.Capture.Backends;

/// <summary>
/// Reads part of a BGRA desktop texture back into a Skia bitmap. The crop happens on the GPU, so only the
/// requested pixels cross the bus.
/// </summary>
internal static class TextureRegionReader
{
    /// <param name="device">Device that owns <paramref name="texture"/>.</param>
    /// <param name="texture">B8G8R8A8 texture in desktop orientation.</param>
    /// <param name="sourceRegion">Pixels to read, in texture coordinates.</param>
    /// <param name="destination">Bitmap that receives the pixels.</param>
    /// <param name="destinationX">Left edge of the pixels in <paramref name="destination"/>.</param>
    /// <param name="destinationY">Top edge of the pixels in <paramref name="destination"/>.</param>
    public static void CopyRegion(
        ID3D11Device device,
        ID3D11Texture2D texture,
        Rectangle sourceRegion,
        SKBitmap destination,
        int destinationX,
        int destinationY)
    {
        var stagingDesc = new Texture2DDescription
        {
            Width = (uint)sourceRegion.Width,
            Height = (uint)sourceRegion.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };

        var sourceBox = new Box(sourceRegion.Left, sourceRegion.Top, 0, sourceRegion.Right, sourceRegion.Bottom, 1);

        using var staging = device.CreateTexture2D(stagingDesc);
        device.ImmediateContext.CopySubresourceRegion(staging, 0, 0, 0, 0, texture, 0, sourceBox);

        var mapped = device.ImmediateContext.Map(staging, 0, MapMode.Read);
        try
        {
            IntPtr destinationPixels = destination.GetPixels() + (destinationY * destination.RowBytes) + (destinationX * 4);

            BgraRowCopyHelper.CopyRows(
                mapped.DataPointer,
                (int)mapped.RowPitch,
                destinationPixels,
                destination.RowBytes,
                sourceRegion.Width * 4,
                sourceRegion.Height);

            // Desktop surfaces are opaque; their alpha byte is undefined.
            BgraRowCopyHelper.SetOpaque(destinationPixels, destination.RowBytes, sourceRegion.Width, sourceRegion.Height);
        }
        finally
        {
            device.ImmediateContext.Unmap(staging, 0);
        }
    }
}
