using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Brightness")]
    internal class Brightness : ImageEffect
    {
        private int brightness;

        [DefaultValue(0), Description("Value must be between -255 and 255.")]
        public int Value
        {
            get => brightness;
            set => brightness = MathHelpers.Clamp(value, -255, 255);
        }

        public Brightness()
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
