using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects
{
    [Description("Outline")]
    public class Outline : ImageEffect
    {
        private int size;
        private int padding;

        [DefaultValue(1)]
        public int Size
        {
            get => size;
            set => size = Math.Max(value, 1);
        }

        [DefaultValue(0)]
        public int Padding
        {
            get => padding;
            set => padding = Math.Max(value, 0);
        }

        // [DefaultValue(typeof(Color), "Black")]
        public SKColor Color { get; set; }

        [DefaultValue(false)]
        public bool OutlineOnly { get; set; }

        public Outline()
        {
            // this.ApplyDefaultPropertyValues();
            Size = 1;
            Padding = 0;
            Color = SKColors.Black;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // return ImageEffectsProcessing.Outline(bmp, Size, Color, Padding, OutlineOnly);
            return bmp;
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }
    }
}
