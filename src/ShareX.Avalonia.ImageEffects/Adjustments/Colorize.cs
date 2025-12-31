using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Colorize")]
    internal class Colorize : ImageEffect
    {
        // [DefaultValue(typeof(Color), "Red")]
        public SKColor Color { get; set; }

        public Colorize()
        {
            // this.ApplyDefaultPropertyValues();
            Color = SKColors.Red;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO
            return bmp;
        }
    }
}
