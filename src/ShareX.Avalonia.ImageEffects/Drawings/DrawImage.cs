using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Image watermark")]
    public class DrawImage : ImageEffect
    {
        [DefaultValue("")]
        public string ImagePath { get; set; }

        // [DefaultValue(ContentAlignment.BottomRight)]
        // public ContentAlignment Placement { get; set; }

        // [DefaultValue(typeof(DrawingPoint), "0, 0")]
        public SKPoint Offset { get; set; }

        [DefaultValue(false)]
        public bool AutoHide { get; set; }

        [DefaultValue(DrawImageSizeMode.DontResize)]
        public DrawImageSizeMode SizeMode { get; set; }

        [DefaultValue(20f)]
        public float ImageScale { get; set; }

        [DefaultValue(false)]
        public bool UseCenterColor { get; set; }

        // [DefaultValue(typeof(Color), "Transparent")]
        public SKColor CenterColor { get; set; }

        public DrawImage()
        {
            // this.ApplyDefaultPropertyValues();
            ImageScale = 20f;
            CenterColor = SKColors.Transparent;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO: Draw image watermark
            return bmp;
        }
    }
}
