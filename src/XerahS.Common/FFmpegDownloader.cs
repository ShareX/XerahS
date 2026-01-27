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

using System;
using System.IO;

namespace XerahS.Common
{
    public sealed class FFmpegDownloadResult
    {
        public bool Success { get; }
        public string? FFmpegPath { get; }
        public string? ErrorMessage { get; }

        private FFmpegDownloadResult(bool success, string? ffmpegPath, string? errorMessage)
        {
            Success = success;
            FFmpegPath = ffmpegPath;
            ErrorMessage = errorMessage;
        }

        public static FFmpegDownloadResult CreateSuccess(string ffmpegPath) => new FFmpegDownloadResult(true, ffmpegPath, null);

        public static FFmpegDownloadResult CreateFailure(string errorMessage) => new FFmpegDownloadResult(false, null, errorMessage);
    }

    public static class FFmpegDownloader
    {
        public const string DefaultOwner = "BtbN";
        public const string DefaultRepo = "FFmpeg-Builds";

        public static Task<FFmpegDownloadResult> DownloadLatestToToolsAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            return DownloadLatestAsync(PathsManager.ToolsFolder, progress, cancellationToken);
        }

        public static async Task<FFmpegDownloadResult> DownloadLatestAsync(string destinationFolder, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                return FFmpegDownloadResult.CreateFailure("Destination folder was not provided.");
            }

            Directory.CreateDirectory(destinationFolder);

            string? downloadedArchive = null;
            FileDownloader? downloader = null;
            Action? detachProgressHandlers = null;

            try
            {
                FFmpegUpdateChecker updateChecker = new FFmpegUpdateChecker(DefaultOwner, DefaultRepo, FFmpegUpdateChecker.ResolveArchitecture());
                string? downloadUrl = await updateChecker.GetLatestDownloadURL(true);

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download URL could not be resolved.");
                }

                string fileName = updateChecker.FileName ?? $"ffmpeg-{updateChecker.Architecture}.zip";
                downloadedArchive = Path.Combine(Path.GetTempPath(), fileName);

                downloader = new FileDownloader(downloadUrl, downloadedArchive);
                detachProgressHandlers = AttachProgressHandlers(downloader, progress);
                bool downloadSuccess = await downloader.StartDownload();
                progress?.Report(100);

                if (!downloadSuccess || !File.Exists(downloadedArchive))
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg download failed.");
                }

                ExtractFFmpegBinaries(downloadedArchive, destinationFolder);

                string ffmpegPath = PathsManager.GetFFmpegPath();

                if (string.IsNullOrWhiteSpace(ffmpegPath))
                {
                    return FFmpegDownloadResult.CreateFailure("FFmpeg was downloaded but could not be located.");
                }

                return FFmpegDownloadResult.CreateSuccess(ffmpegPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "FFmpeg download failed.");
                return FFmpegDownloadResult.CreateFailure("FFmpeg download failed.");
            }
            finally
            {
                detachProgressHandlers?.Invoke();

                if (!string.IsNullOrWhiteSpace(downloadedArchive) && File.Exists(downloadedArchive))
                {
                    try
                    {
                        File.Delete(downloadedArchive);
                    }
                    catch
                    {
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    DebugHelper.WriteLine("FFmpeg download was canceled.");
                }
            }
        }

        private static void ExtractFFmpegBinaries(string archivePath, string destinationFolder)
        {
            string ffmpegBinary = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            string ffprobeBinary = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

            ZipManager.Extract(
                archivePath,
                destinationFolder,
                retainDirectoryStructure: false,
                filter: entry =>
                    entry.Name.Equals(ffmpegBinary, StringComparison.OrdinalIgnoreCase) ||
                    entry.Name.Equals(ffprobeBinary, StringComparison.OrdinalIgnoreCase));

            string extractedFFmpegPath = Path.Combine(destinationFolder, ffmpegBinary);
            string extractedFFprobePath = Path.Combine(destinationFolder, ffprobeBinary);

            if (File.Exists(extractedFFmpegPath))
            {
                EnsureExecutable(extractedFFmpegPath);
            }

            if (File.Exists(extractedFFprobePath))
            {
                EnsureExecutable(extractedFFprobePath);
            }
        }

        private static void EnsureExecutable(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to set FFmpeg executable permissions.");
            }
        }

        private static Action? AttachProgressHandlers(FileDownloader downloader, IProgress<double>? progress)
        {
            if (progress == null)
            {
                return null;
            }

            void ReportProgress()
            {
                if (downloader.FileSize > 0)
                {
                    progress.Report(Math.Clamp(downloader.DownloadPercentage, 0, 100));
                }
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
