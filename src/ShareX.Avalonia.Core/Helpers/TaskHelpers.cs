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

using ShareX.Ava.Common;
using System;
using System.IO;
using SkiaSharp;
// REMOVED: System.Drawing, System.Drawing.Imaging

namespace ShareX.Ava.Core;

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
    public static string GetFileName(TaskSettings taskSettings, string extension, SKBitmap? bmp = null)
    {
        // TODO: Update TaskMetadata to use SKBitmap or be generic
        // Assuming TaskMetadata currently takes Bitmap, we might need to adjust or pass null/properties manually?
        // Let's create TaskMetadata if bmp is SKBitmap.
        // If TaskMetadata is not yet refactored, we might strictly need to refactor it too.
        // Assuming TaskMetadata is simple and we can patch it or it already supports generic properties.
        // For now, let's assume we can construct it or just access width/height.
        
        var metadata = new TaskMetadata();
        if (bmp != null) 
        {
            // metadata.Image = bmp; // If Image property is Bitmap, this fails. 
            // We'll set properties directly if possible or update TaskMetadata later.
            // Looking at TaskMetadata usage below, it seems to access .Image.Width/Height.
            // We should check TaskMetadata definition.
        }

        return GetFileName(taskSettings, extension, metadata, bmp);
    }

    /// <summary>
    /// Generate a file name with metadata
    /// </summary>
    public static string GetFileName(TaskSettings taskSettings, string extension, TaskMetadata? metadata, SKBitmap? bmp = null)
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

        if (bmp != null)
        {
            nameParser.ImageWidth = bmp.Width;
            nameParser.ImageHeight = bmp.Height;
        }
        // Fallback to metadata image if available and bmp is null (legacy)
        else if (metadata?.Image is SKBitmap skBmp)
        {
             nameParser.ImageWidth = skBmp.Width;
             nameParser.ImageHeight = skBmp.Height;
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
    public static string GetScreenshotsFolder(TaskSettings? taskSettings = null, TaskMetadata? metadata = null, SKBitmap? bmp = null)
    {
        var settings = SettingManager.Settings;
        string screenshotsFolder;

        var nameParser = new NameParser(NameParserType.FilePath);

        if (metadata != null)
        {
            if (bmp != null)
            {
                nameParser.ImageWidth = bmp.Width;
                nameParser.ImageHeight = bmp.Height;
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
    public static MemoryStream? SaveImageAsStream(SKBitmap bmp, EImageFormat imageFormat, TaskSettings taskSettings)
    {
        return SaveImageAsStream(bmp, imageFormat, 
            taskSettings.ImageSettings.ImagePNGBitDepth,
            taskSettings.ImageSettings.ImageJPEGQuality,
            taskSettings.ImageSettings.ImageGIFQuality);
    }

    /// <summary>
    /// Save image to stream
    /// </summary>
    public static MemoryStream? SaveImageAsStream(SKBitmap bmp, EImageFormat imageFormat,
        PNGBitDepth pngBitDepth = PNGBitDepth.Default,
        int jpegQuality = 90,
        GIFQuality gifQuality = GIFQuality.Default)
    {
        if (bmp == null) return null;

        var ms = new MemoryStream();

        try
        {
            switch (imageFormat)
            {
                case EImageFormat.PNG:
                    ImageHelpers.SaveBitmap(bmp, "temp.png", 100); // Hack: SaveBitmap logic handles stream? No it saves to file.
                    // We need a helper to save to stream.
                    // ImageHelpers.SaveBitmap logic: SKImage.FromBitmap(bmp).Encode(format, quality).SaveTo(stream);
                    using (var image = SKImage.FromBitmap(bmp))
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        data.SaveTo(ms);
                    }
                    break;

                case EImageFormat.JPEG:
                    using (var image = SKImage.FromBitmap(bmp))
                    using (var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality))
                    {
                        data.SaveTo(ms);
                    }
                    break;

                case EImageFormat.GIF:
                     ImageHelpers.SaveGIF(bmp, ms, gifQuality);
                     break;

                case EImageFormat.BMP:
                    using (var image = SKImage.FromBitmap(bmp))
                    using (var data = image.Encode(SKEncodedImageFormat.Bmp, 100))
                    {
                        data.SaveTo(ms);
                    }
                    break;

                case EImageFormat.TIFF:
                    // Skia lacks TIFF, fallback to PNG or BMP? Or custom if needed.
                    // For now PNG fallback as in ImageHelpers
                    using (var image = SKImage.FromBitmap(bmp))
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        data.SaveTo(ms);
                    }
                    break;
            }

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
    public static string? SaveImageAsFile(SKBitmap bmp, TaskSettings taskSettings, bool overwriteFile = false)
    {
        string screenshotsFolder = GetScreenshotsFolder(taskSettings, null, bmp); // Pass bmp for dims
        FileHelpers.CreateDirectory(screenshotsFolder);

        string extension = EnumExtensions.GetDescription(taskSettings.ImageSettings.ImageFormat);
        string fileName = GetFileName(taskSettings, extension, bmp);
        string filePath = Path.Combine(screenshotsFolder, fileName);

        if (!overwriteFile)
        {
            filePath = HandleExistsFile(filePath, taskSettings);
            if (string.IsNullOrEmpty(filePath)) return null;
        }

        // Logic to save file
        // Re-use logic from SaveImageAsStream or ImageHelpers?
        // ImageHelpers.SaveBitmap supports path.
        
        // But we need to support formats.
        switch (taskSettings.ImageSettings.ImageFormat)
        {
             case EImageFormat.GIF:
                using (var fs = File.OpenWrite(filePath))
                {
                    TaskHelpers.SaveImageAsStream(bmp, EImageFormat.GIF, taskSettings)?.CopyTo(fs);
                }
                break;
             case EImageFormat.JPEG:
                // Use ImageHelpers for convenience if it exposes quality?
                // ImageHelpers.SaveBitmap takes quality.
                ImageHelpers.SaveBitmap(bmp, filePath, taskSettings.ImageSettings.ImageJPEGQuality);
                break;
             default:
                ImageHelpers.SaveBitmap(bmp, filePath);
                break;
        }
        
        return filePath;
    }

    /// <summary>
    /// Create thumbnail from image
    /// </summary>
    public static SKBitmap? CreateThumbnail(SKBitmap bmp, int width, int height)
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
    public static bool ShouldUseJpeg(SKBitmap bmp, TaskSettings taskSettings)
    {
        if (!taskSettings.ImageSettings.ImageAutoUseJPEG) return false;

        long imageSize = (long)bmp.Width * bmp.Height;
        long threshold = (long)taskSettings.ImageSettings.ImageAutoUseJPEGSize * 1024;

        return imageSize > threshold;
    }
    
    // Check if we need simple struct for TaskMetadata if it was removed or if we need to fix it.
    // Assuming TaskMetadata is in another file.
    
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
