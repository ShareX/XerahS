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
            float scaleFactor = GetEffectiveScaleFactor(image, options.ScaleFactor);
            options.CancellationToken.ThrowIfCancellationRequested();
            var result = await Task.Run(async () =>
            {
                using var scaledBitmap = ScaleBitmap(image, scaleFactor);
                return (Recognition: await RecognizeInternal(scaledBitmap, options.Language, options.SingleLine), Width: scaledBitmap.Width, Height: scaledBitmap.Height);
            });

            return new AbsOcrResult
            {
                Text = result.Recognition.Text,
                Success = true,
                SourceWidth = image.Width,
                SourceHeight = image.Height,
                GeometryComplete = options.IncludeGeometry && result.Recognition.GeometryComplete,
                Words = options.IncludeGeometry ? result.Recognition.Words.Select(word => word with
                { X = word.X * image.Width / result.Width, Y = word.Y * image.Height / result.Height, Width = word.Width * image.Width / result.Width, Height = word.Height * image.Height / result.Height }).ToArray() : []
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
        if (Math.Abs(scaleFactor - 1f) < 0.001f)
        {
            return source.Copy();
        }

        int newWidth = Math.Max(1, (int)Math.Round(source.Width * scaleFactor));
        int newHeight = Math.Max(1, (int)Math.Round(source.Height * scaleFactor));
        var info = new SKImageInfo(newWidth, newHeight, source.ColorType, source.AlphaType);
        var scaled = new SKBitmap(info);
        source.ScalePixels(scaled, new SKSamplingOptions(SKCubicResampler.Mitchell));
        return scaled;
    }

    private static float GetEffectiveScaleFactor(SKBitmap source, float requestedScaleFactor)
    {
        float requested = float.IsFinite(requestedScaleFactor) ? Math.Max(requestedScaleFactor, 1f) : 1f;
        int largestDimension = Math.Max(source.Width, source.Height);
        if (largestDimension <= 0)
        {
            return 1f;
        }

        float maximumSupportedScale = (float)OcrEngine.MaxImageDimension / largestDimension;
        return Math.Min(requested, maximumSupportedScale);
    }

    private static async Task<AbsOcrResult> RecognizeInternal(SKBitmap bitmap, string languageTag, bool singleLine)
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

        // Encode SKBitmap to PNG bytes (BMP encoding not supported by SkiaSharp)
        using var skImage = SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        byte[] pngBytes = data.ToArray();

        // Create WinRT stream from bytes
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(pngBytes);
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
        bool unrotated = !ocrResult.TextAngle.HasValue || Math.Abs(ocrResult.TextAngle.Value) < 0.01;
        return new AbsOcrResult
        {
            Text = string.Join(separator, lines), Success = true, GeometryComplete = unrotated,
            Words = unrotated ? ocrResult.Lines.SelectMany((line, index) => line.Words.Select(word =>
                new OcrWordRegion(word.Text, index.ToString(), word.BoundingRect.X, word.BoundingRect.Y,
                    word.BoundingRect.Width, word.BoundingRect.Height))).ToArray() : []
        };
    }
}

#endif
