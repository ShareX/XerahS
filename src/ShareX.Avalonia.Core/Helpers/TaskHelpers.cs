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

namespace XerahS.Core;

/// <summary>
/// Task-related helper methods for file naming, folder management, and image processing.
/// Extracted from ShareX TaskHelpers - contains only pure logic (no UI dependencies).
/// </summary>
public static partial class TaskHelpers
{
    #region File Naming

    /// <summary>
    /// Generate a file name for a captured image
    /// </summary>
    public static string GetFileName(TaskSettings taskSettings, string extension, SkiaSharp.SKBitmap? bmp = null)
    {
        var metadata = bmp != null ? new TaskMetadata(bmp) : new TaskMetadata();
        return GetFileName(taskSettings, extension, metadata);
    }

    /// <summary>
    /// Generate a file name with metadata
    /// </summary>
    public static string GetFileName(TaskSettings taskSettings, string extension, TaskMetadata? metadata)
    {
        var settings = SettingManager.Settings;
        string pattern;

        // Use window-specific pattern if available
        if (!string.IsNullOrEmpty(settings.SaveImageSubFolderPatternWindow) &&
            !string.IsNullOrEmpty(metadata?.WindowTitle))
        {
            pattern = taskSettings.UploadSettings.NameFormatPatternActiveWindow;
        }
        else
        {
            pattern = taskSettings.UploadSettings.NameFormatPattern;
        }

        var nameParser = new NameParser(NameParserType.FileName)
        {
            AutoIncrementNumber = settings.NameParserAutoIncrementNumber,
            MaxNameLength = taskSettings.AdvancedSettings.NamePatternMaxLength,
            MaxTitleLength = taskSettings.AdvancedSettings.NamePatternMaxTitleLength,
            WindowText = metadata?.WindowTitle ?? "",
            ProcessName = metadata?.ProcessName ?? ""
        };

        if (metadata?.Image != null)
        {
            nameParser.ImageWidth = metadata.Image.Width;
            nameParser.ImageHeight = metadata.Image.Height;
        }

        // Use custom timezone if configured
        if (taskSettings.UploadSettings.UseCustomTimeZone)
        {
            nameParser.CustomTimeZone = taskSettings.UploadSettings.CustomTimeZone;
        }

        string fileName = nameParser.Parse(pattern);

        if (!string.IsNullOrEmpty(extension))
        {
            fileName += "." + extension.TrimStart('.');
        }

        // Update auto-increment counter (if NameParser supports it)
        // TODO: Add auto-increment tracking when NameParser is enhanced

        return fileName;
    }

    #endregion

    #region Screenshots Folder

    /// <summary>
    /// Get the screenshots folder path based on settings and metadata
    /// </summary>
    public static string GetScreenshotsFolder(TaskSettings? taskSettings = null, TaskMetadata? metadata = null)
    {
        var settings = SettingManager.Settings;
        string screenshotsFolder;

        var nameParser = new NameParser(NameParserType.FilePath);

        if (metadata != null)
        {
            if (metadata.Image != null)
            {
                nameParser.ImageWidth = metadata.Image.Width;
                nameParser.ImageHeight = metadata.Image.Height;
            }

            nameParser.WindowText = metadata.WindowTitle ?? "";
            nameParser.ProcessName = metadata.ProcessName ?? "";
        }

        if (taskSettings != null && taskSettings.OverrideScreenshotsFolder &&
            !string.IsNullOrEmpty(taskSettings.ScreenshotsFolder))
        {
            screenshotsFolder = nameParser.Parse(taskSettings.ScreenshotsFolder);
        }
        else
        {
            string subFolderPattern;

            if (!string.IsNullOrEmpty(settings.SaveImageSubFolderPatternWindow) &&
                !string.IsNullOrEmpty(nameParser.WindowText))
            {
                subFolderPattern = settings.SaveImageSubFolderPatternWindow;
            }
            else
            {
                subFolderPattern = settings.SaveImageSubFolderPattern;
            }

            string subFolderPath = nameParser.Parse(subFolderPattern);
            screenshotsFolder = Path.Combine(GetScreenshotsParentFolder(), subFolderPath);
        }

        return FileHelpers.GetAbsolutePath(screenshotsFolder);
    }

    /// <summary>
    /// Get the parent folder for screenshots
    /// </summary>
    public static string GetScreenshotsParentFolder()
    {
        var settings = SettingManager.Settings;

        if (settings.UseCustomScreenshotsPath && !string.IsNullOrEmpty(settings.CustomScreenshotsPath))
        {
            return settings.CustomScreenshotsPath;
        }

        // Default to Pictures/ShareX
        string picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(picturesFolder, "ShareX");
    }

    #endregion

    #region File Exists Handling

    /// <summary>
    /// Handle file exists scenario based on settings
    /// </summary>
    public static string HandleExistsFile(string folder, string fileName, TaskSettings taskSettings)
    {
        string filePath = Path.Combine(folder, fileName);
        return HandleExistsFile(filePath, taskSettings);
    }

