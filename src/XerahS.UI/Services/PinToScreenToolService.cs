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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class PinToScreenToolService
{
    private static readonly FilePickerFileType ImageFileType = new("Image files")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.tiff", "*.tif" }
    };

    public static async Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        switch (job)
        {
            case WorkflowType.PinToScreen:
                await PinToScreenAsync(owner);
                break;
            case WorkflowType.PinToScreenFromScreen:
                await PinFromScreenAsync();
                break;
            case WorkflowType.PinToScreenFromClipboard:
                PinFromClipboard();
                break;
            case WorkflowType.PinToScreenFromFile:
                await PinFromFileAsync(owner);
                break;
            case WorkflowType.PinToScreenCloseAll:
                PinToScreenManager.CloseAll();
                break;
        }
    }

    private static async Task PinToScreenAsync(Window? owner)
    {
        // Show startup dialog to let user choose source
        var dialog = new PinToScreenStartupDialog();

        dialog.SelectRegionRequested = SelectRegionWithLocationAsync;
        dialog.BrowseFileRequested = () => BrowseImageFileAsync(owner);

        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            await dialog.ShowDialog<object?>(dialog);
        }

        if (dialog.Result != null)
        {
            PinToScreenManager.PinImage(dialog.Result.Image, dialog.Result.Location, GetOptions());
        }
    }

    private static async Task PinFromScreenAsync()
    {
        if (!PlatformServices.IsInitialized) return;

        var captureOptions = BuildCaptureOptions();

        var rect = await PlatformServices.ScreenCapture.SelectRegionAsync(captureOptions);
        if (rect == SKRectI.Empty) return;

        var bitmap = await PlatformServices.ScreenCapture.CaptureRectAsync(
            new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom), captureOptions);
        if (bitmap == null) return;

        var location = new PixelPoint(rect.Left, rect.Top);
        PinToScreenManager.PinImage(bitmap, location, GetOptions());
    }

    private static void PinFromClipboard()
    {
        if (!PlatformServices.IsInitialized) return;

        var bitmap = PlatformServices.Clipboard.GetImage();

        if (bitmap == null)
        {
            ShowToast("Pin to Screen", "Clipboard does not contain an image.");
            return;
        }

        PinToScreenManager.PinImage(bitmap, null, GetOptions());
    }

    private static async Task PinFromFileAsync(Window? owner)
    {
        var path = await BrowseImageFileAsync(owner);
        if (string.IsNullOrEmpty(path)) return;

        var bitmap = SKBitmap.Decode(path);
        if (bitmap == null)
        {
            ShowToast("Pin to Screen", "Failed to load image file.");
            return;
        }

        PinToScreenManager.PinImage(bitmap, null, GetOptions());
    }

    internal static async Task<(SKBitmap? Bitmap, PixelPoint? Location)> SelectRegionWithLocationAsync()
    {
        if (!PlatformServices.IsInitialized) return (null, null);

        var captureOptions = BuildCaptureOptions();

        await Task.Delay(300); // Allow dialog to hide

        var rect = await PlatformServices.ScreenCapture.SelectRegionAsync(captureOptions);
        if (rect == SKRectI.Empty) return (null, null);

        var bitmap = await PlatformServices.ScreenCapture.CaptureRectAsync(
            new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom), captureOptions);

        if (bitmap == null) return (null, null);

        return (bitmap, new PixelPoint(rect.Left, rect.Top));
    }

    private static async Task<string?> BrowseImageFileAsync(Window? owner)
    {
        var topLevel = owner != null ? TopLevel.GetTopLevel(owner) : null;
        if (topLevel == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = "Select Image to Pin",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType }
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count < 1) return null;

        return files[0].TryGetLocalPath();
    }

    private static PinToScreenOptions GetOptions()
    {
        return SettingsManager.DefaultTaskSettings?.ToolsSettings?.PinToScreenOptions
            ?? new PinToScreenOptions();
    }

    private static CaptureOptions BuildCaptureOptions()
    {
        var captureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings
            ?? new TaskSettingsCapture();

        return new CaptureOptions
        {
            UseModernCapture = captureSettings.UseModernCapture,
            ShowCursor = captureSettings.ShowCursor,
            CaptureTransparent = captureSettings.CaptureTransparent,
            CaptureShadow = captureSettings.CaptureShadow,
            CaptureClientArea = captureSettings.CaptureClientArea
        };
    }

    private static void ShowToast(string title, string text)
    {
        try
        {
            if (PlatformServices.IsToastServiceInitialized)
            {
                PlatformServices.Toast.ShowToast(new ToastConfig
                {
                    Title = title,
                    Text = text,
                    Duration = 4f,
                    Size = new SizeI(420, 120),
                    AutoHide = true,
                    LeftClickAction = ToastClickAction.CloseNotification
                });
                return;
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PinToScreen toast failed");
        }

        try
        {
            PlatformServices.Notification.ShowNotification(title, text);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PinToScreen notification failed");
        }
    }
}
