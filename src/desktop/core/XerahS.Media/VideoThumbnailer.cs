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

using XerahS.Common;
using SkiaSharp;
using System.Diagnostics;

namespace XerahS.Media
{
    public class VideoThumbnailer
    {
        public delegate void ProgressChangedEventHandler(int current, int length);
        public event ProgressChangedEventHandler? ProgressChanged;

        public string FFmpegPath { get; private set; }
        public VideoThumbnailOptions Options { get; private set; }
        public string MediaPath { get; private set; } = string.Empty;
        public VideoInfo? VideoInfo { get; private set; }

        public VideoThumbnailer(string ffmpegPath, VideoThumbnailOptions options)
        {
            FFmpegPath = ffmpegPath;
            Options = options;
        }

        private void UpdateVideoInfo()
        {
            using (FFmpegCLIManager ffmpeg = new FFmpegCLIManager(FFmpegPath))
            {
                VideoInfo = ffmpeg.GetVideoInfo(MediaPath);
            }
        }

        public List<VideoThumbnailInfo> TakeThumbnails(string mediaPath)
        {
            MediaPath = mediaPath;

            UpdateVideoInfo();

            if (VideoInfo == null || VideoInfo.Duration == TimeSpan.Zero)
            {
                return new List<VideoThumbnailInfo>();
            }

            List<VideoThumbnailInfo> tempThumbnails = new List<VideoThumbnailInfo>();

            for (int i = 0; i < Options.ThumbnailCount; i++)
            {
                string mediaFileName = Path.GetFileNameWithoutExtension(MediaPath);

                int timeSliceElapsed;

                if (Options.RandomFrame)
                {
                    timeSliceElapsed = GetRandomTimeSlice(i);
                }
                else
                {
                    timeSliceElapsed = GetTimeSlice(Options.ThumbnailCount) * (i + 1);
                }

                string fileName = string.Format("{0}-{1}.{2}", mediaFileName, timeSliceElapsed, EnumExtensions.GetDescription(Options.ImageFormat));
                string tempThumbnailPath = Path.Combine(GetOutputDirectory(), fileName);

                FileHelpers.DeleteFile(tempThumbnailPath);
                bool thumbnailCreated = false;

                using (Process process = new Process())
                {
                    ProcessStartInfo psi = new ProcessStartInfo()
                    {
                        FileName = FFmpegPath,
                        Arguments = $"-ss {timeSliceElapsed} -i \"{MediaPath}\" -f image2 -vframes 1 -y \"{tempThumbnailPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = psi;
                    process.Start();
                    thumbnailCreated = WaitForExitOrKill(process, TimeSpan.FromSeconds(30)) && process.ExitCode == 0 && File.Exists(tempThumbnailPath);
                }

                if (thumbnailCreated)
                {
                    VideoThumbnailInfo screenshotInfo = new VideoThumbnailInfo(tempThumbnailPath)
                    {
                        Timestamp = TimeSpan.FromSeconds(timeSliceElapsed)
                    };

                    tempThumbnails.Add(screenshotInfo);
                }
                else
                {
                    FileHelpers.DeleteFile(tempThumbnailPath);
                }

                OnProgressChanged(i + 1, Options.ThumbnailCount);
            }

            return Finish(tempThumbnails);
        }

        private List<VideoThumbnailInfo> Finish(List<VideoThumbnailInfo> tempThumbnails)
        {
            List<VideoThumbnailInfo> thumbnails = new List<VideoThumbnailInfo>();

            if (tempThumbnails != null && tempThumbnails.Count > 0)
            {
                if (Options.CombineScreenshots)
                {
                    var img = CombineScreenshots(tempThumbnails);
                    if (img != null)
                    {
                        using (img)
                        {
                            string tempFilePath = Path.Combine(GetOutputDirectory(), Path.GetFileNameWithoutExtension(MediaPath) + Options.FilenameSuffix + "." + EnumExtensions.GetDescription(Options.ImageFormat));
                            ImageHelpers.SaveBitmap(img, tempFilePath);
                            thumbnails.Add(new VideoThumbnailInfo(tempFilePath));
                        }
                    }

                    if (!Options.KeepScreenshots)
                    {
                        tempThumbnails.ForEach(x => File.Delete(x.FilePath));
                    }
                }
                else
                {
                    thumbnails.AddRange(tempThumbnails);
                }

                if (Options.OpenDirectory && thumbnails.Count > 0)
                {
                    FileHelpers.OpenFolderWithFile(thumbnails[0].FilePath);
                }
            }

            return thumbnails;
        }

        protected void OnProgressChanged(int current, int length)
        {
            ProgressChanged?.Invoke(current, length);
        }

        private string GetOutputDirectory()
        {
            string directory = Options.DefaultOutputDirectory ?? string.Empty;

            switch (Options.OutputLocation)
            {
                default:
                case ThumbnailLocationType.DefaultFolder:
                    directory = Options.DefaultOutputDirectory ?? string.Empty;
                    break;
                case ThumbnailLocationType.ParentFolder:
                    directory = Path.GetDirectoryName(MediaPath) ?? string.Empty;
                    break;
                case ThumbnailLocationType.CustomFolder:
                    directory = FileHelpers.ExpandFolderVariables(Options.CustomOutputDirectory) ?? string.Empty;
                    break;
            }

            FileHelpers.CreateDirectory(directory);

            return directory;
        }

        private int GetTimeSlice(int count)
        {
            return (int)(VideoInfo!.Duration.TotalSeconds / count);
        }

        private int GetRandomTimeSlice(int thumbnailIndex)
        {
            int thumbnailCount = Math.Max(1, Options.ThumbnailCount);
            int totalSlots = thumbnailCount + 2;
            int timeSlice = GetTimeSlice(totalSlots);

            if (timeSlice <= 0)
            {
                return 0;
            }

            int slot = Math.Clamp(thumbnailIndex, 0, thumbnailCount - 1);
            int start = timeSlice * (slot + 1);
            int end = timeSlice * (slot + 2) - 1;

            return RandomFast.Next(start, Math.Max(start, end));
        }

        private static bool WaitForExitOrKill(Process process, TimeSpan timeout)
        {
            if (process.WaitForExit(timeout))
            {
                return true;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException
                                          or ObjectDisposedException
                                          or System.ComponentModel.Win32Exception
                                          or AggregateException)
            {
            }

            process.WaitForExit();
            return false;
        }

        private sealed class LoadedThumbnail : IDisposable
        {
            public LoadedThumbnail(SKBitmap image, TimeSpan timestamp)
            {
                Image = image;
                Timestamp = timestamp;
            }

            public SKBitmap Image { get; }
            public TimeSpan Timestamp { get; }

            public void Dispose()
            {
                Image.Dispose();
            }
        }

        private List<LoadedThumbnail> LoadThumbnailImages(List<VideoThumbnailInfo> thumbnails)
        {
            List<LoadedThumbnail> images = new List<LoadedThumbnail>();

            foreach (VideoThumbnailInfo thumbnail in thumbnails)
            {
                SKBitmap? bmp = ImageHelpers.LoadBitmap(thumbnail.FilePath);

                if (bmp != null)
                {
                    if (Options.MaxThumbnailWidth > 0 && bmp.Width > Options.MaxThumbnailWidth)
                    {
                        int maxThumbnailHeight = (int)((float)Options.MaxThumbnailWidth / bmp.Width * bmp.Height);
                        SKBitmap resized = ImageHelpers.ResizeImage(bmp, Options.MaxThumbnailWidth, maxThumbnailHeight);
                        bmp.Dispose();
                        bmp = resized;
                    }

                    images.Add(new LoadedThumbnail(bmp, thumbnail.Timestamp));
                }
            }

            return images;
        }

        private SKBitmap? CombineScreenshots(List<VideoThumbnailInfo> thumbnails)
        {
            List<LoadedThumbnail> images = new List<LoadedThumbnail>();
            SKBitmap? finalImage = null;

            try
            {
                string infoString = "";
                int infoStringHeight = 0;

                using (SKPaint fontPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
                using (SKFont infoFont = new SKFont(SKTypeface.FromFamilyName("Arial"), 12))
                {
                    SKFontMetrics infoMetrics = infoFont.Metrics;

                    if (Options.AddVideoInfo)
                    {
                        infoString = VideoInfo?.ToString() ?? string.Empty;
                        infoStringHeight = (int)Math.Ceiling(infoMetrics.Descent - infoMetrics.Ascent) + 5;
                    }

                    images = LoadThumbnailImages(thumbnails);

                    if (images.Count == 0) return null;

                    int columnCount = Math.Max(1, Options.ColumnCount);
                    int padding = Math.Max(0, Options.Padding);
                    int spacing = Math.Max(0, Options.Spacing);

                    int thumbWidth = images.Max(x => x.Image.Width);

                    int width = (padding * 2) +
                                (thumbWidth * columnCount) +
                                ((columnCount - 1) * spacing);

                    if (Options.MaxCombinedWidth > 0 && width > Options.MaxCombinedWidth)
                    {
                        DebugHelper.WriteLine($"VideoThumbnailer: Combined thumbnail width {width} exceeds limit {Options.MaxCombinedWidth}, returning null.");
                        foreach (var img in images) img.Image.Dispose();
                        return null;
                    }

                    int rowCount = (int)Math.Ceiling(images.Count / (float)columnCount);

                    int thumbHeight = images.Max(x => x.Image.Height);

                    int height = (padding * 3) +
                                 infoStringHeight +
                                 (thumbHeight * rowCount) +
                                 ((rowCount - 1) * spacing);

                    if (Options.MaxCombinedHeight > 0 && height > Options.MaxCombinedHeight)
                    {
                        DebugHelper.WriteLine($"VideoThumbnailer: Combined thumbnail height {height} exceeds limit {Options.MaxCombinedHeight}, returning null.");
                        foreach (var img in images) img.Image.Dispose();
                        return null;
                    }

                    finalImage = new SKBitmap(width, height);

                    using (SKCanvas g = new SKCanvas(finalImage))
                    {
                        g.Clear(SKColors.WhiteSmoke);

                        if (!string.IsNullOrEmpty(infoString))
                        {
                            float infoBaseline = padding - infoMetrics.Ascent;
                            g.DrawText(infoString, padding, infoBaseline, SKTextAlign.Left, infoFont, fontPaint);
                        }

                        int i = 0;
                        int offsetY = (padding * 2) + infoStringHeight;

                        using (SKPaint shadowPaint = new SKPaint { Color = new SKColor(0, 0, 0, 75) })
                        using (SKPaint borderPaint = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1 })
                        using (SKPaint timestampPaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
                        using (SKFont timestampFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 10))
                        {
                            SKFontMetrics timestampMetrics = timestampFont.Metrics;

                            for (int y = 0; y < rowCount; y++)
                            {
                                int offsetX = padding;

                                for (int x = 0; x < columnCount; x++)
                                {
                                    LoadedThumbnail thumbnail = images[i];

                                    if (Options.DrawShadow)
                                    {
                                        int shadowOffset = 3;
                                        g.DrawRect(offsetX + shadowOffset, offsetY + shadowOffset, thumbnail.Image.Width, thumbnail.Image.Height, shadowPaint);
                                    }

                                    g.DrawBitmap(thumbnail.Image, offsetX, offsetY, SKSamplingOptions.Default);

                                    if (Options.DrawBorder)
                                    {
                                        g.DrawRect(offsetX, offsetY, thumbnail.Image.Width - 1, thumbnail.Image.Height - 1, borderPaint);
                                    }

                                    if (Options.AddTimestamp)
                                    {
                                        int timestampOffset = 10;
                                        string timestampText = thumbnail.Timestamp.ToString();
                                        float timestampBaseline = offsetY + timestampOffset - timestampMetrics.Ascent;
                                        g.DrawText(timestampText, offsetX + timestampOffset, timestampBaseline, SKTextAlign.Left, timestampFont, timestampPaint);
                                    }

                                    i++;

                                    if (i >= images.Count)
                                    {
                                        return finalImage;
                                    }

                                    offsetX += thumbWidth + spacing;
                                }

                                offsetY += thumbHeight + spacing;
                            }
                        }
                    }
                }

                return finalImage;
            }
            catch
            {
                if (finalImage != null)
                {
                    finalImage.Dispose();
                }

                throw;
            }
            finally
            {
                foreach (LoadedThumbnail image in images)
                {
                    if (image != null)
                    {
                        image.Dispose();
                    }
                }
            }
        }
    }
}
