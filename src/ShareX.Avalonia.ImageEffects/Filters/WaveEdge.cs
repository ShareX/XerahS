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
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using SkiaSharp;


namespace ShareX.Avalonia.ImageEffects
{
    [Description("Wave edge")]
    public class WaveEdge : ImageEffect
    {
        [DefaultValue(15)]
        public int Depth { get; set; }

        [DefaultValue(20)]
        public int Range { get; set; }

        [DefaultValue(AnchorSides.All)]
        public AnchorSides Sides { get; set; }

        public WaveEdge()
        {
            // this.ApplyDefaultPropertyValues();
            Depth = 15;
            Range = 20;
            Sides = AnchorSides.All;
        }

        public override SKBitmap Apply(SKBitmap bmp)
        {
            return ImageEffectsProcessing.DrawWaveEdge(bmp, Depth, Range, Sides);
        }

        protected override string? GetSummary()
        {
            return $"{Depth}, {Range}";
        }
    }
}
