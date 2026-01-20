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

using Avalonia.Controls;
using ShareX.Editor.Helpers;
using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.UI.Services;

public static class QrCodeToolService
{
    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        return job switch
        {
            WorkflowType.QRCode => ShowGeneratorAsync(owner),
            WorkflowType.QRCodeDecodeFromScreen => ScanFromScreenAsync(owner),
            WorkflowType.QRCodeScanRegion => ScanFromRegionAsync(owner),
            _ => Task.CompletedTask
        };
    }

    public static async Task ShowGeneratorAsync(Window? owner)
    {
        var viewModel = new QrCodeGeneratorViewModel();
        var dialog = new QrCodeGeneratorDialog
        {
            DataContext = viewModel
        };

        await ShowDialogAsync(dialog, owner);
    }

    public static async Task ScanFromScreenAsync(Window? owner)
    {
        if (!PlatformServices.IsInitialized)
        {
            ShowToast("QR Code", "Platform services are not initialized.");
            return;
        }

        var captureOptions = BuildCaptureOptions();
        var bitmap = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(captureOptions);
        if (bitmap == null)
        {
            ShowToast("QR Code", "Screen capture was cancelled.");
            return;
        }

        await DecodeAndShowAsync(bitmap, owner);
    }

    public static async Task ScanFromRegionAsync(Window? owner)
    {
        if (!PlatformServices.IsInitialized)
        {
            ShowToast("QR Code", "Platform services are not initialized.");
            return;
        }

        var captureOptions = BuildCaptureOptions();
        var bitmap = await PlatformServices.ScreenCapture.CaptureRegionAsync(captureOptions);
        if (bitmap == null)
        {
            ShowToast("QR Code", "Region capture was cancelled.");
            return;
        }

        await DecodeAndShowAsync(bitmap, owner);
    }

    private static CaptureOptions BuildCaptureOptions()
    {
        var captureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings ?? new TaskSettingsCapture();

        return new CaptureOptions
        {
            UseModernCapture = captureSettings.UseModernCapture,
            ShowCursor = captureSettings.ShowCursor,
            CaptureTransparent = captureSettings.CaptureTransparent,
            CaptureShadow = captureSettings.CaptureShadow,
            CaptureClientArea = captureSettings.CaptureClientArea
        };
    }

    private static async Task DecodeAndShowAsync(SKBitmap bitmap, Window? owner)
    {
        try
        {
            string? error;
            var results = QrCodeService.Decode(bitmap, out error);

            if (!string.IsNullOrEmpty(error))
            {
                ShowToast("QR Code", error);
                return;
            }

            if (results.Count == 0)
            {
                ShowToast("QR Code", "No QR code was detected.");
                return;
            }

            ShowToast("QR Code", $"Decoded {results.Count} QR code(s).");

            var previewBitmap = BitmapConversionHelpers.ToAvaloniBitmap(bitmap);
            var viewModel = new QrCodeDecodeResultsViewModel(results, previewBitmap);
            var dialog = new QrCodeDecodeResultsDialog
            {
                DataContext = viewModel
            };

            await ShowDialogAsync(dialog, owner);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static async Task ShowDialogAsync(Window dialog, Window? owner)
    {
        if (owner != null)
        {
            await dialog.ShowDialog(owner);
            return;
        }

        dialog.Show();
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
            DebugHelper.WriteException(ex, "QR Code toast failed");
        }

        try
        {
            PlatformServices.Notification.ShowNotification(title, text);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "QR Code notification failed");
        }
    }
}
