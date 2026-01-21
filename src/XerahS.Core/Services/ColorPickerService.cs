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

using System.Drawing;
using XerahS.Common;

namespace XerahS.Core.Services;

/// <summary>
/// Provides formatting helpers for color picker workflows and screen sampling outputs.
/// </summary>
public static class ColorPickerService
{
    /// <summary>
    /// Formats a template string using color and screen position tokens.
    /// </summary>
    public static string FormatColorText(string template, Color color, System.Drawing.Point position)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return CodeMenuEntryPixelInfo.Parse(template, color, position);
    }

    /// <summary>
    /// Builds the clipboard text for the screen color picker based on user settings.
    /// </summary>
    public static string GetClipboardText(TaskSettingsTools? toolsSettings, Color color, System.Drawing.Point position, bool useCtrlFormat)
    {
        string? format = useCtrlFormat
            ? toolsSettings?.ScreenColorPickerFormatCtrl
            : toolsSettings?.ScreenColorPickerFormat;

        if (string.IsNullOrWhiteSpace(format))
        {
            format = "$hex";
        }

        return FormatColorText(format, color, position);
    }

    /// <summary>
    /// Builds the info text shown after sampling a screen color.
    /// </summary>
    public static string GetInfoText(TaskSettingsTools? toolsSettings, Color color, System.Drawing.Point position)
    {
        string? template = toolsSettings?.ScreenColorPickerInfoText;
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "RGB: $r255, $g255, $b255$nHex: $hex$nX: $x Y: $y";
        }

        return FormatColorText(template, color, position);
    }
}
