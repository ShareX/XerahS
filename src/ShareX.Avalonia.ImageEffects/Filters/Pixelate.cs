using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects
{
    internal class Pixelate : ImageEffect
    {
        [DefaultValue(16)]
        public int Size { get; set; }

        [DefaultValue(0)]
        public int BorderSize { get; set; }

        // [DefaultValue(typeof(Color), "Transparent")]
        public SKColor BorderColor { get; set; }

        public Pixelate()
        {
            // this.ApplyDefaultPropertyValues();
            Size = 16;
            BorderColor = SKColors.Transparent;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             ImageEffectsProcessing.Pixelate(bmp, Size);
             return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{Size}, {BorderSize}";
        }
    }
}
