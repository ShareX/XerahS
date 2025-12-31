using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

using ShareX.Avalonia.Common.Colors;

namespace ShareX.Avalonia.ImageEffects
{
    internal class Shadow : ImageEffect
    {
        private float opacity;

        [DefaultValue(0.6f), Description("Choose a value between 0.1 and 1.0")]
        public float Opacity
        {
            get
            {
                return opacity;
            }
            set
            {
                opacity = Math.Clamp(value, 0.1f, 1.0f);
            }
        }

        private int size;

        [DefaultValue(10)]
        public int Size
        {
            get
            {
                return size;
            }
            set
            {
                size = Math.Max(value, 0);
            }
        }

        [DefaultValue(0f)]
        public float Darkness { get; set; }

        public SKColor Color { get; set; }

        public SKPoint Offset { get; set; }

        [DefaultValue(true)]
        public bool AutoResize { get; set; }

        public Shadow()
        {
            // this.ApplyDefaultPropertyValues();
            Opacity = 0.6f;
            Size = 10;
            Darkness = 0f;
            Color = SKColors.Black;
            Offset = new SKPoint(0, 0);
            AutoResize = true;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            return ImageEffectsProcessing.AddShadow(bmp, Opacity, Size, Darkness + 1, Color, Offset, AutoResize);
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }
    }
}
