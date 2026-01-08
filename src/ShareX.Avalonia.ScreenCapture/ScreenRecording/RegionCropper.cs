#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using System.Runtime.InteropServices;

namespace XerahS.ScreenCapture.ScreenRecording;

/// <summary>
/// Utility for cropping captured frames to a specific region
/// Stage 2: Window & Region Parity
/// </summary>
public static class RegionCropper
{
    /// <summary>
    /// Crops a frame to the specified region
    /// Returns a new FrameData with a separately allocated buffer containing the cropped region
    /// IMPORTANT: Caller must free the returned DataPtr when done
    /// </summary>
    /// <param name="sourceFrame">Source frame data (pointer-based)</param>
    /// <param name="region">Region to crop to</param>
    /// <returns>Cropped frame data with newly allocated buffer</returns>
    public static unsafe FrameData CropFrame(FrameData sourceFrame, Rectangle region)
    {
        // Validate region is within source frame bounds
        if (region.X < 0 || region.Y < 0 ||
            region.X + region.Width > sourceFrame.Width ||
            region.Y + region.Height > sourceFrame.Height)
        {
            throw new ArgumentException("Region is outside source frame bounds");
        }

        if (sourceFrame.DataPtr == IntPtr.Zero)
        {
            throw new ArgumentException("Source frame DataPtr is null");
        }

        // Calculate bytes per pixel based on format
        int bytesPerPixel = sourceFrame.Format switch
        {
            PixelFormat.Bgra32 => 4,
            PixelFormat.Rgba32 => 4,
            PixelFormat.Nv12 => throw new NotSupportedException("NV12 cropping not yet supported"),
            _ => throw new NotSupportedException($"Pixel format {sourceFrame.Format} not supported for cropping")
        };

        // Calculate cropped frame properties
        int croppedStride = region.Width * bytesPerPixel;
        int croppedBufferSize = croppedStride * region.Height;

        // Allocate new buffer for cropped data
        IntPtr croppedDataPtr = Marshal.AllocHGlobal(croppedBufferSize);

        try
        {
            byte* srcPtr = (byte*)sourceFrame.DataPtr;
            byte* dstPtr = (byte*)croppedDataPtr;

            // Copy row by row
            for (int y = 0; y < region.Height; y++)
            {
                int srcY = region.Y + y;
                int srcOffset = (srcY * sourceFrame.Stride) + (region.X * bytesPerPixel);
                int dstOffset = y * croppedStride;

                // Copy one row using native memory copy
                Buffer.MemoryCopy(
                    srcPtr + srcOffset,
                    dstPtr + dstOffset,
                    croppedStride,
                    croppedStride);
            }

            return new FrameData
            {
                DataPtr = croppedDataPtr,
                Width = region.Width,
                Height = region.Height,
                Stride = croppedStride,
                Timestamp = sourceFrame.Timestamp,
                Format = sourceFrame.Format
            };
        }
        catch
        {
            // Free allocated memory on error
            Marshal.FreeHGlobal(croppedDataPtr);
            throw;
        }
    }

    /// <summary>
    /// Frees memory allocated by CropFrame
    /// </summary>
    public static void FreeCroppedFrame(FrameData frame)
    {
        if (frame.DataPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(frame.DataPtr);
        }
    }

    /// <summary>
    /// Validates that a region is suitable for cropping
    /// </summary>
    public static bool IsValidRegion(Rectangle region, int sourceWidth, int sourceHeight)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return false;

        if (region.X < 0 || region.Y < 0)
            return false;

        if (region.X + region.Width > sourceWidth)
            return false;

        if (region.Y + region.Height > sourceHeight)
            return false;

        return true;
    }
}
