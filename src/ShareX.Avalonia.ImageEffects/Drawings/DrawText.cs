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
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Text watermark")]
    public class DrawText : ImageEffect
    {
        [DefaultValue("Text watermark")]
        public string Text { get; set; } = "Text watermark";

        // [DefaultValue(ContentAlignment.BottomRight)]
        // public ContentAlignment Placement { get; set; }

        // [DefaultValue(typeof(DrawingPoint), "5, 5")]
        public SKPoint Offset { get; set; } = new SKPoint(5, 5);

        [DefaultValue(false)]
        public bool AutoHide { get; set; }

        // [DefaultValue(typeof(Font), "Arial, 11.25pt")]
        // public Font TextFont { get; set; } = new Font("Arial", 11.25f);
        public string FontFamily { get; set; } = "Arial";
        public float FontSize { get; set; } = 14;
        public SKFontStyleWeight FontWeight { get; set; } = SKFontStyleWeight.Normal;

        // [DefaultValue(TextRenderingHint.SystemDefault)]
        // public TextRenderingHint TextRenderingMode { get; set; }

        // [DefaultValue(typeof(Color), "235, 235, 235")]
        public SKColor TextColor { get; set; } = new SKColor(235, 235, 235);

        [DefaultValue(true)]
        public bool DrawTextShadow { get; set; } = true;

        // [DefaultValue(typeof(Color), "Black")]
        public SKColor TextShadowColor { get; set; } = SKColors.Black;

        // [DefaultValue(typeof(DrawingPoint), "-1, -1")]
        public SKPoint TextShadowOffset { get; set; } = new SKPoint(-1, -1);

        private int cornerRadius = 4;

        [DefaultValue(4)]
        public int CornerRadius
        {
            get => cornerRadius;
            set => cornerRadius = Math.Max(0, value);
        }

        [DefaultValue(typeof(CanvasMargin), "5, 5, 5, 5")]
        public CanvasMargin Padding { get; set; } = new CanvasMargin(5);

        [DefaultValue(true)]
        public bool DrawBorder { get; set; } = true;

        // [DefaultValue(typeof(Color), "Black")]
        public SKColor BorderColor { get; set; } = SKColors.Black;

        [DefaultValue(1)]
        public int BorderSize { get; set; } = 1;

        [DefaultValue(true)]
        public bool DrawBackground { get; set; } = true;

        // [DefaultValue(typeof(Color), "42, 47, 56")]
        public SKColor BackgroundColor { get; set; } = new SKColor(42, 47, 56);

        [DefaultValue(false)]
        public bool UseGradient { get; set; }

        // public GradientInfo Gradient { get; set; }

        public DrawText()
        {
            // AddDefaultGradient();
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
             // TODO: Draw text watermark using Skia
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

