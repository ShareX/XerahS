#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

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

namespace XerahS.UI.Helpers;

/// <summary>
/// Converts between Avalonia and System.Drawing colors for UI binding.
/// </summary>
public static class ColorConversion
{
    public static Avalonia.Media.Color ToAvaloniaColor(System.Drawing.Color color)
    {
        return Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static System.Drawing.Color ToDrawingColor(Avalonia.Media.Color color)
    {
        return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
