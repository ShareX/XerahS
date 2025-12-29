#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using ShareX.Avalonia.Common;

namespace ShareX.Avalonia.Common.Helpers
{
    public static class ColorHelpers
    {
        public static Color[] StandardColors = new Color[]
        {
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(64, 64, 64),
            Color.FromArgb(255, 0, 0),
            Color.FromArgb(255, 106, 0),
            Color.FromArgb(255, 216, 0),
            Color.FromArgb(182, 255, 0),
            Color.FromArgb(76, 255, 0),
            Color.FromArgb(0, 255, 33),
            Color.FromArgb(0, 255, 144),
            Color.FromArgb(0, 255, 255),
            Color.FromArgb(0, 148, 255),
            Color.FromArgb(0, 38, 255),
            Color.FromArgb(72, 0, 255),
            Color.FromArgb(178, 0, 255),
            Color.FromArgb(255, 0, 220),
            Color.FromArgb(255, 0, 110),
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(128, 128, 128),
            Color.FromArgb(127, 0, 0),
            Color.FromArgb(127, 51, 0),
            Color.FromArgb(127, 106, 0),
            Color.FromArgb(91, 127, 0),
            Color.FromArgb(38, 127, 0),
            Color.FromArgb(0, 127, 14),
            Color.FromArgb(0, 127, 70),
            Color.FromArgb(0, 127, 127),
            Color.FromArgb(0, 74, 127),
            Color.FromArgb(0, 19, 127),
            Color.FromArgb(33, 0, 127),
            Color.FromArgb(87, 0, 127),
            Color.FromArgb(127, 0, 110),
            Color.FromArgb(127, 0, 55)
        };

        public static string ColorToHex(Color color, ColorFormat format = ColorFormat.RGB)
        {
            switch (format)
            {
                default:
                case ColorFormat.RGB:
                    return string.Format("{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
                case ColorFormat.RGBA:
                    return string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", color.R, color.G, color.B, color.A);
                case ColorFormat.ARGB:
                    return string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
            }
        }

        public static int ColorToDecimal(Color color, ColorFormat format = ColorFormat.RGB)
        {
            switch (format)
            {
                default:
                case ColorFormat.RGB:
                    return color.R << 16 | color.G << 8 | color.B;
                case ColorFormat.RGBA:
                    return color.R << 24 | color.G << 16 | color.B << 8 | color.A;
                case ColorFormat.ARGB:
                    return color.A << 24 | color.R << 16 | color.G << 8 | color.B;
            }
        }

        public static HSB ColorToHSB(Color color)
        {
            HSB hsb = new HSB();

            int max;
            int min;

            if (color.R > color.G)
            {
                max = color.R;
                min = color.G;
            }
            else
            {
                max = color.G;
                min = color.R;
            }

            if (color.B > max)
            {
                max = color.B;
            }
            else if (color.B < min)
            {
                min = color.B;
            }

            hsb.Hue = GetHue(color);
            hsb.Saturation = max == 0 ? 0 : 1f - (1f * min / max);
            hsb.Brightness = max / 255f;

            return hsb;
        }

        public static CMYK ColorToCMYK(Color color)
        {
            float red = color.R / 255f;
            float green = color.G / 255f;
            float blue = color.B / 255f;

            float black = Math.Min(1 - red, Math.Min(1 - green, 1 - blue));

            CMYK cmyk = new CMYK
            {
                K = black,
                C = (1 - red - black) / (1 - black),
                M = (1 - green - black) / (1 - black),
                Y = (1 - blue - black) / (1 - black)
            };

            return cmyk;
        }

        public static string ColorToRGBString(Color color)
        {
            return string.Format("{0}, {1}, {2}", color.R, color.G, color.B);
        }

        public static string ColorToRGBAString(Color color)
        {
            return string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);
        }

        public static string ColorToHSBString(Color color)
        {
            HSB hsb = ColorToHSB(color);
            return string.Format("{0}, {1}%, {2}%", Math.Round(hsb.Hue), Math.Round(hsb.Saturation * 100), Math.Round(hsb.Brightness * 100));
        }

        public static string ColorToCMYKString(Color color)
        {
            CMYK cmyk = ColorToCMYK(color);
            return string.Format("{0}%, {1}%, {2}%, {3}%", Math.Round(cmyk.C * 100), Math.Round(cmyk.M * 100), Math.Round(cmyk.Y * 100), Math.Round(cmyk.K * 100));
        }

