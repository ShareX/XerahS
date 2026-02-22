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

using SkiaSharp;

namespace XerahS.Services.Abstractions;

/// <summary>
/// Composite service that routes image encoding to appropriate encoders based on format.
/// </summary>
public interface IImageEncoderService
{
    /// <summary>
    /// Encodes the bitmap to the specified file path, automatically selecting the appropriate encoder.
    /// </summary>
    /// <param name="bitmap">The bitmap to encode.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="format">The image format to encode to.</param>
    /// <param name="quality">The quality level (0-100). Default is 100.</param>
    Task EncodeAsync(SKBitmap bitmap, string filePath, EImageFormat format, int quality = 100);
}
