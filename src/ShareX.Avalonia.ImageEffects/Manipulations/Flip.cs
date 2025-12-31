using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Flip")]
    public class Flip : ImageEffect
    {
        [DefaultValue(false)]
        public bool Horizontal { get; set; }

        [DefaultValue(false)]
        public bool Vertical { get; set; }

        public Flip()
        {
            // this.ApplyDefaultPropertyValues();
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO: Flip implementation
            return bmp;
        }
    }
}
