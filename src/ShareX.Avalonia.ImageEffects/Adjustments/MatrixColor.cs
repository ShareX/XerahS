using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;
// using System.Drawing.Imaging;

namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Color matrix")]
    public class MatrixColor : ImageEffect
    {
        [DefaultValue(1f)]
        public float Matrix00 { get; set; }
        [DefaultValue(0f)]
        public float Matrix01 { get; set; }
        [DefaultValue(0f)]
        public float Matrix02 { get; set; }
        [DefaultValue(0f)]
        public float Matrix03 { get; set; }
        [DefaultValue(0f)]
        public float Matrix04 { get; set; }

        [DefaultValue(0f)]
        public float Matrix10 { get; set; }
        [DefaultValue(1f)]
        public float Matrix11 { get; set; }
        [DefaultValue(0f)]
        public float Matrix12 { get; set; }
        [DefaultValue(0f)]
        public float Matrix13 { get; set; }
        [DefaultValue(0f)]
        public float Matrix14 { get; set; }

        [DefaultValue(0f)]
        public float Matrix20 { get; set; }
        [DefaultValue(0f)]
        public float Matrix21 { get; set; }
        [DefaultValue(1f)]
        public float Matrix22 { get; set; }
        [DefaultValue(0f)]
        public float Matrix23 { get; set; }
        [DefaultValue(0f)]
        public float Matrix24 { get; set; }

        [DefaultValue(0f)]
        public float Matrix30 { get; set; }
        [DefaultValue(0f)]
        public float Matrix31 { get; set; }
        [DefaultValue(0f)]
        public float Matrix32 { get; set; }
        [DefaultValue(1f)]
        public float Matrix33 { get; set; }
        [DefaultValue(0f)]
        public float Matrix34 { get; set; }

        [DefaultValue(0f)]
        public float Matrix40 { get; set; }
        [DefaultValue(0f)]
        public float Matrix41 { get; set; }
        [DefaultValue(0f)]
        public float Matrix42 { get; set; }
        [DefaultValue(0f)]
        public float Matrix43 { get; set; }
        [DefaultValue(1f)]
        public float Matrix44 { get; set; }

        public MatrixColor()
        {
            // this.ApplyDefaultPropertyValues();
            Matrix00 = 1f; Matrix11 = 1f; Matrix22 = 1f; Matrix33 = 1f; Matrix44 = 1f;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Apply color matrix
             return bmp;
        }
    }
}
