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
/// Platform-agnostic OCR (Optical Character Recognition) service.
/// Windows uses native Windows.Media.Ocr; Linux uses the system Tesseract.
/// </summary>
public interface IOcrService
{
    bool IsSupported { get; }

    /// <summary>
    /// Why OCR is unavailable and how to fix it (for example, which package to install), or null when it is available.
    /// </summary>
    string? UnavailableReason => null;

    Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options);

    OcrLanguage[] GetAvailableLanguages();
}

public record OcrLanguage(string DisplayName, string LanguageTag)
{
    public override string ToString() => DisplayName;
}

public class OcrOptions
{
    public string Language { get; set; } = "en";
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
