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

using System.Diagnostics;
using System.Globalization;
using System.Text;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services.QuickSetup;
using XerahS.Platform.Linux.Wayland.WindowQuery;

namespace XerahS.Platform.Linux;

/// <summary>
/// OCR through the system's <c>tesseract</c> program. Nothing is bundled: the program and its
/// language data come from the distribution's packages, and the image goes in over standard input.
/// </summary>
public class LinuxOcrService : IOcrService
{
    private const string TesseractCommand = "tesseract";
    private const int MaxScaledDimension = 10000;

    private OcrLanguage[]? _languages;

    public bool IsSupported => WaylandWindowPointQueryCommandRunner.CommandExists(TesseractCommand);

    public string? UnavailableReason => IsSupported
        ? null
        : $"OCR needs Tesseract. Install it: {LinuxDistroGuidance.InstallHint(LinuxDistroGuidance.Detect(), QuickSetupPackage.Tesseract)}";

    public OcrLanguage[] GetAvailableLanguages()
    {
        // Only cache a non-empty list, so language packs installed while the app runs show up.
        if (_languages is { Length: > 0 })
        {
            return _languages;
        }

        if (!IsSupported)
        {
            return [];
        }

        try
        {
            (int exitCode, byte[] output, string error) = RunTesseractAsync(["--list-langs"], null, TimeSpan.FromSeconds(10), CancellationToken.None)
                .GetAwaiter().GetResult();
            if (exitCode != 0)
            {
                DebugHelper.WriteLine($"tesseract --list-langs failed ({exitCode}): {error.Trim()}");
                return [];
            }

            _languages = ParseLanguages(Encoding.UTF8.GetString(output));
            return _languages;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "tesseract --list-langs");
            return [];
        }
    }

    public async Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
    {
        try
        {
            if (!IsSupported)
            {
                return Failed(UnavailableReason!);
            }

            OcrLanguage[] available = GetAvailableLanguages();
            string? language = MatchLanguage(options.Language, available);
            if (language == null)
            {
                string installed = available.Length > 0 ? string.Join(", ", available.Select(l => l.LanguageTag)) : "none";
                return Failed($"Tesseract has no language data for \"{options.Language}\". Installed: {installed}. Install the matching tesseract language package.");
            }

            byte[] png = await Task.Run(() => EncodeScaledPng(image, options.ScaleFactor)).ConfigureAwait(false);

            var arguments = new List<string> { "stdin", "stdout", "-l", language };
            if (options.IncludeGeometry) arguments.Add("tsv");
            (int exitCode, byte[] output, string error) = await RunTesseractAsync(
                arguments, png, TimeSpan.FromMinutes(2), options.CancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                return Failed($"tesseract failed: {error.Trim()}");
            }

            if (options.IncludeGeometry)
            {
                using var codec = SKCodec.Create(new SKMemoryStream(png));
                return ParseGeometry(Encoding.UTF8.GetString(output), image.Width, image.Height, codec.Info.Width, codec.Info.Height, options.SingleLine);
            }
            return new OcrResult
            {
                Text = FormatText(Encoding.UTF8.GetString(output), options.SingleLine),
                Success = true
            };
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

    internal static OcrResult ParseGeometry(string tsv, int width, int height, int scaledWidth, int scaledHeight, bool singleLine)
    {
        var words = new List<OcrWordRegion>();
        bool valid = tsv.StartsWith("level\tpage_num\t", StringComparison.Ordinal) && width > 0 && height > 0 && scaledWidth > 0 && scaledHeight > 0;
        foreach (string line in tsv.Split('\n').Skip(1))
        {
            var cells = line.TrimEnd('\r').Split('\t');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (cells.Length < 12) { valid = false; continue; }
            if (cells[0] != "5" || string.IsNullOrWhiteSpace(cells[11])) continue;
            if (!double.TryParse(cells[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                !double.TryParse(cells[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
                !double.TryParse(cells[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double w) ||
                !double.TryParse(cells[9], NumberStyles.Float, CultureInfo.InvariantCulture, out double h) ||
                !double.TryParse(cells[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double confidence) ||
                !double.IsFinite(x + y + w + h + confidence) || x < 0 || y < 0 || w <= 0 || h <= 0 ||
                x + w > scaledWidth || y + h > scaledHeight || confidence < 0 || confidence > 100)
            { valid = false; continue; }
            words.Add(new(cells[11], string.Join("/", cells[1..5]), x * width / scaledWidth, y * height / scaledHeight,
                w * width / scaledWidth, h * height / scaledHeight, confidence / 100));
        }
        return new OcrResult
        {
            Success = true, GeometryComplete = valid, SourceWidth = width, SourceHeight = height, Words = words,
            Text = string.Join(singleLine ? " " : Environment.NewLine,
                words.GroupBy(word => word.LineId).Select(group => string.Join(" ", group.Select(word => word.Text))))
        };
    }

    private static OcrResult Failed(string message) => new()
    {
        Text = string.Empty,
        Success = false,
        ErrorMessage = message
    };

    private static byte[] EncodeScaledPng(SKBitmap source, float requestedScaleFactor)
    {
        float scale = float.IsFinite(requestedScaleFactor) ? Math.Max(requestedScaleFactor, 1f) : 1f;
        int largestDimension = Math.Max(source.Width, source.Height);
        if (largestDimension > 0)
        {
            scale = Math.Min(scale, Math.Max(1f, (float)MaxScaledDimension / largestDimension));
        }

        if (Math.Abs(scale - 1f) < 0.001f)
        {
            using SKImage image = SKImage.FromBitmap(source);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var scaled = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        source.ScalePixels(scaled, new SKSamplingOptions(SKCubicResampler.Mitchell));
        using SKImage scaledImage = SKImage.FromBitmap(scaled);
        using SKData scaledData = scaledImage.Encode(SKEncodedImageFormat.Png, 100);
        return scaledData.ToArray();
    }

    private static async Task<(int ExitCode, byte[] Output, string Error)> RunTesseractAsync(
        IEnumerable<string> arguments, byte[]? input, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = TesseractCommand,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = input != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start tesseract.");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        // Read both pipes while writing stdin so a full pipe buffer cannot deadlock the child.
        using var output = new MemoryStream();
        Task outputTask = process.StandardOutput.BaseStream.CopyToAsync(output, timeoutSource.Token);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);

        try
        {
            if (input != null)
            {
                await process.StandardInput.BaseStream.WriteAsync(input, timeoutSource.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);
            return (process.ExitCode, output.ToArray(), error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }

            throw new TimeoutException($"tesseract did not finish within {timeout.TotalSeconds:0} seconds.");
        }
    }

    /// <summary>
    /// <c>tesseract --list-langs</c> prints a header line, then one code per line. "osd" is orientation detection, not a language.
    /// </summary>
    internal static OcrLanguage[] ParseLanguages(string output)
    {
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("List of available languages", StringComparison.OrdinalIgnoreCase) && line != "osd" && !line.Contains(' '))
            .Select(code => new OcrLanguage(GetDisplayName(code), code))
            .OrderBy(language => language.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// The installed Tesseract code for a tag: the code itself, or the ISO 639-2 code of a BCP 47 tag that
    /// settings carried over from Windows or the defaults ("en" → "eng", "zh-Hans" → "chi_sim").
    /// </summary>
    internal static string? MatchLanguage(string? tag, IReadOnlyList<OcrLanguage> available)
    {
        tag = tag?.Trim();
        if (string.IsNullOrEmpty(tag))
        {
            return null;
        }

        OcrLanguage? exact = available.FirstOrDefault(l => l.LanguageTag.Equals(tag, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact.LanguageTag;
        }

        string? code = ToTesseractCode(tag);
        return code != null && available.Any(l => l.LanguageTag == code) ? code : null;
    }

    internal static string? ToTesseractCode(string tag)
    {
        if (tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return tag.Contains("Hant", StringComparison.OrdinalIgnoreCase) || tag.EndsWith("TW", StringComparison.OrdinalIgnoreCase) ||
                tag.EndsWith("HK", StringComparison.OrdinalIgnoreCase) ? "chi_tra" : "chi_sim";
        }

        try
        {
            string threeLetter = CultureInfo.GetCultureInfo(tag).ThreeLetterISOLanguageName;
            return string.IsNullOrEmpty(threeLetter) || threeLetter == "ivl" ? null : threeLetter;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    internal static string FormatText(string text, bool singleLine)
    {
        IEnumerable<string> lines = text.Replace("\f", "").Split('\n').Select(line => line.TrimEnd()).Where(line => line.Length > 0);
        return string.Join(singleLine ? " " : Environment.NewLine, lines);
    }

    private static string GetDisplayName(string code)
    {
        string baseCode = code.Split('_')[0];
        foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (culture.ThreeLetterISOLanguageName == baseCode)
            {
                return code.Contains('_') ? $"{culture.DisplayName} ({code})" : culture.DisplayName;
            }
        }

        return code;
    }
}
