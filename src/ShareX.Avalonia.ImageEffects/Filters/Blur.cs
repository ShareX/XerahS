using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects
{
    internal class Blur : ImageEffect
    {
        private int radius;

        [DefaultValue(15)]
        public int Radius
        {
            get
            {
                return radius;
            }
            set
            {
                radius = Math.Max(value, 3);
            }
        }

        public Blur()
        {
            // this.ApplyDefaultPropertyValues(); // Helper requires updating or removal
            Radius = 15;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            return ImageEffectsProcessing.ApplyBlur(bmp, Radius);
        }

        protected override string? GetSummary()
        {
            return Radius.ToString();
        }
    }
}
