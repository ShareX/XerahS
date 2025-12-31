using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;
// using ShareX.Avalonia.Common.Extensions;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Rotate")]
    public class Rotate : ImageEffect
    {
        [DefaultValue(90f)]
        public float Angle { get; set; }

        [DefaultValue(true)]
        public bool Upsize { get; set; } = true;

        [DefaultValue(true)]
        public bool Clip { get; set; } = true;

        // [DefaultValue(typeof(Color), "Transparent")]
        public SKColor BackgroundColor { get; set; }

        public Rotate()
        {
            // this.ApplyDefaultPropertyValues();
            Angle = 90f;
            BackgroundColor = SKColors.Transparent;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Rotate implementation
             return bmp;
        }

        protected override string? GetSummary()
        {
            return Angle.ToString();
        }
    }
}
