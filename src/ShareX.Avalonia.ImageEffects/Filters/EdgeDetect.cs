using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Edge detect")]
    internal class EdgeDetect : ImageEffect
    {
        public override SKBitmap Apply(SKBitmap bmp)
        {
            // ConvolutionMatrixManager not migrated yet
            // return ConvolutionMatrixManager.EdgeDetect().Apply(bmp);
            return bmp;
        }
    }
}
