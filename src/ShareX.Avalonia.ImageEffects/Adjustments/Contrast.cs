using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Contrast")]
    public class Contrast : ImageEffect
    {
        private int contrast;

        [DefaultValue(0), Description("Value must be between -100 and 100.")]
        public int Value
        {
            get => contrast;
            set => contrast = MathHelpers.Clamp(value, -100, 100);
        }

        public Contrast()
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
