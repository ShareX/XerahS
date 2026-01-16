#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XerahS.UI.Converters;

/// <summary>
/// Converts a boolean (IsRecording) to a color for the recording indicator
/// True = Red (recording), False = Gray (idle)
/// </summary>
public class BoolToRecordingColorConverter : IValueConverter
{
    public static readonly BoolToRecordingColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRecording)
        {
            return isRecording 
                ? new SolidColorBrush(Color.FromRgb(239, 68, 68))  // Red-500
                : new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray-500
        }

        return new SolidColorBrush(Color.FromRgb(107, 114, 128));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
