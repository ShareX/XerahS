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

using ShareX.Avalonia.Common;
using ShareX.Avalonia.Common.Colors;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using DrawingPoint = System.Drawing.Point;
using DrawingPointF = System.Drawing.PointF;

namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Text watermark")]
    public class DrawText : ImageEffect
    {
        [DefaultValue("Text watermark")]
        public string Text { get; set; } = "Text watermark";

        [DefaultValue(ContentAlignment.BottomRight)]
        public ContentAlignment Placement { get; set; } = ContentAlignment.BottomRight;

        [DefaultValue(typeof(DrawingPoint), "5, 5")]
        public DrawingPoint Offset { get; set; } = new DrawingPoint(5, 5);

        [DefaultValue(false), Description("If text watermark size bigger than source image then don't draw it.")]
        public bool AutoHide { get; set; }

        [DefaultValue(typeof(Font), "Arial, 11.25pt")]
        public Font TextFont { get; set; } = new Font("Arial", 11.25f);

        [DefaultValue(TextRenderingHint.SystemDefault)]
        public TextRenderingHint TextRenderingMode { get; set; } = TextRenderingHint.SystemDefault;

        [DefaultValue(typeof(Color), "235, 235, 235")]
        public Color TextColor { get; set; } = Color.FromArgb(235, 235, 235);

        [DefaultValue(true)]
        public bool DrawTextShadow { get; set; } = true;

        [DefaultValue(typeof(Color), "Black")]
        public Color TextShadowColor { get; set; } = Color.Black;

        [DefaultValue(typeof(DrawingPoint), "-1, -1")]
        public DrawingPoint TextShadowOffset { get; set; } = new DrawingPoint(-1, -1);

        private int cornerRadius = 4;

        [DefaultValue(4)]
        public int CornerRadius
        {
            get => cornerRadius;
            set => cornerRadius = System.Math.Max(0, value);
        }

        [DefaultValue(typeof(CanvasMargin), "5, 5, 5, 5")]
        public CanvasMargin Padding { get; set; } = new CanvasMargin(5);

        [DefaultValue(true)]
        public bool DrawBorder { get; set; } = true;

        [DefaultValue(typeof(Color), "Black")]
        public Color BorderColor { get; set; } = Color.Black;

        [DefaultValue(1)]
        public int BorderSize { get; set; } = 1;

        [DefaultValue(true)]
        public bool DrawBackground { get; set; } = true;

        [DefaultValue(typeof(Color), "42, 47, 56")]
        public Color BackgroundColor { get; set; } = Color.FromArgb(42, 47, 56);

        [DefaultValue(false)]
        public bool UseGradient { get; set; }

        public GradientInfo Gradient { get; set; }

        public DrawText()
        {
            AddDefaultGradient();
        }

        private void AddDefaultGradient()
        {
            Gradient = new GradientInfo();
            Gradient.Colors.Add(new GradientStop(Color.FromArgb(68, 120, 194), 0f));
            Gradient.Colors.Add(new GradientStop(Color.FromArgb(13, 58, 122), 50f));
            Gradient.Colors.Add(new GradientStop(Color.FromArgb(6, 36, 78), 50f));
            Gradient.Colors.Add(new GradientStop(Color.FromArgb(23, 89, 174), 100f));
        }

        public override Bitmap Apply(Bitmap bmp)
        {
            if (bmp == null || string.IsNullOrEmpty(Text))
            {
                return bmp;
            }

            using Font textFont = TextFont ?? new Font("Arial", 11.25f);

            if (textFont.Size < 1)
            {
                return bmp;
            }

            NameParser parser = new NameParser(NameParserType.Text)
            {
                ImageWidth = bmp.Width,
                ImageHeight = bmp.Height
            };

            string parsedText = parser.Parse(Text);
            Size textSize = WatermarkHelpers.MeasureText(parsedText, textFont, TextRenderingMode);
            Size watermarkSize = new Size(Padding.Left + textSize.Width + Padding.Right, Padding.Top + textSize.Height + Padding.Bottom);
            DrawingPoint watermarkPosition = WatermarkHelpers.GetPosition(Placement, Offset, bmp.Size, watermarkSize);
            Rectangle watermarkRectangle = new Rectangle(watermarkPosition, watermarkSize);

            if (AutoHide && !new Rectangle(DrawingPoint.Empty, bmp.Size).Contains(watermarkRectangle))
            {
                return bmp;
            }

            using Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.HighQuality;

            using GraphicsPath path = WatermarkHelpers.CreateRoundedRectangle(watermarkRectangle, CornerRadius);

            if (DrawBackground)
            {
                using Brush? backgroundBrush = UseGradient && Gradient != null && Gradient.IsValid
                    ? Gradient.GetGradientBrush(watermarkRectangle)
                    : new SolidBrush(BackgroundColor);

                g.FillPath(backgroundBrush, path);
            }

            if (DrawBorder)
            {
                int borderSize = System.Math.Max(1, BorderSize);

                if (borderSize % 2 == 0)
                {
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                }

                using Pen borderPen = new Pen(BorderColor, borderSize);
                g.DrawPath(borderPen, path);
                g.PixelOffsetMode = PixelOffsetMode.Default;
            }

            g.TextRenderingHint = TextRenderingMode;
            DrawingPointF textPoint = new DrawingPointF(watermarkRectangle.X + Padding.Left, watermarkRectangle.Y + Padding.Top);

            if (DrawTextShadow)
            {
                using Brush shadowBrush = new SolidBrush(TextShadowColor);
                g.DrawString(parsedText, textFont, shadowBrush, textPoint.X + TextShadowOffset.X, textPoint.Y + TextShadowOffset.Y);
            }

            using Brush textBrush = new SolidBrush(TextColor);
            g.DrawString(parsedText, textFont, textBrush, textPoint);

            return bmp;
        }

        protected override string? GetSummary()
        {
            if (!string.IsNullOrEmpty(Text))
            {
                return Text.Truncate(20, "...");
            }

            return null;
        }
    }
}
