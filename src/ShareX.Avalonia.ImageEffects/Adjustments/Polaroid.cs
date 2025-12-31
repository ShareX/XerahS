using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Polaroid")]
    internal class Polaroid : ImageEffect
    {
        [DefaultValue(5)]
        public int Margin { get; set; }

        [DefaultValue(true)]
        public bool Rotate { get; set; }

        public Polaroid()
        {
            // this.ApplyDefaultPropertyValues();
            Margin = 5;
            Rotate = true;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO
            return bmp;
        }
    }
}
