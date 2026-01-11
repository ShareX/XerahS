#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
        public event ProgressChangedEventHandler ProgressChanged;

        public string FFmpegPath { get; private set; }
        public VideoThumbnailOptions Options { get; private set; }
        public string MediaPath { get; private set; }
        public VideoInfo VideoInfo { get; private set; }

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
                return null;
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
                    process.WaitForExit(1000 * 30);
                }

                if (File.Exists(tempThumbnailPath))
                {
                    VideoThumbnailInfo screenshotInfo = new VideoThumbnailInfo(tempThumbnailPath)
                    {
                        Timestamp = TimeSpan.FromSeconds(timeSliceElapsed)
                    };

                    tempThumbnails.Add(screenshotInfo);
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
                    using (SKBitmap img = CombineScreenshots(tempThumbnails))
                    {
                        if (img != null)
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
            string directory;

            switch (Options.OutputLocation)
            {
                default:
                case ThumbnailLocationType.DefaultFolder:
                    directory = Options.DefaultOutputDirectory;
                    break;
                case ThumbnailLocationType.ParentFolder:
                    directory = Path.GetDirectoryName(MediaPath);
                    break;
                case ThumbnailLocationType.CustomFolder:
                    directory = FileHelpers.ExpandFolderVariables(Options.CustomOutputDirectory);
                    break;
            }

            FileHelpers.CreateDirectory(directory);

            return directory;
        }

        private int GetTimeSlice(int count)
        {
            return (int)(VideoInfo.Duration.TotalSeconds / count);
        }

        private int GetRandomTimeSlice(int start)
        {
            List<int> mediaSeekTimes = new List<int>();

            for (int i = 1; i < Options.ThumbnailCount + 2; i++)
            {
                mediaSeekTimes.Add(GetTimeSlice(Options.ThumbnailCount + 2) * i);
            }

            return (int)((RandomFast.NextDouble() * (mediaSeekTimes[start + 1] - mediaSeekTimes[start])) + mediaSeekTimes[start]);
        }

        private SKBitmap CombineScreenshots(List<VideoThumbnailInfo> thumbnails)
        {
            List<SKBitmap> images = new List<SKBitmap>();
            SKBitmap finalImage = null;

            try
            {
                string infoString = "";
                int infoStringHeight = 0;

                using (SKPaint fontPaint = new SKPaint { TextSize = 12, Typeface = SKTypeface.FromFamilyName("Arial"), Color = SKColors.Black, IsAntialias = true })
                {
                    if (Options.AddVideoInfo)
                    {
                        infoString = VideoInfo.ToString();
                        SKRect textBounds = new SKRect();
                        fontPaint.MeasureText(infoString, ref textBounds);
                        infoStringHeight = (int)textBounds.Height + 5; // Add some padding
                    }

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

                            images.Add(bmp);
                        }
                    }

                    if (images.Count == 0) return null;

                    int columnCount = Options.ColumnCount;

                    int thumbWidth = images[0].Width;

                    int width = (Options.Padding * 2) +
                                (thumbWidth * columnCount) +
                                ((columnCount - 1) * Options.Spacing);

                    int rowCount = (int)Math.Ceiling(images.Count / (float)columnCount);

                    int thumbHeight = images[0].Height;

                    int height = (Options.Padding * 3) +
                                 infoStringHeight +
                                 (thumbHeight * rowCount) +
                                 ((rowCount - 1) * Options.Spacing);

                    finalImage = new SKBitmap(width, height);

                    using (SKCanvas g = new SKCanvas(finalImage))
                    {
                        g.Clear(SKColors.WhiteSmoke);

                        if (!string.IsNullOrEmpty(infoString))
                        {
                            g.DrawText(infoString, Options.Padding, Options.Padding + infoStringHeight - 5, fontPaint);
                        }

                        int i = 0;
                        int offsetY = (Options.Padding * 2) + infoStringHeight;

                        using (SKPaint shadowPaint = new SKPaint { Color = new SKColor(0, 0, 0, 75) })
                        using (SKPaint borderPaint = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1 })
                        using (SKPaint timestampPaint = new SKPaint { TextSize = 10, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), Color = SKColors.White, IsAntialias = true })
                        {

                            for (int y = 0; y < rowCount; y++)
                            {
                                int offsetX = Options.Padding;

                                for (int x = 0; x < columnCount; x++)
                                {
                                    if (Options.DrawShadow)
                                    {
                                        int shadowOffset = 3;
                                        g.DrawRect(offsetX + shadowOffset, offsetY + shadowOffset, thumbWidth, thumbHeight, shadowPaint);
                                    }

                                    g.DrawBitmap(images[i], offsetX, offsetY);

                                    if (Options.DrawBorder)
                                    {
                                        g.DrawRect(offsetX, offsetY, thumbWidth - 1, thumbHeight - 1, borderPaint);
                                    }

                                    if (Options.AddTimestamp)
                                    {
                                        int timestampOffset = 10;
                                        string timestampText = thumbnails[i].Timestamp.ToString();
                                        // Simple text shadow/outline for readability? Original didn't have it explicit but usually good.
                                        // Original used Brushes.White.
                                        g.DrawText(timestampText, offsetX + timestampOffset, offsetY + timestampOffset + 10, timestampPaint); // +10 for approximate baseline
                                    }

                                    i++;

                                    if (i >= images.Count)
                                    {
                                        return finalImage;
                                    }

                                    offsetX += thumbWidth + Options.Spacing;
                                }

                                offsetY += thumbHeight + Options.Spacing;
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
                foreach (SKBitmap image in images)
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