    /// <summary>
    /// Handle file exists scenario - returns final file path
    /// </summary>
    public static string HandleExistsFile(string filePath, TaskSettings taskSettings)
    {
        if (File.Exists(filePath))
        {
            switch (taskSettings.ImageSettings.FileExistAction)
            {
                case FileExistAction.Overwrite:
                    return filePath;

                case FileExistAction.UniqueName:
                    return FileHelpers.GetUniqueFilePath(filePath);

                case FileExistAction.Cancel:
                    return "";

                case FileExistAction.Ask:
                default:
                    // For now, default to unique name (UI will handle Ask)
                    return FileHelpers.GetUniqueFilePath(filePath);
            }
        }

        return filePath;
    }

    #endregion

    #region Image Processing

    /// <summary>
    /// Save image to stream with specified format
    /// </summary>
    public static MemoryStream? SaveImageAsStream(SkiaSharp.SKBitmap bmp, EImageFormat imageFormat, TaskSettings taskSettings)
    {
        return SaveImageAsStream(bmp, imageFormat,
            taskSettings.ImageSettings.ImagePNGBitDepth,
            taskSettings.ImageSettings.ImageJPEGQuality,
            taskSettings.ImageSettings.ImageGIFQuality);
    }

    /// <summary>
    /// Save image to stream
    /// </summary>
    public static MemoryStream? SaveImageAsStream(SkiaSharp.SKBitmap bmp, EImageFormat imageFormat,
        PNGBitDepth pngBitDepth = PNGBitDepth.Default,
        int jpegQuality = 90,
        GIFQuality gifQuality = GIFQuality.Default)
    {
        if (bmp == null) return null;

        var ms = new MemoryStream();

        try
        {
            using var image = SkiaSharp.SKImage.FromBitmap(bmp);
            using var data = imageFormat switch
            {
                EImageFormat.JPEG => image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, jpegQuality),
                EImageFormat.GIF => image.Encode(SkiaSharp.SKEncodedImageFormat.Gif, 100),
                EImageFormat.BMP => image.Encode(SkiaSharp.SKEncodedImageFormat.Bmp, 100),
                EImageFormat.TIFF => image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100), // SkiaSharp doesn't support TIFF encoding
                _ => image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)
            };

            data.SaveTo(ms);
            ms.Position = 0;
            return ms;
        }
        catch
        {
            ms.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Save image to file
    /// </summary>
    public static string? SaveImageAsFile(SkiaSharp.SKBitmap bmp, TaskSettings taskSettings, bool overwriteFile = false)
    {
        string screenshotsFolder = GetScreenshotsFolder(taskSettings);
        FileHelpers.CreateDirectory(screenshotsFolder);

        string extension = EnumExtensions.GetDescription(taskSettings.ImageSettings.ImageFormat);
        string fileName = GetFileName(taskSettings, extension, bmp);
        string filePath = Path.Combine(screenshotsFolder, fileName);

        if (!overwriteFile)
        {
            filePath = HandleExistsFile(filePath, taskSettings);
            if (string.IsNullOrEmpty(filePath)) return null;
        }

        ImageHelpers.SaveBitmap(bmp, filePath);
        return filePath;
    }

    /// <summary>
    /// Create thumbnail from image
    /// </summary>
    public static SkiaSharp.SKBitmap? CreateThumbnail(SkiaSharp.SKBitmap bmp, int width, int height)
    {
        if (bmp == null) return null;

        // Calculate dimensions maintaining aspect ratio
        double ratioX = width > 0 ? (double)width / bmp.Width : 0;
        double ratioY = height > 0 ? (double)height / bmp.Height : 0;
        double ratio = Math.Min(ratioX > 0 ? ratioX : ratioY, ratioY > 0 ? ratioY : ratioX);

        if (ratio <= 0 || ratio >= 1) return null;

        int newWidth = (int)(bmp.Width * ratio);
        int newHeight = (int)(bmp.Height * ratio);

        return ImageHelpers.ResizeImage(bmp, newWidth, newHeight);
    }

    /// <summary>
    /// Check if file should be auto-converted to JPEG
    /// </summary>
    public static bool ShouldUseJpeg(SkiaSharp.SKBitmap bmp, TaskSettings taskSettings)
    {
        if (!taskSettings.ImageSettings.ImageAutoUseJPEG) return false;

        long imageSize = (long)bmp.Width * bmp.Height;
        long threshold = (long)taskSettings.ImageSettings.ImageAutoUseJPEGSize * 1024;

        return imageSize > threshold;
    }

    #endregion

    #region Upload Checks

    /// <summary>
    /// Check if uploading is allowed based on settings
    /// </summary>
    public static bool IsUploadAllowed()
    {
        return !SettingManager.Settings.DisableUpload;
    }

    #endregion
}
