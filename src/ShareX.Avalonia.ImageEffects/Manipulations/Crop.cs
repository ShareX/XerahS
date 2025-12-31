using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    internal class Crop : ImageEffect
    {
        [DefaultValue(0)]
        public int Left { get; set; }

        [DefaultValue(0)]
        public int Top { get; set; }

        [DefaultValue(0)]
        public int Right { get; set; }

        [DefaultValue(0)]
        public int Bottom { get; set; }

        public Crop()
        {
            // this.ApplyDefaultPropertyValues();
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            int width = bmp.Width - (Left + Right);
            int height = bmp.Height - (Top + Bottom);

            if (width <= 0 || height <= 0)
            {
                return bmp;
            }
            
            // SKRect rect = new SKRect(Left, Top, Left + width, Top + height);
            // return ImageEffectsProcessing.CropBitmap(bmp, rect);
            return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{Left}, {Top}, {Right}, {Bottom}";
        }
    }
}
