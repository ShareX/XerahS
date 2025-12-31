using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Draw checkerboard")]
    internal class DrawCheckerboard : ImageEffect
    {
        [DefaultValue(10)]
        public int Size { get; set; }

        // [DefaultValue(typeof(Color), "LightGray")]
        public SKColor Color1 { get; set; }

        // [DefaultValue(typeof(Color), "White")]
        public SKColor Color2 { get; set; }

        public DrawCheckerboard()
        {
            // this.ApplyDefaultPropertyValues();
            Size = 10;
            Color1 = SKColors.LightGray;
            Color2 = SKColors.White;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Draw checkerboard
             return bmp;
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }
    }
}
