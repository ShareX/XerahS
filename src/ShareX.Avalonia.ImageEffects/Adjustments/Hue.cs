using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Hue")]
    internal class Hue : ImageEffect
    {
        private int hue;

        [DefaultValue(0), Description("Value must be between -180 and 180.")]
        public int Value
        {
            get => hue;
            set => hue = MathHelpers.Clamp(value, -180, 180);
        }

        public Hue()
        {
            // this.ApplyDefaultPropertyValues();
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO
             return bmp;
        }

        protected override string? GetSummary()
        {
            return Value.ToString();
        }
    }
}
