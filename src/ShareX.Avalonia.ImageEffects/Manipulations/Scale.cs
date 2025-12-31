using ShareX.Avalonia.Common;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Manipulations
{
    [Description("Scale")]
    public class Scale : ImageEffect
    {
        private float x;
        private float y;

        [DefaultValue(1f)]
        public float X
        {
            get => x;
            set => x = Math.Max(value, 0f);
        }

        [DefaultValue(1f)]
        public float Y
        {
            get => y;
            set => y = Math.Max(value, 0f);
        }

        public Scale()
        {
            // this.ApplyDefaultPropertyValues();
            X = 1f;
            Y = 1f;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            if (X == 1 && Y == 1)
            {
                return bmp;
            }

            // TODO: Scale implementation
            return bmp;
        }

        protected override string? GetSummary()
        {
            return $"{X}, {Y}";
        }
    }
}
