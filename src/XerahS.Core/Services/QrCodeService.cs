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
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace XerahS.Core.Services;

public static class QrCodeService
{
    public const int MaxInputLength = 2953;

    public static bool TryGenerate(string text, int size, out SKBitmap? bitmap, out string? error)
    {
        bitmap = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter text to generate a QR code.";
            return false;
        }

        if (text.Length > MaxInputLength)
        {
            error = $"Input text is too long. Maximum length is {MaxInputLength} characters.";
            return false;
        }

        if (size <= 0)
        {
            error = "QR code size must be greater than zero.";
            return false;
        }

        try
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Width = size,
                    Height = size,
                    Margin = 1,
                    PureBarcode = true
                }
            };

            bitmap = writer.Write(text);
            if (bitmap == null)
            {
                error = "Failed to generate QR code.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<string> Decode(SKBitmap bitmap, out string? error)
    {
        error = null;

        if (bitmap == null)
        {
            error = "No image provided for decoding.";
            return Array.Empty<string>();
        }

        try
        {
            var reader = new ZXing.SkiaSharp.BarcodeReader
            {
                AutoRotate = true,
                TryInverted = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
                }
            };

            var results = reader.DecodeMultiple(bitmap);
            if (results == null || results.Length == 0)
            {
                return Array.Empty<string>();
            }

            return results
                .Select(result => result.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return Array.Empty<string>();
        }
    }
}
