using System;

namespace XerahS.Platform.Windows.Capture;

/// <summary>
/// Copies tightly-addressable BGRA rows from a source buffer into a destination buffer.
/// Handles padded source rows and optional vertical flipping without reading padding bytes.
/// </summary>
internal static class BgraRowCopyHelper
{
    public static unsafe void CopyRows(
        IntPtr sourceBase,
        int sourceStride,
        IntPtr destinationBase,
        int destinationStride,
        int bytesPerRow,
        int height,
        bool flipVertically = false)
    {
        if (sourceBase == IntPtr.Zero) throw new ArgumentNullException(nameof(sourceBase));
        if (destinationBase == IntPtr.Zero) throw new ArgumentNullException(nameof(destinationBase));
        if (bytesPerRow <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerRow));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (height == 0) return;

        int normalizedSourceStride = ValidateStride(sourceStride, bytesPerRow, nameof(sourceStride));
        int normalizedDestinationStride = ValidateStride(destinationStride, bytesPerRow, nameof(destinationStride));

        byte* srcTopRow = NormalizeTopRow((byte*)sourceBase.ToPointer(), sourceStride, height);
        byte* dstTopRow = NormalizeTopRow((byte*)destinationBase.ToPointer(), destinationStride, height);

        for (int y = 0; y < height; y++)
        {
            int sourceRowIndex = flipVertically ? height - 1 - y : y;
            byte* srcRow = srcTopRow + (sourceRowIndex * normalizedSourceStride);
            byte* dstRow = dstTopRow + (y * normalizedDestinationStride);
            Buffer.MemoryCopy(srcRow, dstRow, normalizedDestinationStride, bytesPerRow);
        }
    }

    /// <summary>
    /// Forces the alpha byte of every BGRA pixel to 0xFF. Screen surfaces are opaque, but GDI and
    /// Desktop Duplication leave the alpha byte undefined (often zero).
    /// </summary>
    public static unsafe void SetOpaque(IntPtr destinationBase, int destinationStride, int widthInPixels, int height)
    {
        if (destinationBase == IntPtr.Zero) throw new ArgumentNullException(nameof(destinationBase));
        if (widthInPixels < 0) throw new ArgumentOutOfRangeException(nameof(widthInPixels));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (widthInPixels == 0 || height == 0) return;

        int normalizedStride = ValidateStride(destinationStride, widthInPixels * 4, nameof(destinationStride));
        byte* topRow = NormalizeTopRow((byte*)destinationBase.ToPointer(), destinationStride, height);

        for (int y = 0; y < height; y++)
        {
            uint* pixel = (uint*)(topRow + (y * normalizedStride));
            for (int x = 0; x < widthInPixels; x++)
            {
                pixel[x] |= 0xFF000000u;
            }
        }
    }

    private static unsafe byte* NormalizeTopRow(byte* basePointer, int stride, int height)
    {
        if (stride >= 0)
        {
            return basePointer;
        }

        return basePointer + ((height - 1) * stride);
    }

    private static int ValidateStride(int stride, int bytesPerRow, string paramName)
    {
        int absoluteStride = Math.Abs(stride);
        if (absoluteStride < bytesPerRow)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                $"{paramName} must be at least {bytesPerRow} bytes to copy one full pixel row.");
        }

        return absoluteStride;
    }
}
