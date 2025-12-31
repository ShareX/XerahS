using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Draw background")]
    public class DrawBackground : ImageEffect
    {
        // [DefaultValue(typeof(Color), "White")]
        public SKColor Color { get; set; }

        [DefaultValue(false)]
        public bool UseGradient { get; set; }

        // public GradientInfo Gradient { get; set; }

        public DrawBackground()
        {
            // this.ApplyDefaultPropertyValues();
            Color = SKColors.White;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Background draw implementation
             return bmp;
        }

        protected override string? GetSummary()
        {
            return Color.ToString();
        }
    }
}
