#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace XerahS.Common.Helpers
{
    public static class ClipboardHelpersEx
    {
        public static Bitmap ImageFromClipboardDib(byte[] data)
        {
            // BITMAPINFOHEADER struct
            // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader
            // 40 bytes

            if (data == null || data.Length < 40)
            {
                return null;
            }

            // data[0] = biSize
            // data[4] = biWidth
            // data[8] = biHeight
            // data[12] = biPlanes
            // data[14] = biBitCount
            // data[16] = biCompression
            // data[20] = biSizeImage
            // data[24] = biXPelsPerMeter
            // data[28] = biYPelsPerMeter
            // data[32] = biClrUsed
            // data[36] = biClrImportant

            int biSize = (int)ReadIntFromByteArray(data, 0, 4, true);
            int biWidth = (int)ReadIntFromByteArray(data, 4, 4, true);
            int biHeight = (int)ReadIntFromByteArray(data, 8, 4, true);
            short biPlanes = (short)ReadIntFromByteArray(data, 12, 2, true);
            short biBitCount = (short)ReadIntFromByteArray(data, 14, 2, true);
            int biCompression = (int)ReadIntFromByteArray(data, 16, 4, true);
            int biSizeImage = (int)ReadIntFromByteArray(data, 20, 4, true);
            // int biXPelsPerMeter = (int)ReadIntFromByteArray(data, 24, 4, true);
            // int biYPelsPerMeter = (int)ReadIntFromByteArray(data, 28, 4, true);
            int biClrUsed = (int)ReadIntFromByteArray(data, 32, 4, true);
            // int biClrImportant = (int)ReadIntFromByteArray(data, 36, 4, true);

            // 0 = BI_RGB
            // 3 = BI_BITFIELDS
            if (biCompression != 0 && biCompression != 3)
            {
                return null;
            }

            int headerSize = 40;

            if (biBitCount <= 8)
            {
                if (biClrUsed == 0)
                {
                    biClrUsed = 1 << biBitCount;
                }

                headerSize += biClrUsed * 4;
            }
            else if (biCompression == 3 && biBitCount > 8)
            {
                headerSize += 12; // 3 Color masks
            }

            if (data.Length < headerSize)
            {
                return null;
            }

            byte[] pixels = new byte[data.Length - headerSize];
            Array.Copy(data, headerSize, pixels, 0, pixels.Length);

            PixelFormat pixelFormat = PixelFormat.Undefined;

            switch (biBitCount)
            {
                case 32:
                    pixelFormat = PixelFormat.Format32bppRgb;
                    break;
                case 24:
                    pixelFormat = PixelFormat.Format24bppRgb;
                    break;
                case 8:
                    pixelFormat = PixelFormat.Format8bppIndexed;
                    break;
                case 4:
                    pixelFormat = PixelFormat.Format4bppIndexed;
                    break;
                case 1:
                    pixelFormat = PixelFormat.Format1bppIndexed;
                    break;
            }

            // TODO: Handle 16 bit images
            if (pixelFormat != PixelFormat.Undefined)
            {
                Bitmap bmp = new Bitmap(biWidth, biHeight, pixelFormat);

                // Initialize palette
                if (biBitCount <= 8)
                {
                    ColorPalette palette = bmp.Palette;

                    for (int i = 0; i < biClrUsed; i++)
                    {
                        int index = 40 + i * 4;
                        palette.Entries[i] = Color.FromArgb(data[index + 2], data[index + 1], data[index]);
                    }

                    bmp.Palette = palette;
                }

                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);

                int stride = bmpData.Stride;
                int dataStride = ((biWidth * biBitCount + 31) & ~31) / 8; // Padding to 4 bytes

                // Copy image data
                // DIB images are bottom-up
                for (int y = 0; y < biHeight; y++)
                {
                    int srcIndex = (biHeight - 1 - y) * dataStride;
                    int destIndex = y * stride;

                    if (srcIndex + dataStride <= pixels.Length) // Check bounds
                    {
                        Marshal.Copy(pixels, srcIndex, new IntPtr(bmpData.Scan0.ToInt64() + destIndex), Math.Min(dataStride, stride));
                    }
                }

                bmp.UnlockBits(bmpData);

                return bmp;
            }

            return null;
        }

        public static void WriteIntToByteArray(byte[] data, int startIndex, int bytes, bool littleEndian, uint value)
        {
            int lastByte = bytes - 1;
            if (data.Length < startIndex + bytes)
                throw new ArgumentOutOfRangeException("startIndex", "Data array is too small to write a " + bytes + "-byte value at offset " + startIndex + ".");
            for (int index = 0; index < bytes; index++)
            {
                int offs = startIndex + (littleEndian ? index : lastByte - index);
                data[offs] = (byte)(value >> (8 * index) & 0xFF);
            }
        }

        public static uint ReadIntFromByteArray(byte[] data, int startIndex, int bytes, bool littleEndian)
        {
            int lastByte = bytes - 1;
            if (data.Length < startIndex + bytes)
                throw new ArgumentOutOfRangeException("startIndex", "Data array is too small to read a " + bytes + "-byte value at offset " + startIndex + ".");
            uint value = 0;
            for (int index = 0; index < bytes; index++)
            {
                int offs = startIndex + (littleEndian ? index : lastByte - index);
                value += (uint)(data[offs] << (8 * index));
            }
            return value;
        }

        public static byte[] GetImageData(Bitmap sourceImage, out int stride)
        {
            BitmapData sourceData = sourceImage.LockBits(new Rectangle(0, 0, sourceImage.Width, sourceImage.Height), ImageLockMode.ReadOnly, sourceImage.PixelFormat);
            stride = sourceData.Stride;
            byte[] data = new byte[stride * sourceImage.Height];
            Marshal.Copy(sourceData.Scan0, data, 0, data.Length);
            sourceImage.UnlockBits(sourceData);
            return data;
        }

        public static Bitmap BuildImage(byte[] sourceData, int width, int height, int stride, PixelFormat pixelFormat, Color[] palette, Color? defaultColor)
        {
            Bitmap newImage = new Bitmap(width, height, pixelFormat);
            BitmapData targetData = newImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, newImage.PixelFormat);
            int newDataWidth = ((Image.GetPixelFormatSize(pixelFormat) * width) + 7) / 8;
            bool isFlipped = stride < 0;
            stride = Math.Abs(stride);
            int targetStride = targetData.Stride;
            long scan0 = targetData.Scan0.ToInt64();
            for (int y = 0; y < height; y++)
                Marshal.Copy(sourceData, y * stride, new IntPtr(scan0 + (y * targetStride)), newDataWidth);
            newImage.UnlockBits(targetData);
            if (isFlipped)
                newImage.RotateFlip(RotateFlipType.Rotate180FlipX);
            if ((pixelFormat & PixelFormat.Indexed) != 0 && palette != null)
            {
                ColorPalette pal = newImage.Palette;
                for (int i = 0; i < pal.Entries.Length; i++)
                {
                    if (i < palette.Length)
                        pal.Entries[i] = palette[i];
                    else if (defaultColor.HasValue)
                        pal.Entries[i] = defaultColor.Value;
                    else
                        break;
                }
                newImage.Palette = pal;
            }
            return newImage;
        }

        public static Bitmap CloneImage(Bitmap sourceImage)
        {
            // Simple clone using constructor if possible, but deep copy logic preserved for safety with GDI+ locking
            // However, native GDI+ Clone is usually sufficient. But reusing original logic for exact behavior.
            Rectangle rect = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
            Bitmap targetImage = new Bitmap(rect.Width, rect.Height, sourceImage.PixelFormat);
            targetImage.SetResolution(sourceImage.HorizontalResolution, sourceImage.VerticalResolution);
            BitmapData sourceData = sourceImage.LockBits(rect, ImageLockMode.ReadOnly, sourceImage.PixelFormat);
            BitmapData targetData = targetImage.LockBits(rect, ImageLockMode.WriteOnly, targetImage.PixelFormat);
            int actualDataWidth = ((Image.GetPixelFormatSize(sourceImage.PixelFormat) * rect.Width) + 7) / 8;
            int h = sourceImage.Height;
            int origStride = sourceData.Stride;
            bool isFlipped = origStride < 0;
            origStride = Math.Abs(origStride);
            int targetStride = targetData.Stride;
            byte[] imageData = new byte[actualDataWidth];
            IntPtr sourcePos = sourceData.Scan0;
            IntPtr destPos = targetData.Scan0;
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(sourcePos, imageData, 0, actualDataWidth);
                Marshal.Copy(imageData, 0, destPos, actualDataWidth);
                sourcePos = new IntPtr(sourcePos.ToInt64() + origStride);
                destPos = new IntPtr(destPos.ToInt64() + targetStride);
            }
            targetImage.UnlockBits(targetData);
            sourceImage.UnlockBits(sourceData);
            if (isFlipped)
                targetImage.RotateFlip(RotateFlipType.Rotate180FlipX);
            if ((sourceImage.PixelFormat & PixelFormat.Indexed) != 0)
                targetImage.Palette = sourceImage.Palette;
            return targetImage;
        }

        public static Bitmap DIBV5ToBitmap(byte[] data)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                BITMAPV5HEADER bmi = (BITMAPV5HEADER)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(BITMAPV5HEADER));
                int stride = -(int)(bmi.bV5SizeImage / bmi.bV5Height);
                long offset = bmi.bV5Size + ((bmi.bV5Height - 1) * (int)(bmi.bV5SizeImage / bmi.bV5Height));
                if (bmi.bV5Compression == (uint)NativeConstants.BI_BITFIELDS)
                {
                    offset += 12;
                }
                IntPtr scan0 = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + offset);
                // Creating bitmap from pointer requires the pointer to stay valid if we don't copy?
                // Actually new Bitmap(..., scan0) does NOT copy. So we must copy data or keep handle pinned.
                // Original code freed handle immediately which is DANGEROUS unless it copied. 
                // Wait, it returned 'bitmap'. If 'bitmap' wraps scan0, handle free is bad.
                // But typically we should clone it.
                // Note: The original returned bitmap.
                // I'll clone it to be safe or investigate.
                using (Bitmap temp = new Bitmap(bmi.bV5Width, bmi.bV5Height, stride, PixelFormat.Format32bppPArgb, scan0))
                {
                    return CloneImage(temp);
                }
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
