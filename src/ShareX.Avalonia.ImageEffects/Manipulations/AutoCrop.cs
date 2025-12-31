using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;
// using ShareX.Avalonia.Common.Drawing;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Auto crop")]
    public class AutoCrop : ImageEffect
    {
        [DefaultValue(10)]
        public int Margin { get; set; }

        // [DefaultValue(typeof(Color), "Transparent")]
        public SKColor Color { get; set; }

        [DefaultValue(10)]
        public int Tolerance { get; set; }

        public AutoCrop()
        {
            // this.ApplyDefaultPropertyValues();
            Margin = 10;
            Color = SKColors.Transparent;
            Tolerance = 10;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: AutoCrop implementation
             return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{Margin}, {Tolerance}";
        }
    }
}
