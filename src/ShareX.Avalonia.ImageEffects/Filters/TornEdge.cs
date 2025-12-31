using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;
// using AnchorSides = ShareX.Avalonia.ImageEffects.Helpers.ImgeEffectsProcessing.AnchorSides; // Need to verify if AnchorSides was ported

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Torn edge")]
    public class TornEdge : ImageEffect
    {
        [DefaultValue(15)]
        public int Depth { get; set; }

        [DefaultValue(20)]
        public int Range { get; set; }

        // [DefaultValue(AnchorSides.All)]
        // public AnchorSides Sides { get; set; }

        [DefaultValue(true)]
        public bool CurvedEdges { get; set; }

        public TornEdge()
        {
            // this.ApplyDefaultPropertyValues();
            Depth = 15;
            Range = 20;
            CurvedEdges = true;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // return ImageEffectsProcessing.TornEdges(bmp, Depth, Range, Sides, CurvedEdges, true, SKColors.Transparent);
             return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{Depth}, {Range}";
        }
    }
}
