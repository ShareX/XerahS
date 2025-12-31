using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects
{
    internal class Emboss : ImageEffect
    {
        public override SKBitmap Apply(SKBitmap bmp)
        {
             // ConvolutionMatrixManager not migrated yet
             // return ConvolutionMatrixManager.Emboss().Apply(bmp);
             return bmp;
        }
    }
}
