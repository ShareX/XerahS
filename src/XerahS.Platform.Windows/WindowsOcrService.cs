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

#if WINDOWS

using SkiaSharp;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using XerahS.Platform.Abstractions;
using AbsOcrResult = XerahS.Platform.Abstractions.OcrResult;

namespace XerahS.Platform.Windows;

public class WindowsOcrService : IOcrService
{
    public bool IsSupported => true; // Windows 10.0.18362.0+ is our minimum target

    public OcrLanguage[] GetAvailableLanguages()
    {
        return OcrEngine.AvailableRecognizerLanguages
            .Select(lang => new OcrLanguage(lang.DisplayName, lang.LanguageTag))
            .OrderBy(lang => lang.DisplayName)
            .ToArray();
    }

    public async Task<AbsOcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
    {
        try
        {
            float scaleFactor = Math.Max(options.ScaleFactor, 1f);
            string text = await Task.Run(async () =>
            {
                using var scaledBitmap = ScaleBitmap(image, scaleFactor);
                return await RecognizeInternal(scaledBitmap, options.Language, options.SingleLine);
            });

            return new AbsOcrResult
            {
                Text = text,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new AbsOcrResult
            {
                Text = string.Empty,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static SKBitmap ScaleBitmap(SKBitmap source, float scaleFactor)
    {
        if (scaleFactor <= 1f)
        {
            return source.Copy();
        }

        int newWidth = (int)(source.Width * scaleFactor);
        int newHeight = (int)(source.Height * scaleFactor);
        var info = new SKImageInfo(newWidth, newHeight, source.ColorType, source.AlphaType);
        var scaled = new SKBitmap(info);
        source.ScalePixels(scaled, SKFilterQuality.High);
        return scaled;
    }

    private static async Task<string> RecognizeInternal(SKBitmap bitmap, string languageTag, bool singleLine)
    {
        var language = new Language(languageTag);

        if (!OcrEngine.IsLanguageSupported(language))
        {
            throw new InvalidOperationException(
                $"{language.DisplayName} language is not available on this system for OCR.");
        }

        var engine = OcrEngine.TryCreateFromLanguage(language)
            ?? throw new InvalidOperationException(
                $"Failed to create OCR engine for {language.DisplayName}.");

        // Encode SKBitmap to BMP bytes
        using var skImage = SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Bmp, 100);
        byte[] bmpBytes = data.ToArray();

        // Create WinRT stream from bytes
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bmpBytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);

        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        var ocrResult = await engine.RecognizeAsync(softwareBitmap);

        IEnumerable<string> lines;

        if (languageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
            languageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            // CJK: remove spaces between words
            lines = ocrResult.Lines.Select(line =>
                string.Concat(line.Words.Select(word => word.Text)));
        }
        else if (language.LayoutDirection == LanguageLayoutDirection.Rtl)
        {
            // RTL: reverse word order
            lines = ocrResult.Lines.Select(line =>
                string.Join(" ", line.Words.Reverse().Select(word => word.Text)));
        }
        else
        {
            lines = ocrResult.Lines.Select(line => line.Text);
        }

        string separator = singleLine ? " " : Environment.NewLine;
        return string.Join(separator, lines);
    }
}

#endif
