using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Draw border")]
    public class DrawBorder : ImageEffect
    {
        [DefaultValue(1)]
        public int Size { get; set; }

        // [DefaultValue(typeof(Color), "Black")]
        public SKColor Color { get; set; }

        // [DefaultValue(typeof(Color), "Transparent")]
        public SKColor InsideColor { get; set; }

        [DefaultValue(0)]
        public int Offset { get; set; }

        [DefaultValue(false)]
        public bool UseCenterColor { get; set; }

        public DrawBorder()
        {
            // this.ApplyDefaultPropertyValues();
            Size = 1;
            Color = SKColors.Black;
            InsideColor = SKColors.Transparent;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Draw border
             return bmp;
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }
    }
}
