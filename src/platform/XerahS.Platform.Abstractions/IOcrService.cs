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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Platform-agnostic OCR (Optical Character Recognition) service. One service can offer several engines
/// (<see cref="Engines"/>): the bundled PaddleOCR models, and the system's own engine (Windows.Media.Ocr on Windows,
/// Tesseract on Linux). <see cref="OcrOptions.Engine"/> picks the one a call uses.
/// </summary>
public interface IOcrService
{
    bool IsSupported { get; }

    /// <summary>
    /// Why OCR is unavailable and how to fix it (for example, which package to install), or null when it is available.
    /// </summary>
    string? UnavailableReason => null;

    /// <summary>
    /// The name of this service's own engine, as shown to the user.
    /// </summary>
    string EngineName => "System";

    /// <summary>
    /// The engines the user can choose between, the default first. Empty when the service has a single engine.
    /// </summary>
    IReadOnlyList<OcrEngineInfo> Engines => [];

    Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options);

    OcrLanguage[] GetAvailableLanguages();

    /// <summary>
    /// The languages to offer for one of <see cref="Engines"/>; null or an unknown id means the default engine.
    /// </summary>
    OcrLanguage[] GetAvailableLanguages(string? engine) => GetAvailableLanguages();
}

/// <param name="Id">The value for <see cref="OcrOptions.Engine"/>.</param>
public sealed record OcrEngineInfo(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public static class OcrEngineIds
{
    /// <summary>PaddleOCR where it can read the language, otherwise the system engine.</summary>
    public const string Automatic = "auto";
    public const string PaddleOcr = "paddleocr";
    public const string System = "system";
}

public record OcrLanguage(string DisplayName, string LanguageTag)
{
    public override string ToString() => DisplayName;
}

public class OcrOptions
{
    public string Language { get; set; } = "en";
    /// <summary>One of <see cref="IOcrService.Engines"/>; null or empty for the default engine.</summary>
    public string? Engine { get; set; }
    public float ScaleFactor { get; set; } = 2f;
    public bool SingleLine { get; set; }
    public bool IncludeGeometry { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public sealed record OcrWordRegion(string Text, string LineId, double X, double Y, double Width, double Height, double? Confidence = null);

public class OcrResult
{
    public string Text { get; set; } = string.Empty;
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }
    public bool GeometryComplete { get; set; }
    public IReadOnlyList<OcrWordRegion> Words { get; set; } = [];
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
