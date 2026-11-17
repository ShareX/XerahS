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
using XerahS.Core.Tasks.Processors;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class PinToScreenToolService
{
    private static readonly FilePickerFileType ImageFileType = new(Resources.GetString("Pin_ImageFilesFilter"))
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
                await PinFromClipboardAsync();
                break;
            case WorkflowType.PinToScreenFromFile:
                await PinFromFileAsync(owner);
                break;
            case WorkflowType.PinToScreenCloseAll:
                PinToScreenManager.CloseAll();
                break;
        }
    }

    public readonly record struct PinFilesResult(int PinnedCount, int SkippedCount);

    /// <summary>
    /// Pins with OmaSnap on Omarchy-like Hyprland sessions (its Upload button uses the XerahS
    /// destination through omaxerahs), otherwise with the XerahS pin window (XIP0088).
    /// </summary>
    private static async Task PinAsync(SKBitmap bitmap, PixelPoint? location)
    {
        if (await HostedEditorAndPinService.TryPinAsync(bitmap))
        {
            return;
        }

        PinToScreenManager.PinImage(bitmap, location, GetOptions());
    }

    public static async Task<PinFilesResult> PinFilesAsync(IEnumerable<string>? filePaths, bool showToast = true)
    {
        int pinnedCount = 0;
        int skippedCount = 0;

        foreach (string filePath in filePaths ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                skippedCount++;
                continue;
            }

            try
            {
                using SKBitmap? bitmap = SKBitmap.Decode(filePath);
                if (bitmap == null)
                {
                    skippedCount++;
                    continue;
                }

                if (!await HostedEditorAndPinService.TryPinFileAsync(filePath))
                {
                    PinToScreenManager.PinImage(bitmap, null, GetOptions());
                }

                pinnedCount++;
            }
            catch (Exception ex)
            {
                skippedCount++;
                DebugHelper.WriteException(ex, $"PinToScreen: Failed to pin '{filePath}'.");
            }
        }

        if (showToast)
        {
            if (pinnedCount == 0)
            {
                ShowToast(Resources.GetString("SendToAction_PinToScreen"), Resources.GetString("Pin_NoCompatibleImages"));
            }
            else if (skippedCount > 0)
            {
                ShowToast(Resources.GetString("SendToAction_PinToScreen"), Resources.GetString("Pin_PinnedSummary", pinnedCount, skippedCount));
            }
        }

        return new PinFilesResult(pinnedCount, skippedCount);
    }

    private static async Task PinToScreenAsync(Window? owner)
    {
        var dialog = new PinToScreenStartupDialog
        {
            BrowseFileRequested = () => BrowseImageFileAsync(null, owner)
        };

        var fromScreen = false;
        dialog.SelectRegionRequested = () =>
        {
            fromScreen = true;
            return Task.FromResult<(SKBitmap? Bitmap, PixelPoint? Location)>((null, null));
        };

        await ModalDialogHost.ShowUntilClosedAsync(
            dialog,
            set => dialog.CloseRequested = () => set(),
            debugSource: nameof(PinToScreenStartupDialog));

        if (fromScreen)
        {
            var (bitmap, location) = await SelectRegionWithLocationAsync();
            if (bitmap != null)
            {
                try
                {
                    await PinAsync(bitmap, location);
                }
                finally
                {
                    bitmap.Dispose();
                }
            }

            return;
        }

        if (dialog.Result != null)
        {
            var bitmap = dialog.Result.Image;
            try
            {
                await PinAsync(bitmap, dialog.Result.Location);
            }
            finally
            {
                bitmap.Dispose();
            }
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
        try
        {
            await PinAsync(bitmap, location);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static async Task PinFromClipboardAsync()
    {
        if (!PlatformServices.IsInitialized) return;

        var bitmap = PlatformServices.Clipboard.GetImage();

        if (bitmap == null)
        {
            ShowToast(Resources.GetString("SendToAction_PinToScreen"), Resources.GetString("Pin_ClipboardNoImage"));
            return;
        }

        try
        {
            await PinAsync(bitmap, null);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static async Task PinFromFileAsync(Window? owner)
    {
        var path = await BrowseImageFileAsync(null, owner);
        if (string.IsNullOrEmpty(path)) return;

        if (await HostedEditorAndPinService.TryPinFileAsync(path))
        {
            return;
        }

        using var bitmap = SKBitmap.Decode(path);
        if (bitmap == null)
        {
            ShowToast(Resources.GetString("SendToAction_PinToScreen"), Resources.GetString("Pin_LoadImageFailed"));
            return;
        }

        try
        {
            PinToScreenManager.PinImage(bitmap, null, GetOptions());
        }
        finally
        {
            bitmap.Dispose();
        }
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

    private static async Task<string?> BrowseImageFileAsync(Window? window, Window? owner)
    {
        var storageProvider = StorageProviderResolver.Resolve(window, owner);
        if (storageProvider == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = Resources.GetString("Pin_SelectImageTitle"),
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType }
        };

        var files = await storageProvider.OpenFilePickerAsync(options);
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
            LinuxRegionSelectorPreference = captureSettings.LinuxRegionSelectorPreference,
            MacOSRegionSelectorPreference = captureSettings.MacOSRegionSelectorPreference,
            MacOSPlayCaptureSound = captureSettings.MacOSPlayCaptureSound,
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
