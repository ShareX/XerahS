using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Gamma")]
    internal class Gamma : ImageEffect
    {
        private float gamma;

        [DefaultValue(1f), Description("Value must be between 0.1 and 5.0.")]
        public float Value
        {
            get => gamma;
            set => gamma = MathHelpers.Clamp(value, 0.1f, 5.0f);
        }

        public Gamma()
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
