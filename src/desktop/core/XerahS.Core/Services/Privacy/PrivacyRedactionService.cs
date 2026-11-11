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

using System.Text.Json;
using SkiaSharp;
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Services.Privacy;

public enum PrivacyRedactionOutcome
{
    /// <summary>A redacted copy was written; see <see cref="PrivacyRedactionResult.OutputPath"/>.</summary>
    Saved,
    /// <summary>The user cancelled the review. Nothing was written.</summary>
    Cancelled,
    /// <summary>The image could not be read, is animated, or is too large. Nothing was written.</summary>
    UnsupportedImage,
    /// <summary>The scan, review or save failed. Nothing was written, or a partial file was removed.</summary>
    Failed
}

public sealed record PrivacyRedactionResult(PrivacyRedactionOutcome Outcome, string? OutputPath = null);

/// <summary>
/// The on-demand Redact sensitive information tool: optional AI analysis (with consent), local evidence, the shared review
/// dialog in save mode, then a new flattened PNG beside the original. The original is never modified.
/// Unlike <see cref="PrivacyUploadGuard"/> nothing leaves the computer except the AI scan the user allows, so this does not
/// fail closed: without AI it still offers local evidence and manual review.
/// Never log image content, OCR text, model replies or file names here.
/// </summary>
public sealed class PrivacyRedactionService
{
    private const long MaxPixels = 100_000_000;
    private const long MaxFileBytes = 100_000_000;

    private readonly IPrivacyReviewService _review;
    private readonly Func<IPrivacyAnalysisProvider?>? _providerFactory;
    private readonly IOcrService? _ocr;
    private readonly TaskSettingsPrivacy _policy;

    public PrivacyRedactionService(IPrivacyReviewService review, Func<IPrivacyAnalysisProvider?>? providerFactory,
        IOcrService? ocr, TaskSettingsPrivacy? policy = null)
    {
        _review = review;
        _providerFactory = providerFactory;
        _ocr = ocr;
        _policy = Clone(policy ?? CurrentPolicy());
    }

    /// <summary>The service the GUI host composed, or null when there is no review dialog (a headless host).</summary>
    public static PrivacyRedactionService? CreateDefault()
    {
        IPrivacyReviewService? review = PrivacyServices.ReviewService;
        return review == null ? null : new PrivacyRedactionService(review, PrivacyServices.RedactionAnalysisProviderFactory,
            PlatformServices.IsInitialized ? PlatformServices.Ocr : null);
    }

    /// <summary>
    /// The categories, effect and margins the user set for Privacy mode on the default workflow; the defaults when unset.
    /// The tool uses them whether or not Privacy mode is switched on for uploads.
    /// </summary>
    public static TaskSettingsPrivacy CurrentPolicy()
    {
        try
        {
            return SettingsManager.DefaultTaskSettings?.PrivacySettings ?? new TaskSettingsPrivacy();
        }
        catch (Exception)
        {
            return new TaskSettingsPrivacy();
        }
    }

    public static bool IsSupportedFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) &&
        (Common.FileHelpers.IsImageFile(path) || string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase));

    /// <summary>Redacts a still image file. The copy goes in the same folder as <paramref name="path"/>.</summary>
    public async Task<PrivacyRedactionResult> RedactFileAsync(string path, CancellationToken token)
    {
        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (folder == null) return new(PrivacyRedactionOutcome.UnsupportedImage);
        using SKBitmap? source = LoadStillImage(path);
        if (source == null) return new(PrivacyRedactionOutcome.UnsupportedImage);
        return await RedactAsync(source, folder, Path.GetFileNameWithoutExtension(path), token);
    }

    /// <summary>
    /// Redacts <paramref name="image"/> (not modified or disposed) and saves the approved copy as
    /// <c>&lt;baseName&gt;-redacted.png</c> in <paramref name="folder"/>, or the first free numbered name after it.
    /// </summary>
    public async Task<PrivacyRedactionResult> RedactAsync(SKBitmap image, string folder, string baseName, CancellationToken token)
    {
        if (image.Width <= 0 || image.Height <= 0 || (long)image.Width * image.Height > MaxPixels)
            return new(PrivacyRedactionOutcome.UnsupportedImage);

        string? written = null;
        try
        {
            using SKBitmap source = image.Copy() ?? throw new InvalidOperationException("Could not copy image.");
            PrivacyScanResult scan = await PrivacyScanService.AnalyzeWithConsentAsync(_providerFactory?.Invoke(), _review, source,
                _policy, token);
            token.ThrowIfCancellationRequested();
            scan = await PrivacyScanService.AddLocalEvidenceAsync(source, _policy, scan, _ocr, token);
            using SKBitmap candidate = RegionRedactionRenderer.Render(source, scan.Findings, _policy);
            PrivacyReviewResult result = await _review.ReviewSaveAsync(source, candidate, scan, _policy, token);
            using SKBitmap? approved = result.Image;
            if (result.Decision != PrivacyReviewDecision.Save || approved == null) return new(PrivacyRedactionOutcome.Cancelled);

            // Flattened PNG pixels only: no editable layers or source metadata reach the copy.
            byte[] bytes = RegionRedactionRenderer.EncodePng(approved);
            Directory.CreateDirectory(folder);
            // Only a file this call created may be removed on failure, so the path is kept once the create succeeded.
            await using FileStream file = CreateFreeFile(folder, baseName, out string createdPath);
            written = createdPath;
            await file.WriteAsync(bytes, token);
            return new(PrivacyRedactionOutcome.Saved, written);
        }
        catch (OperationCanceledException)
        {
            Remove(written);
            return new(PrivacyRedactionOutcome.Cancelled);
        }
        catch (Exception)
        {
            Remove(written);
            return new(PrivacyRedactionOutcome.Failed);
        }
    }

    /// <summary>The first of <c>name-redacted.png</c>, <c>name-redacted-2.png</c>, ... that does not exist, created exclusively.</summary>
    internal static FileStream CreateFreeFile(string folder, string baseName, out string path)
    {
        for (int i = 1; i <= 10_000; i++)
        {
            path = Path.Combine(folder, i == 1 ? $"{baseName}-redacted.png" : $"{baseName}-redacted-{i}.png");
            if (File.Exists(path)) continue;
            try
            {
                return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Created by someone else between the check and the create; try the next name.
            }
        }
        throw new IOException("No free file name for the redacted copy.");
    }

    private static SKBitmap? LoadStillImage(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes) return null;
            using SKCodec? codec = SKCodec.Create(path);
            if (codec == null || codec.FrameCount > 1 || codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
                (long)codec.Info.Width * codec.Info.Height > MaxPixels)
                return null;
            return SKBitmap.Decode(codec);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Remove(string? path)
    {
        if (path == null) return;
        try { File.Delete(path); } catch (Exception) { /* Best effort; never touches the original. */ }
    }

    private static TaskSettingsPrivacy Clone(TaskSettingsPrivacy policy) =>
        JsonSerializer.Deserialize<TaskSettingsPrivacy>(JsonSerializer.Serialize(policy)) ?? new TaskSettingsPrivacy();
}
