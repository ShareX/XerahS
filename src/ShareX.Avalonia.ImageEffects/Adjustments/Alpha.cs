using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Adjustments
{
    [Description("Alpha")]
    public class Alpha : ImageEffect
    {
        private float alpha;

        [DefaultValue(1f)]
        public float Opacity
        {
            get => alpha;
            set => alpha = MathHelpers.Clamp(value, 0f, 1f);
        }

        [DefaultValue(false)]
        public bool SetAlpha { get; set; }

        public Alpha()
        {
            // this.ApplyDefaultPropertyValues();
            Opacity = 1f;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // TODO
            return bmp;
        }

        protected override string? GetSummary()
        {
            return Opacity.ToString();
        }
    }
}
