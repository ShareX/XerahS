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
using ShareX.Avalonia.Common.Helpers;
using ShareX.Avalonia.ImageEffects.Helpers;
using System.ComponentModel;
using System.Drawing;

namespace ShareX.Avalonia.ImageEffects.Drawings
{
    [Description("Background image")]
    public class DrawBackgroundImage : ImageEffect
    {
        [DefaultValue("")]
        public string ImageFilePath { get; set; }

        [DefaultValue(true)]
        public bool Center { get; set; }

        [DefaultValue(false)]
        public bool Tile { get; set; }

        public DrawBackgroundImage()
        {
            this.ApplyDefaultPropertyValues();
        }

        public override Bitmap Apply(Bitmap bmp)
        {
            return ImageEffectsProcessing.DrawBackgroundImage(bmp, ImageFilePath, Center, Tile);
        }

        protected override string? GetSummary()
        {
            if (!string.IsNullOrEmpty(ImageFilePath))
            {
                return System.IO.Path.GetFileName(ImageFilePath);
            }

            return null;
        }
    }
}
