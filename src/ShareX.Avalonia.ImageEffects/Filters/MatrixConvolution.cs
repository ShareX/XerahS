using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Convolution matrix")]
    internal class MatrixConvolution : ImageEffect
    {
        [DefaultValue(0)]
        public int X0Y0 { get; set; }
        [DefaultValue(0)]
        public int X1Y0 { get; set; }
        [DefaultValue(0)]
        public int X2Y0 { get; set; }

        [DefaultValue(0)]
        public int X0Y1 { get; set; }
        [DefaultValue(1)]
        public int X1Y1 { get; set; }
        [DefaultValue(0)]
        public int X2Y1 { get; set; }

        [DefaultValue(0)]
        public int X0Y2 { get; set; }
        [DefaultValue(0)]
        public int X1Y2 { get; set; }
        [DefaultValue(0)]
        public int X2Y2 { get; set; }

        [DefaultValue(1.0)]
        public double Factor { get; set; }

        [DefaultValue((byte)0)]
        public byte Offset { get; set; }

        public MatrixConvolution()
        {
            // this.ApplyDefaultPropertyValues();
            X1Y1 = 1;
            Factor = 1.0;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Implement Matrix Convolution using SKImageFilter.CreateMatrixConvolution
             return bmp;
        }
    }
}
