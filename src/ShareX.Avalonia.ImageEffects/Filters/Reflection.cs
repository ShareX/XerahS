using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects
{
    [Description("Reflection")]
    public class Reflection : ImageEffect
    {
        private int percentage;
        private int maxAlpha;
        private int minAlpha;

        [DefaultValue(20), Description("Reflection height size relative to screenshot height.\nValue need to be between 1 to 100.")]
        public int Percentage
        {
            get => percentage;
            set => percentage = MathHelpers.Clamp(value, 1, 100);
        }

        [DefaultValue(255), Description("Reflection transparency start from this value to MinAlpha.\nValue need to be between 0 to 255.")]
        public int MaxAlpha
        {
            get => maxAlpha;
            set => maxAlpha = MathHelpers.Clamp(value, 0, 255);
        }

        [DefaultValue(0), Description("Reflection transparency start from MaxAlpha to this value.\nValue need to be between 0 to 255.")]
        public int MinAlpha
        {
            get => minAlpha;
            set => minAlpha = MathHelpers.Clamp(value, 0, 255);
        }

        [DefaultValue(0), Description("Reflection start position will be: Screenshot height + Offset")]
        public int Offset { get; set; }

        [DefaultValue(false), Description("Adding skew to reflection from bottom left to bottom right.")]
        public bool Skew { get; set; }

        [DefaultValue(25), Description("How much pixel skew left to right.")]
        public int SkewSize { get; set; }

        public Reflection()
        {
            // this.ApplyDefaultPropertyValues();
            Percentage = 20;
            MaxAlpha = 255;
            MinAlpha = 0;
            SkewSize = 25;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // return ImageEffectsProcessing.DrawReflection(bmp, Percentage, MaxAlpha, MinAlpha, Offset, Skew, SkewSize);
            return bmp;
        }

        protected override string? GetSummary()
        {
            return Percentage.ToString();
        }
    }
}
