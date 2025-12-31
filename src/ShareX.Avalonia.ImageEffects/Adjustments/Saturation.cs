using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Saturation")]
    internal class Saturation : ImageEffect
    {
        private float saturation;

        [DefaultValue(1f), Description("Value must be between 0.0 and 5.0.")]
        public float Value
        {
            get => saturation;
            set => saturation = MathHelpers.Clamp(value, 0.0f, 5.0f);
        }

        public Saturation()
        {
            // this.ApplyDefaultPropertyValues();
            Value = 1f;
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
