using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Slice")]
    public class Slice : ImageEffect
    {
        private int minSliceHeight;
        private int maxSliceHeight;

        [DefaultValue(10)]
        public int MinSliceHeight
        {
            get => minSliceHeight;
            set => minSliceHeight = Math.Max(value, 1);
        }

        [DefaultValue(100)]
        public int MaxSliceHeight
        {
            get => maxSliceHeight;
            set => maxSliceHeight = Math.Max(value, 1);
        }

        [DefaultValue(0)]
        public int MinSliceShift { get; set; }

        [DefaultValue(10)]
        public int MaxSliceShift { get; set; }

        public Slice()
        {
            // this.ApplyDefaultPropertyValues();
            MinSliceHeight = 10;
            MaxSliceHeight = 100;
            MinSliceShift = 0;
            MaxSliceShift = 10;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Skia slice implementation
             return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{MinSliceHeight}, {MaxSliceHeight}";
        }
    }
}
