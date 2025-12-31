using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Rounded corners")]
    internal class RoundedCorners : ImageEffect
    {
        [DefaultValue(20)]
        public int CornerRadius { get; set; }

        [DefaultValue(false)]
        public bool RoundTopLeft { get; set; }

        [DefaultValue(false)]
        public bool RoundTopRight { get; set; }

        [DefaultValue(false)]
        public bool RoundBottomLeft { get; set; }

        [DefaultValue(false)]
        public bool RoundBottomRight { get; set; }

        // [DefaultValue(typeof(Color), "Transparent")]
        public SKColor BackgroundColor { get; set; }

        public RoundedCorners()
        {
            // this.ApplyDefaultPropertyValues();
            CornerRadius = 20;
            BackgroundColor = SKColors.Transparent;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO: Rounded corners implementation
            return bmp;
        }

        protected override string? GetSummary()
        {
            return CornerRadius.ToString();
        }
    }
}
