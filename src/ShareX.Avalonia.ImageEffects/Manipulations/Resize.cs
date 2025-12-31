using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    public class Resize : ImageEffect
    {
        [DefaultValue(250), Description("Use width as 0 to automatically adjust width to maintain aspect ratio.")]
        public int Width { get; set; }

        [DefaultValue(0), Description("Use height as 0 to automatically adjust height to maintain aspect ratio.")]
        public int Height { get; set; }

        [DefaultValue(ResizeMode.ResizeAll)]
        public ResizeMode Mode { get; set; }

        public Resize()
        {
            // this.ApplyDefaultPropertyValues();
            Width = 250;
        }

        public Resize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            if (Width <= 0 && Height <= 0)
            {
                return bmp;
            }

            // TODO: Resize implementation
            return bmp;
        }

        protected override string? GetSummary()
        {
            return Width.ToString();
        }
    }
}
