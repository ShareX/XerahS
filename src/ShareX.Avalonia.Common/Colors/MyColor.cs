using System.Drawing;

namespace XerahS.Common
{
    public struct MyColor
    {
        public RGBA RGBA;
        public HSB HSB;
        public CMYK CMYK;

        public bool IsTransparent
        {
            get
            {
                return RGBA.Alpha < 255;
            }
        }

        public MyColor(Color color)
        {
            RGBA = color;
            HSB = color;
            CMYK = color;
        }

        public static implicit operator MyColor(Color color)
        {
            return new MyColor(color);
        }

        public static implicit operator Color(MyColor color)
        {
            return color.RGBA;
        }

        public static bool operator ==(MyColor left, MyColor right)
        {
            return (left.RGBA == right.RGBA) && (left.HSB == right.HSB) && (left.CMYK == right.CMYK);
        }

        public static bool operator !=(MyColor left, MyColor right)
        {
            return !(left == right);
        }

        public void RGBAUpdate()
        {
            HSB = RGBA;
            CMYK = RGBA;
        }

        public void HSBUpdate()
        {
            RGBA = HSB;
            CMYK = HSB;
        }

        public void CMYKUpdate()
        {
            RGBA = CMYK;
            HSB = CMYK;
        }

        public override string ToString()
        {
            return string.Format(
@"RGBA (Red, Green, Blue, Alpha) = {0}, {1}, {2}, {3}
HSB (Hue, Saturation, Brightness) = {4:0.0}, {5:0.0}%, {6:0.0}%
CMYK (Cyan, Magenta, Yellow, Key) = {7:0.0}%, {8:0.0}%, {9:0.0}%, {10:0.0}%
Hex (RGB, RGBA, ARGB) = #{11}, #{12}, #{13}
Decimal (RGB, RGBA, ARGB) = {14}, {15}, {16}",
                RGBA.Red, RGBA.Green, RGBA.Blue, RGBA.Alpha,
                HSB.Hue360, HSB.Saturation100, HSB.Brightness100,
                CMYK.Cyan100, CMYK.Magenta100, CMYK.Yellow100, CMYK.Key100,
                ColorHelpers.ColorToHex(this), ColorHelpers.ColorToHex(this, ColorFormat.RGBA), ColorHelpers.ColorToHex(this, ColorFormat.ARGB),
                ColorHelpers.ColorToDecimal(this), ColorHelpers.ColorToDecimal(this, ColorFormat.RGBA), ColorHelpers.ColorToDecimal(this, ColorFormat.ARGB));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }
    }
}
