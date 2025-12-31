using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects
{
    [Description("Color depth")]
    internal class ColorDepth : ImageEffect
    {
        private int bitsPerChannel;

        [DefaultValue(4)]
        public int BitsPerChannel
        {
            get
            {
                return bitsPerChannel;
            }
            set
            {
                bitsPerChannel = MathHelpers.Clamp(value, 1, 8);
            }
        }

        public ColorDepth()
        {
            // this.ApplyDefaultPropertyValues();
            BitsPerChannel = 4;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             ImageEffectsProcessing.ColorDepth(bmp, BitsPerChannel);
             return bmp;
        }

        protected override string? GetSummary()
        {
            string summary = BitsPerChannel + " bit";

            if (BitsPerChannel > 1)
            {
                summary += "s";
            }

            return summary;
        }
    }
}
