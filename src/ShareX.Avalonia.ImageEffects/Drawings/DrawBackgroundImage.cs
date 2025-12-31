using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Draw background image")]
    public class DrawBackgroundImage : ImageEffect
    {
        [DefaultValue("")]
        public string ImagePath { get; set; }

        // [DefaultValue(ContentAlignment.MiddleCenter)]
        // public ContentAlignment Anchor { get; set; }

        [DefaultValue(typeof(CanvasMargin), "0, 0, 0, 0")]
        public CanvasMargin Margin { get; set; }

        [DefaultValue(DrawImageSizeMode.DontResize)]
        public DrawImageSizeMode SizeMode { get; set; }

        public DrawBackgroundImage()
        {
            // this.ApplyDefaultPropertyValues();
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Draw background image
             return bmp;
        }
    }
}
