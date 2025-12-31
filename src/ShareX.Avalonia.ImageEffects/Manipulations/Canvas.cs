using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Canvas")]
    public class Canvas : ImageEffect
    {
        [DefaultValue(typeof(CanvasMargin), "0, 0, 0, 0")]
        public CanvasMargin Margin { get; set; }

        // [DefaultValue(typeof(Color), "White")]
        public SKColor Color { get; set; }

        public Canvas()
        {
            // this.ApplyDefaultPropertyValues();
            Color = SKColors.White;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Canvas resize implementation
             return bmp;
        }

        protected override string? GetSummary()
        {
            return Margin.ToString();
        }
    }
}
