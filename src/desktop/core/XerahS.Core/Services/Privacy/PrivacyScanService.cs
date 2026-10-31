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

using System.Text.RegularExpressions;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using ZXing;
using ZXing.Common;

namespace XerahS.Core.Services.Privacy;

/// <summary>Bounded local evidence supplements AI. It never declares a failed AI scan clean.</summary>
public static class PrivacyScanService
{
    private static readonly (PrivacyCategory Category, Regex Pattern)[] Patterns =
    [
        (PrivacyCategory.Email, Pattern(@"[\w.+-]+@[\w.-]+\.[\p{L}]{2,}")),
        (PrivacyCategory.PaymentCard, Pattern(@"\b(?:[0-9][ -]?){13,19}\b")),
        (PrivacyCategory.Phone, Pattern(@"(?<!\w)\+?[0-9][0-9 ().-]{7,}[0-9](?!\w)")),
        (PrivacyCategory.Credential, Pattern(@"(?i)(?:password|passwd|api[ _-]?key|access[ _-]?token|secret|recovery[ _-]?code|private[ _-]?key)\s*[:=]\s*\S+")),
        (PrivacyCategory.GovernmentId, Pattern(@"(?i)(?:passport|social security|tax id|ssn)\s*[:#=]?\s*[A-Z0-9 -]{4,}")),
        (PrivacyCategory.Address, Pattern(@"(?i)\b[0-9]+\s+.{1,80}\b(?:street|road|avenue|lane|drive|crescent|st\.|rd\.)\b"))
    ];
    private static Regex Pattern(string expression) => new(expression, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static async Task<PrivacyScanResult> AddLocalEvidenceAsync(SKBitmap image, TaskSettingsPrivacy policy,
        PrivacyScanResult scan, IOcrService? ocr, CancellationToken token)
    {
        var findings = scan.Findings.ToList();
        string? limitation = null;
        try
        {
            var rules = policy.CustomPatterns ?? [];
            if (rules.Count > 16 || rules.Any(rule => rule.Length > 500)) throw new ArgumentException("Custom rule limit exceeded.");
            foreach (var rule in rules) _ = Pattern(rule); // Validate even when OCR is unavailable.
            if (ocr?.IsSupported == true)
            {
                var result = await ocr.RecognizeAsync(image, new OcrOptions { IncludeGeometry = true, ScaleFactor = 1,
                    Language = "en", CancellationToken = token });
                if (result.Success && result.GeometryComplete && result.SourceWidth == image.Width && result.SourceHeight == image.Height)
                    findings.AddRange(DetectText(result, policy));
                else limitation = "Local OCR geometry was unavailable; inspect all text manually.";
            }
            else limitation = "Local OCR is unavailable; inspect all text manually.";
            if ((policy.Categories ?? []).Contains(PrivacyCategory.EncodedSecret))
            {
                var reader = new ZXing.SkiaSharp.BarcodeReader { AutoRotate = false, Options = new DecodingOptions
                    { TryHarder = true, TryInverted = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
                foreach (var result in reader.DecodeMultiple(image) ?? [])
                {
                    // Finder centres lie inside the symbol. Extend outward to cover the whole code and quiet zone.
                    var points = result.ResultPoints;
                    if (points == null || points.Length < 3) { limitation = "Some QR code bounds could not be verified; inspect manually."; continue; }
                    double left = points.Min(p => p.X), top = points.Min(p => p.Y);
                    double right = points.Max(p => p.X), bottom = points.Max(p => p.Y);
                    double pad = Math.Max(right - left, bottom - top) * 0.3 + 4;
                    left = Math.Max(0, left - pad); top = Math.Max(0, top - pad);
                    right = Math.Min(image.Width, right + pad); bottom = Math.Min(image.Height, bottom + pad);
                    findings.Add(new(PrivacyCategory.EncodedSecret, left / image.Width, top / image.Height,
                        (right - left) / image.Width, (bottom - top) / image.Height, 1));
                }
            }
            token.ThrowIfCancellationRequested();
            if (findings.Count > 512) return new(PrivacyScanStatus.Incomplete, [], "Too many regions; annotate manually.");
            foreach (var finding in findings) RegionRedactionRenderer.Validate(finding);
            return scan with { Findings = findings, Limitation = limitation };
        }
        catch (OperationCanceledException) { throw; }
        catch { return scan with { Status = PrivacyScanStatus.Incomplete, Limitation = "Local analysis could not finish; annotate manually." }; }
    }

    private static bool HasCardChecksum(string value)
    {
        var digits = value.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();
        if (digits.Length < 13 || digits.Length > 19) return false;
        int sum = 0;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int digit = digits[i];
            if ((digits.Length - 1 - i) % 2 == 1) { digit *= 2; if (digit > 9) digit -= 9; }
            sum += digit;
        }
        return sum % 10 == 0;
    }

    internal static IReadOnlyList<PrivacyFinding> DetectText(OcrResult result, TaskSettingsPrivacy policy)
    {
        var findings = new List<PrivacyFinding>();
        foreach (var group in result.Words.GroupBy(word => word.LineId))
        {
            string line = string.Join(" ", group.Select(word => word.Text));
            if (line.Length > 10000) throw new ArgumentException("OCR line too long.");
            var customPatterns = policy.CustomPatterns ?? [];
            if (customPatterns.Count > 16 || customPatterns.Any(pattern => pattern.Length > 500))
                throw new ArgumentException("Custom sensitivity rules exceed the limit.");
            var rules = Patterns.Concat(customPatterns.Select(pattern => (PrivacyCategory.Custom, Pattern(pattern))));
            foreach (var (category, pattern) in rules)
            {
                if (!(policy.Categories ?? []).Contains(category) || !pattern.IsMatch(line)) continue;
                if (category == PrivacyCategory.PaymentCard && !pattern.Matches(line).Any(match => HasCardChecksum(match.Value))) continue;
                // Redact the whole matched line to include wrapped punctuation and adjacent identifiers.
                double left = group.Min(w => w.X), top = group.Min(w => w.Y);
                double right = group.Max(w => w.X + w.Width), bottom = group.Max(w => w.Y + w.Height);
                findings.Add(new(category, left / result.SourceWidth, top / result.SourceHeight,
                    (right - left) / result.SourceWidth, (bottom - top) / result.SourceHeight, 1));
            }
        }
        return findings;
    }
}
