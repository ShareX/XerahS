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

using System.Globalization;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.OCR;

/// <summary>
/// The OCR service the application uses: the bundled PaddleOCR engine and the platform's own engine behind one
/// interface, so the user can choose between them. With the engine left on Automatic, PaddleOCR reads the image
/// unless the chosen language is written in a script its model does not know (Cyrillic, Korean, Arabic and others)
/// and the system engine has that language.
/// </summary>
public sealed class PaddleOcrService : IOcrService
{
    /// <summary>The language tag for "let PaddleOCR read whatever is there".</summary>
    public const string AutomaticLanguageTag = "auto";

    private const string PaddleName = "PaddleOCR";

    private readonly IOcrService? _system;
    private readonly Func<bool> _paddleAvailable;
    private readonly Func<SKBitmap, PaddleOcrResult> _recognize;
    private readonly Func<string, bool> _canRead;

    public PaddleOcrService(IOcrService? system)
        : this(system, () => PaddleOcrEngine.IsAvailable, bitmap => PaddleOcrEngine.Shared.Recognize(bitmap), PaddleOcrEngine.CanRead)
    {
    }

    internal PaddleOcrService(IOcrService? system, Func<bool> paddleAvailable, Func<SKBitmap, PaddleOcrResult> recognize, Func<string, bool> canRead)
    {
        _system = system;
        _paddleAvailable = paddleAvailable;
        _recognize = recognize;
        _canRead = canRead;
    }

    /// <summary>
    /// Puts PaddleOCR in front of the platform's OCR service. Call once, after the platform has registered its own.
    /// </summary>
    public static void Register()
    {
        if (PlatformServices.Ocr is not PaddleOcrService)
        {
            PlatformServices.Ocr = new PaddleOcrService(PlatformServices.Ocr);
        }
    }

    private bool SystemSupported => _system?.IsSupported == true;

    public bool IsSupported => _paddleAvailable() || SystemSupported;

    public string? UnavailableReason => IsSupported ? null : _system?.UnavailableReason ?? "OCR is not available on this platform.";

    public string EngineName => PaddleName;

    public IReadOnlyList<OcrEngineInfo> Engines
    {
        get
        {
            bool paddle = _paddleAvailable();

            if (paddle && SystemSupported)
            {
                return
                [
                    new OcrEngineInfo(OcrEngineIds.Automatic, "Automatic"),
                    new OcrEngineInfo(OcrEngineIds.PaddleOcr, PaddleName),
                    new OcrEngineInfo(OcrEngineIds.System, _system!.EngineName)
                ];
            }

            if (paddle)
            {
                return [new OcrEngineInfo(OcrEngineIds.PaddleOcr, PaddleName)];
            }

            return SystemSupported ? [new OcrEngineInfo(OcrEngineIds.System, _system!.EngineName)] : [];
        }
    }

    public OcrLanguage[] GetAvailableLanguages() => GetAvailableLanguages(null);

    public OcrLanguage[] GetAvailableLanguages(string? engine)
    {
        OcrLanguage automatic = new("Automatic", AutomaticLanguageTag);

        return ResolveEngine(engine) switch
        {
            OcrEngineIds.PaddleOcr => [automatic],
            OcrEngineIds.System => SystemSupported ? _system!.GetAvailableLanguages() : [],
            _ => [automatic, .. _system!.GetAvailableLanguages()]
        };
    }

