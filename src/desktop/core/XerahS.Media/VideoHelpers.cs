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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XerahS.Common;

namespace XerahS.Media
{
    public class VideoHelpers
    {
        private FFmpegCLIManager _ffmpegManager;

        public VideoHelpers(string? ffmpegPath = null)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                ffmpegPath = PathsManager.GetFFmpegPath();
            }
            _ffmpegManager = new FFmpegCLIManager(ffmpegPath);
        }

        public VideoInfo? GetVideoInfo(string inputPath)
        {
            return _ffmpegManager.GetVideoInfo(inputPath);
        }

        /// <summary>
        /// Convert video to GIF using FFmpeg with palette generation for high quality.
        /// </summary>
        /// <param name="inputPath">Path to input video file</param>
        /// <param name="outputPath">Path to output GIF file</param>
        /// <param name="fps">Frame rate (default 15)</param>
        /// <param name="width">Width (default -1 to keep aspect ratio)</param>
        /// <param name="statsMode">palettegen stats_mode (default: full)</param>
        /// <param name="dither">paletteuse dither (default: sierra2_4a)</param>
        /// <param name="bayerScale">paletteuse bayer_scale (default: 2)</param>
        /// <param name="paletteNew">palettegen new=1 when stats_mode=single</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> ConvertToGifAsync(
            string inputPath,
            string outputPath,
            int fps = 15,
            int width = -1,
            string? statsMode = null,
            string? dither = null,
            int bayerScale = 2,
            bool paletteNew = false)
        {
            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            {
                DebugHelper.WriteLine("ConvertToGif: Input file not found: " + inputPath);
                return false;
            }

            string ffmpegPath = _ffmpegManager.FFmpegPath;
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                DebugHelper.WriteLine("ConvertToGif: FFmpeg path not found.");
                return false;
            }

            try
            {
                // Match ShareX palette defaults (stats_mode=full, dither=sierra2_4a, bayer_scale=2).
                // https://ffmpeg.org/ffmpeg-filters.html#palettegen-1
                // https://ffmpeg.org/ffmpeg-filters.html#paletteuse
                // https://ffmpeg.org/ffmpeg-filters.html#mpdecimate
                statsMode = string.IsNullOrWhiteSpace(statsMode) ? "full" : statsMode;
                dither = string.IsNullOrWhiteSpace(dither) ? "bayer" : dither;

                string preProcess = $"fps={fps}";
                if (width > 0)
                {
                    preProcess += $",scale={width}:-1:flags=lanczos";
                }

                string paletteGen = $"palettegen=stats_mode={statsMode}";
                if (paletteNew)
                {
                    paletteGen += ":new=1";
                }

                string paletteUse = $"paletteuse=dither={dither}";
                if (string.Equals(dither, "bayer", StringComparison.OrdinalIgnoreCase))
                {
                    paletteUse += $":bayer_scale={bayerScale}";
                }

                string filterComplex = $"[0:v] {preProcess},split [a][b];[a] {paletteGen} [p];[b][p] {paletteUse},mpdecimate";

                var args = $"-i \"{inputPath}\" -filter_complex \"{filterComplex}\" \"{outputPath}\" -y";

                DebugHelper.WriteLine($"ConvertToGif executing: {ffmpegPath} {args}");

                var tcs = new TaskCompletionSource<bool>();

                using (var process = new Process())
                {
                    process.StartInfo.FileName = ffmpegPath;
                    // Use command line argument string directly. 
                    // Note: quoting in Process.StartInfo.Arguments can be tricky on Windows vs Core.
                    // But here we are constructing the full string.
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.EnableRaisingEvents = true;
                    process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode == 0);

                    process.OutputDataReceived += (s, e) => { if (e.Data != null) DebugHelper.WriteLine($"FFmpeg out: {e.Data}"); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) DebugHelper.WriteLine($"FFmpeg err: {e.Data}"); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await tcs.Task;
                }

                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex);
                return false;
            }
        }
    }
}
