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

namespace XerahS.Common
{
    public sealed class FFmpegDownloadResult
    {
        public bool Success { get; }
        public string? FFmpegPath { get; }
        public string? ErrorMessage { get; }
        public string? ExpectedDownloadUrl { get; }

        private FFmpegDownloadResult(bool success, string? ffmpegPath, string? errorMessage, string? expectedDownloadUrl)
        {
            Success = success;
            FFmpegPath = ffmpegPath;
            ErrorMessage = errorMessage;
            ExpectedDownloadUrl = expectedDownloadUrl;
        }

        public static FFmpegDownloadResult CreateSuccess(string ffmpegPath) => new FFmpegDownloadResult(true, ffmpegPath, null, null);

        public static FFmpegDownloadResult CreateFailure(string errorMessage, string? expectedDownloadUrl = null)
            => new FFmpegDownloadResult(false, null, errorMessage, expectedDownloadUrl);
    }

    public static class FFmpegDownloader
    {
        public const string DefaultOwner = "BtbN";
        public const string DefaultRepo = "FFmpeg-Builds";

        public static Task<FFmpegDownloadResult> DownloadLatestToToolsAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
            => DownloadLatestAsync(PathsManager.ToolsArchitectureFolder, progress, cancellationToken);

        public static async Task<FFmpegDownloadResult> DownloadLatestAsync(string destinationFolder, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destinationFolder))
                return FFmpegDownloadResult.CreateFailure("Destination folder was not provided.");
            if (cancellationToken.IsCancellationRequested)
                return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");

            var checker = new FFmpegUpdateChecker();
            if (checker.GetExpectedAssetName() == null)
            {
                string guidance = OperatingSystem.IsMacOS()
                    ? "Install FFmpeg and ffprobe with Homebrew (brew install ffmpeg), or configure a custom executable path."
                    : "Install FFmpeg and ffprobe for your operating system and CPU, then configure a custom executable path.";
                return FFmpegDownloadResult.CreateFailure(
                    $"BtbN does not publish FFmpeg builds for this platform ({checker.Architecture}). {guidance}",
                    checker.GetExpectedDownloadUrl());
            }

            string workFolder = Path.Combine(Path.GetTempPath(), $"xerahs-ffmpeg-{Guid.NewGuid():N}");
            Action? detachProgressHandlers = null;
            try
            {
                string? downloadUrl = await checker.GetLatestDownloadURL(true, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(downloadUrl) || checker.FileName == null || checker.ChecksumsURL == null)
                    return FFmpegDownloadResult.CreateFailure("The BtbN FFmpeg build and checksum manifest could not be resolved.", checker.GetExpectedDownloadUrl());

                byte[] checksums = await HttpClientFactory.Create().GetByteArrayAsync(checker.ChecksumsURL, cancellationToken).ConfigureAwait(false);
                string? expectedHash = FFmpegReleaseVerifier.FindHash(checksums, checker.FileName);
                if (expectedHash == null)
                    return FFmpegDownloadResult.CreateFailure("The FFmpeg archive is missing from BtbN's checksum manifest and was not installed.", downloadUrl);

                Directory.CreateDirectory(workFolder);
                string archivePath = Path.Combine(workFolder, checker.FileName);
                var downloader = new FileDownloader(downloadUrl, archivePath);
                detachProgressHandlers = AttachProgressHandlers(downloader, progress);
                bool downloaded = await downloader.StartDownload(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!downloaded || !File.Exists(archivePath))
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download failed.", downloadUrl);

                string installedPath = await InstallArchiveAsync(archivePath, expectedHash, destinationFolder, checker.Architecture, cancellationToken).ConfigureAwait(false);
                progress?.Report(100);
                DebugHelper.WriteLine($"[FFmpeg] Installed {DefaultOwner}/{DefaultRepo} {checker.FileName} (SHA-256 verified) to {destinationFolder}");
                return FFmpegDownloadResult.CreateSuccess(installedPath);
            }
            catch (OperationCanceledException)
            {
                return FFmpegDownloadResult.CreateFailure("FFmpeg download was canceled.");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "FFmpeg download failed.");
                return FFmpegDownloadResult.CreateFailure($"FFmpeg download failed: {ex.Message}", checker.GetExpectedDownloadUrl());
            }
            finally
            {
                detachProgressHandlers?.Invoke();
                try { if (Directory.Exists(workFolder)) Directory.Delete(workFolder, recursive: true); }
                catch (Exception ex) { DebugHelper.WriteException(ex, "Could not remove the FFmpeg temporary download folder."); }
            }
        }

        internal static async Task<string> InstallArchiveAsync(string archivePath, string expectedHash, string destinationFolder,
            FFmpegArchitecture architecture, CancellationToken cancellationToken = default)
        {
            if (!await FFmpegReleaseVerifier.IsFileHashValidAsync(archivePath, expectedHash, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException("The FFmpeg archive does not match BtbN's SHA-256 checksum and was not installed.");
            return await FFmpegPackageInstaller.InstallAsync(archivePath, destinationFolder, architecture, cancellationToken).ConfigureAwait(false);
        }

        private static Action? AttachProgressHandlers(FileDownloader downloader, IProgress<double>? progress)
        {
            if (progress == null) return null;
            void ReportProgress()
            {
                if (downloader.FileSize > 0)
                    progress.Report(Math.Clamp(downloader.DownloadPercentage * 0.9, 0, 90));
            }
            downloader.FileSizeReceived += ReportProgress;
            downloader.ProgressChanged += ReportProgress;
            return () =>
            {
                downloader.FileSizeReceived -= ReportProgress;
                downloader.ProgressChanged -= ReportProgress;
            };
        }
    }
}
