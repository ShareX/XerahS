using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects
{
    [Description("RGB split")]
    public class RGBSplit : ImageEffect
    {
        // [DefaultValue(typeof(DrawingPoint), "-5, 0")]
        public SKPoint OffsetRed { get; set; } = new SKPoint(-5, 0);

        // [DefaultValue(typeof(DrawingPoint), "0, 0")]
        public SKPoint OffsetGreen { get; set; }

        // [DefaultValue(typeof(DrawingPoint), "5, 0")]
        public SKPoint OffsetBlue { get; set; } = new SKPoint(5, 0);

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO: Skia implementation of channel split
            return bmp;
        }
    }
}
