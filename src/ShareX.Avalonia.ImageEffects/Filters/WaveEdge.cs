using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;
// using AnchorSides = ShareX.Avalonia.ImageEffects.Helpers.ImageEffectsProcessing.AnchorSides;

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Wave edge")]
    public class WaveEdge : ImageEffect
    {
        [DefaultValue(15)]
        public int Depth { get; set; }

        [DefaultValue(20)]
        public int Range { get; set; }

        // [DefaultValue(AnchorSides.All)]
        // public AnchorSides Sides { get; set; }

        public WaveEdge()
        {
            // this.ApplyDefaultPropertyValues();
            Depth = 15;
            Range = 20;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // return ImageEffectsProcessing.WavyEdges(bmp, Depth, Range, Sides, SKColors.Transparent);
            return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{Depth}, {Range}";
        }
    }
}
