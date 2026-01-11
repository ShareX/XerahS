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

using SkiaSharp;
using System.Runtime.InteropServices;

namespace XerahS.Common.GIF
{
    /// <summary>
    /// Summary description for Class1.
    /// </summary>
    public abstract class Quantizer
    {
        /// <summary>
        /// Construct the quantizer
        /// </summary>
        /// <param name="singlePass">If true, the quantization only needs to loop through the source pixels once</param>
        /// <remarks>
        /// If you construct this class with a true value for singlePass, then the code will, when quantizing your image,
        /// only call the 'QuantizeImage' function. If two passes are required, the code will call 'InitialQuantizeImage'
        /// and then 'QuantizeImage'.
        /// </remarks>
        protected Quantizer(bool singlePass)
        {
            _singlePass = singlePass;
            _pixelSize = Marshal.SizeOf(typeof(Color32));
        }

        /// <summary>
        /// Quantize an image and return the resulting output bitmap
        /// </summary>
        /// <param name="source">The image to quantize</param>
        /// <returns>A quantized version of the image</returns>
        public SKBitmap Quantize(SKBitmap source)
        {
            // Get the size of the source image
            int height = source.Height;
            int width = source.Width;

            // Copy and ensure 32bpp
            // We force BGRA because Color32 struct expects that layout
            SKBitmap copy = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (SKCanvas canvas = new SKCanvas(copy))
            {
                canvas.DrawBitmap(source, 0, 0);
            }

            // Construct 8bpp output? Skia doesn't really have a "palette" bitmap type in the same way GDI+ does for manipulation.
            // SKColorType.Index8 is deprecated/removed in modern Skia.
            // However, typically GIF encoders take an index array and a palette.
            // The original code returns a 8bpp indexed Bitmap.
            // Here we might need to return a result that contains indices.
            // Since SkiaSharp doesn't support Index8 well, we will return an SKBitmap
            // BUT, the problem is we are simulating what System.Drawing did.

            // To properly support GIF logic which expects 8-bit indices:
            // We should probably just return the raw indices byte array and the palette.
            // BUT, for now, to keep signature similar (returning an object), we can return an 8-bit grayscale SKBitmap
            // where pixel values are indices? SKColorType.Gray8 exists.

            SKBitmap output = new SKBitmap(width, height, SKColorType.Gray8, SKAlphaType.Unpremul);

            // Access pixels
            IntPtr sourcePixels = copy.GetPixels();
            IntPtr outputPixels = output.GetPixels();

            try
            {
                // First pass
                if (!_singlePass)
                    FirstPass(sourcePixels, width, height, copy.RowBytes);

                // Get Palette - abstract method
                // We need to store this palette somewhere. 
                // The original code set output.Palette. 
                // Skia bitmaps don't carry a palette.
                // We will attach it as a property or return a wrapper?
                // For this port, we'll store it in a public property that the caller (SaveGIF) can access.

                _palette = GetPalette();

                // Second pass
                SecondPass(sourcePixels, outputPixels, width, height, copy.RowBytes, output.RowBytes);
            }
            finally
            {
                copy.Dispose();
            }

            return output;
        }

        // We add this to store the result palette
        private List<SKColor> _palette;
        public List<SKColor> ResultPalette => _palette;

        /// <summary>
        /// Execute the first pass through the pixels in the image
        /// </summary>
        protected virtual void FirstPass(IntPtr sourcePixels, int width, int height, int stride)
        {
            IntPtr pSourceRow = sourcePixels;

            for (int row = 0; row < height; row++)
            {
                IntPtr pSourcePixel = pSourceRow;

                for (int col = 0; col < width; col++)
                {
                    InitialQuantizePixel(new Color32(pSourcePixel));
                    pSourcePixel = (IntPtr)((long)pSourcePixel + _pixelSize);
                }

                pSourceRow = (IntPtr)((long)pSourceRow + stride);
            }
        }

        /// <summary>
        /// Execute a second pass through the bitmap
        /// </summary>
        protected virtual void SecondPass(IntPtr sourcePixels, IntPtr outputPixels, int width, int height, int sourceStride, int outputStride)
        {
            IntPtr pSourceRow = sourcePixels;
            IntPtr pDestinationRow = outputPixels;
            IntPtr pPreviousPixel = pSourceRow;

            // Convert first pixel
            byte pixelValue = QuantizePixel(new Color32(pSourceRow));
            Marshal.WriteByte(pDestinationRow, pixelValue);

            for (int row = 0; row < height; row++)
            {
                IntPtr pSourcePixel = pSourceRow;
                IntPtr pDestinationPixel = pDestinationRow;

                for (int col = 0; col < width; col++)
                {
                    if (Marshal.ReadInt32(pPreviousPixel) != Marshal.ReadInt32(pSourcePixel))
                    {
                        pixelValue = QuantizePixel(new Color32(pSourcePixel));
                        pPreviousPixel = pSourcePixel;
                    }

                    Marshal.WriteByte(pDestinationPixel, pixelValue);

                    pSourcePixel = (IntPtr)((long)pSourcePixel + _pixelSize);
                    pDestinationPixel = (IntPtr)((long)pDestinationPixel + 1);
                }

                pSourceRow = (IntPtr)((long)pSourceRow + sourceStride);
                pDestinationRow = (IntPtr)((long)pDestinationRow + outputStride);
            }
        }

        protected virtual void InitialQuantizePixel(Color32 pixel)
        {
        }

        protected abstract byte QuantizePixel(Color32 pixel);

        protected abstract List<SKColor> GetPalette();

        private bool _singlePass;
        private int _pixelSize;

        [StructLayout(LayoutKind.Explicit)]
        public struct Color32
        {
            public Color32(IntPtr pSourcePixel)
            {
                this = (Color32)Marshal.PtrToStructure(pSourcePixel, typeof(Color32));
            }

            [FieldOffset(0)]
            public byte Blue;
            [FieldOffset(1)]
            public byte Green;
            [FieldOffset(2)]
            public byte Red;
            [FieldOffset(3)]
            public byte Alpha;

            [FieldOffset(0)]
            public int ARGB;

            public SKColor Color
            {
                get
                {
                    return new SKColor(Red, Green, Blue, Alpha);
                }
            }
        }
    }
}