    public async Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);

        string engine = ResolveEngine(options.Engine);

        if (engine == OcrEngineIds.Automatic)
        {
            engine = PaddleReads(options.Language) || !SystemHasLanguage(options.Language) ? OcrEngineIds.PaddleOcr : OcrEngineIds.System;
        }

        if (engine == OcrEngineIds.System)
        {
            return SystemSupported
                ? await _system!.RecognizeAsync(image, options).ConfigureAwait(false)
                : Failed(_system?.UnavailableReason ?? "OCR is not available on this platform.");
        }

        if (!_paddleAvailable())
        {
            return Failed("PaddleOCR is not available in this build. Choose another OCR engine.");
        }

        try
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            PaddleOcrResult result = await Task.Run(() => _recognize(image), options.CancellationToken).ConfigureAwait(false);
            return ToOcrResult(result, image.Width, image.Height, options.SingleLine);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PaddleOCR");
            return Failed($"PaddleOCR failed: {ex.Message}");
        }
    }

    /// <summary>
    /// The engine a request runs on: the one asked for when this build has it, otherwise the best one there is.
    /// </summary>
    private string ResolveEngine(string? engine)
    {
        bool paddle = _paddleAvailable();
        engine = engine?.Trim().ToLowerInvariant();

        if (!paddle)
        {
            return OcrEngineIds.System;
        }

        if (!SystemSupported)
        {
            return OcrEngineIds.PaddleOcr;
        }

        return engine is OcrEngineIds.PaddleOcr or OcrEngineIds.System ? engine : OcrEngineIds.Automatic;
    }

    private bool SystemHasLanguage(string? tag)
    {
        if (!SystemSupported || string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string language = BaseLanguage(tag);

        try
        {
            return _system!.GetAvailableLanguages().Any(available =>
                available.LanguageTag.Equals(tag, StringComparison.OrdinalIgnoreCase) ||
                BaseLanguage(available.LanguageTag).Equals(language, StringComparison.OrdinalIgnoreCase) ||
                (FindCulture(available.LanguageTag)?.TwoLetterISOLanguageName.Equals(FindCulture(tag)?.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OCR language lookup");
            return false;
        }
    }

    /// <summary>
    /// Whether PaddleOCR can read a language, judged by whether the language's own name is written in characters the
    /// model knows. A tag nothing is known about counts as readable, so PaddleOCR stays the default.
    /// </summary>
    private bool PaddleReads(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tag.Equals(AutomaticLanguageTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        CultureInfo? culture = FindCulture(tag);

        try
        {
            return culture == null || _canRead(culture.NativeName);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PaddleOCR dictionary");
            return true;
        }
    }

    /// <summary>
    /// The culture for a BCP 47 tag ("ru", "zh-Hans") or a Tesseract code ("rus", "chi_sim").
    /// </summary>
    internal static CultureInfo? FindCulture(string tag)
    {
        string language = BaseLanguage(tag);

        if (language.Length == 3)
        {
            if (language.Equals("chi", StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.GetCultureInfo("zh");
            }

            return CultureInfo.GetCultures(CultureTypes.NeutralCultures).FirstOrDefault(culture =>
                culture.ThreeLetterISOLanguageName.Equals(language, StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(language);
            // An unknown tag still gives a culture, named after the tag itself.
            return culture.ThreeLetterISOLanguageName.Length == 3 && culture.ThreeLetterISOLanguageName != "ivl" ? culture : null;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static string BaseLanguage(string tag) => tag.Trim().Split('-', '_')[0];

    internal static OcrResult ToOcrResult(PaddleOcrResult result, int width, int height, bool singleLine)
    {
        List<OcrWordRegion> words = [];

        for (int line = 0; line < result.Lines.Count; line++)
        {
            foreach (PaddleOcrTextRegion region in result.Lines[line])
            {
                // PaddleOCR reports a box per piece of text, not per word, so each piece is one entry of its line.
                SKRect bounds = SKRect.Intersect(region.Bounds, SKRect.Create(width, height));

                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    words.Add(new OcrWordRegion(region.Text, line.ToString(CultureInfo.InvariantCulture),
                        bounds.Left, bounds.Top, bounds.Width, bounds.Height, Math.Clamp(region.Score, 0f, 1f)));
                }
            }
        }

        return new OcrResult
        {
            Text = result.GetText(singleLine),
            Success = true,
            SourceWidth = width,
            SourceHeight = height,
            GeometryComplete = words.Count == result.Regions.Count,
            Words = words
        };
    }

    private static OcrResult Failed(string message) => new()
    {
        Text = string.Empty,
        Success = false,
        ErrorMessage = message
    };
}