        public static Color HexToColor(string hex, ColorFormat format = ColorFormat.RGB)
        {
            if (string.IsNullOrEmpty(hex))
            {
                throw new ArgumentException(nameof(hex));
            }

            hex = Regex.Replace(hex, "[^a-fA-F0-9]", string.Empty);

            if (((format == ColorFormat.RGBA || format == ColorFormat.ARGB) && hex.Length != 8) ||
                (format == ColorFormat.RGB && hex.Length != 6))
            {
                throw new ArgumentException("Color must be in hex format.", nameof(hex));
            }

            int r = int.Parse(hex.Substring(format == ColorFormat.ARGB ? 2 : 0, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(format == ColorFormat.ARGB ? 4 : 2, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(format == ColorFormat.ARGB ? 6 : 4, 2), System.Globalization.NumberStyles.HexNumber);

            int a = 255;
            if (format == ColorFormat.RGBA)
            {
                a = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            }
            else if (format == ColorFormat.ARGB)
            {
                a = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            }

            return Color.FromArgb(a, r, g, b);
        }

        public static Color DecimalToColor(int dec, ColorFormat format = ColorFormat.RGB)
        {
            int r = (int)((dec >> 16) & 0xFF);
            int g = (int)((dec >> 8) & 0xFF);
            int b = (int)(dec & 0xFF);
            int a = 255;

            if (format == ColorFormat.RGBA)
            {
                a = (int)(dec & 0xFF);
                b = (int)((dec >> 8) & 0xFF);
                g = (int)((dec >> 16) & 0xFF);
                r = (int)((dec >> 24) & 0xFF);
            }
            else if (format == ColorFormat.ARGB)
            {
                a = (int)((dec >> 24) & 0xFF);
                r = (int)((dec >> 16) & 0xFF);
                g = (int)((dec >> 8) & 0xFF);
                b = (int)(dec & 0xFF);
            }

            return Color.FromArgb(a, r, g, b);
        }

        public static Color HSBTColor(HSB hsb)
        {
            while (hsb.Hue < 0)
            {
                hsb.Hue += 360;
            }

            hsb.Hue %= 360;
            hsb.Saturation = Math.Min(1, Math.Max(0, hsb.Saturation));
            hsb.Brightness = Math.Min(1, Math.Max(0, hsb.Brightness));

            if (hsb.Saturation == 0)
            {
                return Color.FromArgb(Convert.ToInt32(hsb.Brightness * 255), Convert.ToInt32(hsb.Brightness * 255), Convert.ToInt32(hsb.Brightness * 255));
            }

            float fMax, fMid, fMin;
            int iSextant;
            if (hsb.Hue <= 60)
            {
                fMax = hsb.Brightness;
                fMid = hsb.Hue * hsb.Brightness / 60f;
                fMin = 0;
            }
            else if (hsb.Hue <= 120)
            {
                fMax = ((120 - hsb.Hue) * hsb.Brightness / 60f);
                fMid = hsb.Brightness;
                fMin = 0;
            }
            else if (hsb.Hue <= 180)
            {
                fMax = 0;
                fMid = hsb.Brightness;
                fMin = ((hsb.Hue - 120) * hsb.Brightness / 60f);
            }
            else if (hsb.Hue <= 240)
            {
                fMax = 0;
                fMid = ((240 - hsb.Hue) * hsb.Brightness / 60f);
                fMin = hsb.Brightness;
            }
            else if (hsb.Hue <= 300)
            {
                fMax = ((hsb.Hue - 240) * hsb.Brightness / 60f);
                fMid = 0;
                fMin = hsb.Brightness;
            }
            else
            {
                fMax = hsb.Brightness;
                fMid = 0;
                fMin = ((360 - hsb.Hue) * hsb.Brightness / 60f);
            }

            fMid = fMid + fMin;
            fMax = fMax + fMin;

            iSextant = (int)Math.Floor(hsb.Hue / 60f);
            switch (iSextant)
            {
                case 1:
                    return Color.FromArgb(Convert.ToInt32(fMid * 255), Convert.ToInt32(fMax * 255), Convert.ToInt32(fMin * 255));
                case 2:
                    return Color.FromArgb(Convert.ToInt32(fMin * 255), Convert.ToInt32(fMax * 255), Convert.ToInt32(fMid * 255));
                case 3:
                    return Color.FromArgb(Convert.ToInt32(fMin * 255), Convert.ToInt32(fMid * 255), Convert.ToInt32(fMax * 255));
                case 4:
                    return Color.FromArgb(Convert.ToInt32(fMid * 255), Convert.ToInt32(fMin * 255), Convert.ToInt32(fMax * 255));
                case 5:
                    return Color.FromArgb(Convert.ToInt32(fMax * 255), Convert.ToInt32(fMin * 255), Convert.ToInt32(fMid * 255));
                default:
                    return Color.FromArgb(Convert.ToInt32(fMax * 255), Convert.ToInt32(fMid * 255), Convert.ToInt32(fMin * 255));
            }
        }

        public static Color CMYKToColor(CMYK cmyk)
        {
            return Color.FromArgb(
                Convert.ToInt32((1 - cmyk.C) * (1 - cmyk.K) * 255),
                Convert.ToInt32((1 - cmyk.M) * (1 - cmyk.K) * 255),
                Convert.ToInt32((1 - cmyk.Y) * (1 - cmyk.K) * 255));
        }

        public static Color HexToColorSafe(string hex, ColorFormat format = ColorFormat.RGB)
        {
            if (!Regex.IsMatch(hex, "^[0-9A-F]+$", RegexOptions.IgnoreCase))
            {
                return Color.Black;
            }

            return HexToColor(hex, format);
        }

        public static Color DecimalToColorSafe(int dec, ColorFormat format = ColorFormat.RGB)
        {
            if (dec < 0)
            {
                return Color.Black;
            }

            return DecimalToColor(dec, format);
        }

        public static HSB RGBtoHSB(int r, int g, int b)
        {
            return ColorToHSB(Color.FromArgb(r, g, b));
        }

        public static HSB RGBtoHSB(Color color)
        {
            return ColorToHSB(color);
        }

        public static Color HSBTColor(float h, float s, float b)
        {
            return HSBTColor(new HSB(h, s, b));
        }

        public static Color Brightness(Color color, double value)
        {
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, (int)(color.R * value))),
                Math.Max(0, Math.Min(255, (int)(color.G * value))),
                Math.Max(0, Math.Min(255, (int)(color.B * value))));
        }

