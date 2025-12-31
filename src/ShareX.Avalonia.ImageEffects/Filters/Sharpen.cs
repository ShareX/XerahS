using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using SkiaSharp;
using System.ComponentModel;

namespace ShareX.Avalonia.ImageEffects
{
    internal class Sharpen : ImageEffect
    {
        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Matrix convolution shim
             return bmp;
        }
    }
}
