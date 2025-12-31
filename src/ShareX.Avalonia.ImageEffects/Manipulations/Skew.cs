using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Skew")]
    internal class Skew : ImageEffect
    {
        [DefaultValue(0f)]
        public float X { get; set; }

        [DefaultValue(0f)]
        public float Y { get; set; }

        // [DefaultValue(typeof(Color), "Transparent")]
        public SKColor BackgroundColor { get; set; }

        public Skew()
        {
            // this.ApplyDefaultPropertyValues();
            BackgroundColor = SKColors.Transparent;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO: Skew implementation
            return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{X}, {Y}";
        }
    }
}