        public static Color Contrast(Color color, double value)
        {
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, (int)((color.R - 127) * value + 127))),
                Math.Max(0, Math.Min(255, (int)((color.G - 127) * value + 127))),
                Math.Max(0, Math.Min(255, (int)((color.B - 127) * value + 127))));
        }

        public static Color Multiply(this Color color, double value)
        {
            return Color.FromArgb(
                Math.Max(0, Math.Min(255, (int)(color.R * value))),
                Math.Max(0, Math.Min(255, (int)(color.G * value))),
                Math.Max(0, Math.Min(255, (int)(color.B * value))),
                color.A);
        }

        public static int PerceivedBrightness(Color color)
        {
            return (int)Math.Sqrt((color.R * color.R * 0.241) + (color.G * color.G * 0.691) + (color.B * color.B * 0.068));
        }

        public static Color Lerp(Color color1, Color color2, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)Math.Round(color1.A + t * (color2.A - color1.A)),
                (int)Math.Round(color1.R + t * (color2.R - color1.R)),
                (int)Math.Round(color1.G + t * (color2.G - color1.G)),
                (int)Math.Round(color1.B + t * (color2.B - color1.B)));
        }

        public static bool IsTransparent(this Color color)
        {
            return color.A == 0;
        }

        public static Color Darken(this Color color, int value)
        {
            int r = Math.Max(color.R - value, 0);
            int g = Math.Max(color.G - value, 0);
            int b = Math.Max(color.B - value, 0);
            return Color.FromArgb(color.A, r, g, b);
        }

        public static Color Lighten(this Color color, int value)
        {
            int r = Math.Min(color.R + value, 255);
            int g = Math.Min(color.G + value, 255);
            int b = Math.Min(color.B + value, 255);
            return Color.FromArgb(color.A, r, g, b);
        }

        public static Color Grayscale(this Color color)
        {
            int gray = (int)((color.R * 0.3) + (color.G * 0.59) + (color.B * 0.11));
            return Color.FromArgb(gray, gray, gray);
        }

        public static Color MedianColor(List<Color> colors)
        {
            Color color = Color.Black;

            if (colors != null && colors.Count > 0)
            {
                int a = 0, r = 0, g = 0, b = 0;

                foreach (Color c in colors)
                {
                    a += c.A;
                    r += c.R;
                    g += c.G;
                    b += c.B;
                }

                color = Color.FromArgb(a / colors.Count, r / colors.Count, g / colors.Count, b / colors.Count);
            }

            return color;
        }

        public static Color MiddleColor(Color color1, Color color2)
        {
            return Color.FromArgb((color1.A + color2.A) / 2, (color1.R + color2.R) / 2, (color1.G + color2.G) / 2, (color1.B + color2.B) / 2);
        }

        public static float GetHue(Color color)
        {
            int min = Math.Min(color.R, Math.Min(color.G, color.B));
            int max = Math.Max(color.R, Math.Max(color.G, color.B));

            if (min == max)
            {
                return 0;
            }

            float hue = color.GetHue();

            if (hue > 0)
            {
                return hue;
            }

            float delta = max - min;
            if (color.R == max)
            {
                hue = (color.G - color.B) / delta;
            }
            else if (color.G == max)
            {
                hue = 2 + (color.B - color.R) / delta;
            }
            else
            {
                hue = 4 + (color.R - color.G) / delta;
            }

            return hue * 60;
        }

        public static double ValidColor(double number)
        {
            return MathHelpers.Clamp(number, 0d, 1d);
        }

        public static int ValidColor(int number)
        {
            return MathHelpers.Clamp(number, 0, 255);
        }

        public static byte ValidColor(byte number)
        {
            return MathHelpers.Clamp(number, (byte)0, (byte)255);
        }
    }

    public class HSB
    {
        public HSB()
        {
        }

        public HSB(float h, float s, float b)
        {
            Hue = h;
            Saturation = s;
            Brightness = b;
        }

        public float Hue { get; set; }
        public float Saturation { get; set; }
        public float Brightness { get; set; }
    }

    public class CMYK
    {
        public CMYK()
        {
        }

        public CMYK(float c, float m, float y, float k)
        {
            C = c;
            M = m;
            Y = y;
            K = k;
        }

        public float C { get; set; }
        public float M { get; set; }
        public float Y { get; set; }
        public float K { get; set; }
    }
}
