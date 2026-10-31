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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Services.Privacy;

internal sealed record PrivacyUploadApproval(byte[] Bytes, string ByteHash, string ImageHash, string PolicyHash, string ProviderIdentity,
    string DestinationIdentity, string FilePath, PrivacyScanResult Scan);

/// <summary>One gate for capture, existing files, retries, and each actual destination.</summary>
public static class PrivacyUploadGuard
{
    public static bool IsRequired(TaskInfo info) => info.TaskSettings.PrivacySettings?.Enabled == true && info.IsUploadJob &&
        (info.DataType == EDataType.Image ||
         (info.DataType == EDataType.File && IsStillImagePath(info.FilePath)) ||
         (info.DataType == EDataType.Default && info.Metadata?.Image != null &&
          (string.IsNullOrEmpty(info.FilePath) || IsStillImagePath(info.FilePath))));

    private static bool IsStillImagePath(string path) => Common.FileHelpers.IsImageFile(path) ||
        string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase);

    public static async Task<bool> PrepareAsync(TaskInfo info, CancellationToken token)
    {
        if (!IsRequired(info)) return true;
        token.ThrowIfCancellationRequested();
        try
        {
            var provider = PrivacyServices.AnalysisProviderFactory?.Invoke();
            string identity = provider?.Identity ?? "manual";
            if (IsCurrent(info, identity)) return true;
            info.PrivacyApproval = null;
            info.PrivacyReviewRequired = false;
            var review = PrivacyServices.ReviewService;
            if (review == null) return Block(info);
            using var source = LoadSource(info);
            if (source == null || source.Width <= 0 || source.Height <= 0 || (long)source.Width * source.Height > 100_000_000)
                return Block(info);
            // Freeze policy before awaiting consent or inference.
            string policyHash = PolicyHash(info);
            var policy = JsonSerializer.Deserialize<TaskSettingsPrivacy>(JsonSerializer.Serialize(info.TaskSettings.PrivacySettings))!;
            PrivacyScanResult scan = PrivacyScanResult.Unsupported;
            if (provider != null && (!provider.RequiresCloudConsent || await review.ConfirmCloudAnalysisAsync(provider.DisplayName, token)))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                try { scan = await provider.AnalyzeAsync(source, policy, timeout.Token); }
                catch (OperationCanceledException) { token.ThrowIfCancellationRequested(); scan = new(PrivacyScanStatus.Incomplete, []); }
                catch { scan = new(PrivacyScanStatus.Error, []); }
                if (scan.Status == PrivacyScanStatus.Cancelled) { token.ThrowIfCancellationRequested(); scan = new(PrivacyScanStatus.Incomplete, []); }
            }
            token.ThrowIfCancellationRequested();
            scan = await PrivacyScanService.AddLocalEvidenceAsync(source, policy, scan,
                PlatformServices.IsInitialized ? PlatformServices.Ocr : null, token);
            using var candidate = RegionRedactionRenderer.Render(source, scan.Findings, policy);
            var destination = ResolveInitialDestination(info);
            var result = await review.ReviewAsync(source, candidate, scan, DestinationLabel(destination), policy, token);
            using var approved = result.Image;
            if (approved == null || result.Decision == PrivacyReviewDecision.Cancel) return Block(info);
            if (policyHash != PolicyHash(info) || identity != (PrivacyServices.AnalysisProviderFactory?.Invoke()?.Identity ?? "manual"))
                return Block(info);
            byte[] bytes = Encode(approved);
            string folder = TaskHelpers.GetScreenshotsFolder(info.TaskSettings, new TaskMetadata());
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"private-{Guid.NewGuid():N}.png");
            // Exclusive creation publishes only flattened PNG pixels, never editable source or metadata.
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.WriteAsync(bytes, token);
            var flattened = SKBitmap.Decode(bytes) ?? throw new InvalidOperationException("Could not decode redacted image.");
            info.Metadata ??= new TaskMetadata();
            info.Metadata.Image?.Dispose();
            info.Metadata.Image = flattened;
            info.Metadata.WindowTitle = null;
            info.Metadata.ProcessName = null;
            info.Metadata.OcrText = null;
            info.ThumbnailFilePath = "";
            info.FilePath = path;
            info.DataType = EDataType.Image;
            var reviewedScan = scan.Status == PrivacyScanStatus.Complete ? scan : new PrivacyScanResult(PrivacyScanStatus.ManuallyReviewed, []);
            info.PrivacyApproval = new(bytes, Hash(bytes), Hash(Encode(flattened)), policyHash, identity, DestinationIdentity(destination), path, reviewedScan);
            if (result.Decision == PrivacyReviewDecision.KeepLocal)
            {
                info.TaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.UploadImageToHost;
                info.Status = "Kept locally";
                return false;
            }
            return true;
        }
        catch (OperationCanceledException) { token.ThrowIfCancellationRequested(); return Block(info); }
        catch { return Block(info); } // Never log screenshots, OCR, provider payloads or sensitive filenames.
    }

    public static async Task<bool> ConfirmDestinationAsync(TaskInfo info, UploaderInstance destination, CancellationToken token)
    {
        if (!IsRequired(info)) return true;
        string identity = PrivacyServices.AnalysisProviderFactory?.Invoke()?.Identity ?? "manual";
        if (!IsCurrent(info, identity)) return Block(info);
        var approval = info.PrivacyApproval!;
        if (approval.DestinationIdentity == DestinationIdentity(destination)) return true;
        var review = PrivacyServices.ReviewService;
        if (review == null || info.Metadata.Image == null) return Block(info);
        // A different fallback host needs its own approval, but no duplicate cloud scan.
        var result = await review.ReviewAsync(info.Metadata.Image, info.Metadata.Image, approval.Scan,
            DestinationLabel(destination), info.TaskSettings.PrivacySettings, token);
        using var returnedImage = result.Image;
        if (result.Decision != PrivacyReviewDecision.Upload || returnedImage == null || !IsCurrent(info, identity)) return Block(info);
        if (Hash(Encode(returnedImage)) != approval.ImageHash)
        {
            // Manual edits approved in this new destination's review replace the earlier approval.
            byte[] bytes = Encode(returnedImage);
            string path = Path.Combine(Path.GetDirectoryName(approval.FilePath)!, $"private-{Guid.NewGuid():N}.png");
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await file.WriteAsync(bytes, token);
            var flattened = SKBitmap.Decode(bytes) ?? throw new InvalidOperationException("Could not encode reviewed image.");
            info.Metadata.Image.Dispose();
            info.Metadata.Image = flattened;
            info.FilePath = path;
            approval = approval with { Bytes = bytes, ByteHash = Hash(bytes), ImageHash = Hash(Encode(flattened)), FilePath = path,
                Scan = new(PrivacyScanStatus.ManuallyReviewed, []) };
        }
        info.PrivacyApproval = approval with { DestinationIdentity = DestinationIdentity(destination) };
        return true;
    }

    public static Stream OpenApprovedContent(TaskInfo info, UploaderInstance destination)
    {
        string identity = PrivacyServices.AnalysisProviderFactory?.Invoke()?.Identity ?? "manual";
        if (!IsCurrent(info, identity) || info.PrivacyApproval!.DestinationIdentity != DestinationIdentity(destination))
            throw new InvalidOperationException("Privacy review required: content, policy, provider or destination changed.");
        // Own a private copy. Neither file replacement nor a plugin can mutate approved bytes for retries.
        return new MemoryStream(info.PrivacyApproval.Bytes.ToArray(), writable: false);
    }

    private static SKBitmap? LoadSource(TaskInfo info)
    {
        // Validate the complete file even when a caller preloaded its first-frame preview.
        if (!string.IsNullOrEmpty(info.FilePath) && new FileInfo(info.FilePath).Length > 100_000_000) return null;
        using var codec = string.IsNullOrEmpty(info.FilePath) ? null : SKCodec.Create(info.FilePath);
        if (!string.IsNullOrEmpty(info.FilePath) &&
            (codec == null || codec.FrameCount > 1 ||
             codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > 100_000_000))
            return null;
        var image = info.Metadata?.Image;
        if (image != null)
            return image.Width > 0 && image.Height > 0 && (long)image.Width * image.Height <= 100_000_000 ? image.Copy() : null;
        return codec == null ? null : SKBitmap.Decode(codec);
    }

    private static bool IsCurrent(TaskInfo info, string providerIdentity)
    {
        var approval = info.PrivacyApproval;
        if (approval == null || approval.PolicyHash != PolicyHash(info) || approval.ProviderIdentity != providerIdentity ||
            approval.FilePath != info.FilePath || info.Metadata.Image == null || Hash(Encode(info.Metadata.Image)) != approval.ImageHash)
            return false;
        try
        {
            using var stream = new FileStream(info.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream)) == approval.ByteHash;
        }
        catch { return false; }
    }

    private static UploaderInstance? ResolveInitialDestination(TaskInfo info)
    {
        var manager = InstanceManager.Instance;
        string? id = info.TaskSettings.GetDestinationInstanceIdForDataType(EDataType.Image);
        var instance = !string.IsNullOrWhiteSpace(id) ? manager.GetInstance(id) :
            manager.GetDefaultInstance(UploaderCategory.Image) ?? manager.GetDefaultInstance(UploaderCategory.File);
        return instance != null && InstanceManager.IsAutoProvider(instance.ProviderId)
            ? manager.ResolveAutoInstance(instance.Category, instance.InstanceId) : instance;
    }

    private static string DestinationLabel(UploaderInstance? instance) => instance == null
        ? "Configured upload destination (confirmation required when selected)"
        : $"{instance.DisplayName} ({instance.ProviderId})";
    private static string DestinationIdentity(UploaderInstance? instance) => instance == null ? "unresolved" :
        Hash(Encoding.UTF8.GetBytes($"{instance.InstanceId}|{instance.ProviderId}|{instance.SettingsJson}"));
    private static string PolicyHash(TaskInfo info) => Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(info.TaskSettings.PrivacySettings)));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static byte[] Encode(SKBitmap bitmap) => RegionRedactionRenderer.EncodePng(bitmap);
    private static bool Block(TaskInfo info)
    {
        info.PrivacyReviewRequired = true;
        info.Status = "Review required";
        info.Result = new XerahS.Uploaders.UploadResult { IsSuccess = false, Response = "Privacy review required. Open XerahS to review or annotate before uploading." };
        return false;
    }
}
