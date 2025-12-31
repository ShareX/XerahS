using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Gaussian blur")]
    internal class GaussianBlur : ImageEffect
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
                radius = Math.Max(value, 1);
            }
        }

        public GaussianBlur()
        {
             // this.ApplyDefaultPropertyValues();
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
