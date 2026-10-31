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
using System.Threading;
using System.Threading.Tasks;

namespace XerahS.Common;

public static class VideoEditorFfprobeResolver
{
    public static async Task<string> EnsureAvailableAsync(
        string ffmpegPath,
        Action<string>? messageSink = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedFfmpegPath = VideoEditorFfmpegResolver.NormalizePath(ffmpegPath);
        if (string.IsNullOrWhiteSpace(normalizedFfmpegPath))
        {
            throw new ArgumentException("FFmpeg path was not provided.", nameof(ffmpegPath));
        }

        string existingProbePath = ResolvePath(normalizedFfmpegPath);
        if (!string.IsNullOrWhiteSpace(existingProbePath))
        {
            return existingProbePath;
        }

        string downloadFolder = Path.GetDirectoryName(normalizedFfmpegPath) ?? PathsManager.ToolsArchitectureFolder;
        messageSink?.Invoke($"FFprobe missing. Downloading the FFmpeg package to: {downloadFolder}");

        FFmpegDownloadResult downloadResult = await FFmpegDownloader.DownloadLatestAsync(
            downloadFolder,
            cancellationToken: cancellationToken);

        string downloadedProbePath = ResolvePath(normalizedFfmpegPath);
        if (!string.IsNullOrWhiteSpace(downloadedProbePath))
        {
            return downloadedProbePath;
        }

        string detail = downloadResult.ErrorMessage ?? "FFprobe could not be downloaded or located.";
        messageSink?.Invoke(detail);
        throw new InvalidOperationException(detail);
    }

    public static string ResolvePath(string ffmpegPath)
    {
        string normalizedFfmpegPath = VideoEditorFfmpegResolver.NormalizePath(ffmpegPath);
        if (!string.IsNullOrWhiteSpace(normalizedFfmpegPath))
        {
            string siblingProbePath = Path.Combine(
                Path.GetDirectoryName(normalizedFfmpegPath) ?? string.Empty,
                OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
            if (File.Exists(siblingProbePath))
            {
                return siblingProbePath;
            }
        }

        string detectedProbePath = PathsManager.GetFFprobePath();
        return VideoEditorFfmpegResolver.NormalizePath(detectedProbePath);
    }
}
