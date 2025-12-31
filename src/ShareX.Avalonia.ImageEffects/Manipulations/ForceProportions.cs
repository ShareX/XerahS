using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;
// using ShareX.Avalonia.ImageEffects.Enums;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Force proportions")]
    public class ForceProportions : ImageEffect
    {
        [DefaultValue(16f)]
        public float RatioX { get; set; }

        [DefaultValue(9f)]
        public float RatioY { get; set; }

        // [DefaultValue(typeof(Color), "Black")]
        public SKColor Color { get; set; }

        [DefaultValue(true)]
        public bool AutoResize { get; set; }

        // [DefaultValue(ContentAlignment.MiddleCenter)]
        // public ContentAlignment Anchor { get; set; }

        public ForceProportions()
        {
            // this.ApplyDefaultPropertyValues();
            RatioX = 16f;
            RatioY = 9f;
            Color = SKColors.Black;
            AutoResize = true;
            // Anchor = ContentAlignment.MiddleCenter;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Implementation
             return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{RatioX}:{RatioY}";
        }
    }
}
