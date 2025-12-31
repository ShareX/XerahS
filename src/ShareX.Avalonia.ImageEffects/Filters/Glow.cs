using ShareX.Avalonia.Common;
using ShareX.Avalonia.Common.Colors;
using ShareX.Avalonia.ImageEffects.Helpers;
using System;
using System.ComponentModel;
using SkiaSharp;
// using ShareX.Avalonia.Common.Drawing; // Check if GradientInfo exists

namespace ShareX.Avalonia.ImageEffects
{
    [Description("Glow")]
    public class Glow : ImageEffect
    {
        private static readonly Random Random = new Random();
        private int size;
        private float strength;

        [DefaultValue(20)]
        public int Size
        {
            get => size;
            set => size = Math.Max(0, value);
        }

        [DefaultValue(1f)]
        public float Strength
        {
            get => strength;
            set => strength = Math.Max(0.1f, value);
        }

        // [DefaultValue(typeof(Color), "White")]
        public SKColor Color { get; set; }

        [DefaultValue(false)]
        public bool UseGradient { get; set; }

        // public GradientInfo Gradient { get; set; }

        // [DefaultValue(typeof(DrawingPoint), "0, 0")]
        public SKPoint Offset { get; set; }

        public Glow()
        {
            // this.ApplyDefaultPropertyValues();
            // Gradient = CreateDefaultGradient();
            Size = 20;
            Strength = 1f;
            Color = SKColors.White;
            Offset = new SKPoint(0,0);
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            // return ImageEffectsProcessing.AddGlow(bmp, Size, Strength, Color, Offset, UseGradient ? Gradient : null);
            return bmp;
        }

        protected override string? GetSummary()
        {
            return Size.ToString();
        }

        /*
        private static GradientInfo CreateDefaultGradient()
        {
            GradientInfo gradientInfo = new GradientInfo
            {
                Type = LinearGradientMode.ForwardDiagonal
            };

            switch (Random.Next(0, 3))
            {
                case 0:
                    gradientInfo.Colors.Add(new GradientStop(Color.FromArgb(0, 187, 138), 0f));
                    gradientInfo.Colors.Add(new GradientStop(Color.FromArgb(0, 105, 163), 100f));
                    break;
                case 1:
                    gradientInfo.Colors.Add(new GradientStop(Color.FromArgb(255, 3, 135), 0f));
                    gradientInfo.Colors.Add(new GradientStop(Color.FromArgb(255, 143, 3), 100f));
                    break;
                default:
                    gradientInfo.Colors.Add(new GradientStop(Color.FromArgb(184, 11, 195), 0f));
                    gradientInfo.Colors.Add(new GradientStop(Color.FromArgb(98, 54, 255), 100f));
                    break;
            }

            return gradientInfo;
        }
        */
    }
}
