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
using ShareX.ImageEditor.Presentation.Rendering;
using SkiaSharp;
using XerahS.Common;
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
        var factory = UiViewModelFactoryAccessor.GetRequired();
        var viewModel = factory.CreateQrCodeGeneratorViewModel();
        await factory.ViewDialogService.ShowQrCodeGeneratorAsync(viewModel);
    }

    public static async Task ScanFromScreenAsync(Window? owner)
    {
        if (!PlatformServices.IsInitialized)
        {
            ShowToast(Resources.GetString("Qr_Title"), Resources.GetString("Common_PlatformServicesNotInitialized"));
            return;
        }

        var captureOptions = BuildCaptureOptions();
        var bitmap = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(captureOptions);
        if (bitmap == null)
        {
            ShowToast(Resources.GetString("Qr_Title"), Resources.GetString("Qr_ScreenCaptureCancelled"));
            return;
        }

        await DecodeAndShowAsync(bitmap, owner);
    }

    public static async Task ScanFromRegionAsync(Window? owner)
    {
        if (!PlatformServices.IsInitialized)
        {
            ShowToast(Resources.GetString("Qr_Title"), Resources.GetString("Common_PlatformServicesNotInitialized"));
            return;
        }

        var captureOptions = BuildCaptureOptions();
        var bitmap = await PlatformServices.ScreenCapture.CaptureRegionAsync(captureOptions);
        if (bitmap == null)
        {
            ShowToast(Resources.GetString("Qr_Title"), Resources.GetString("Qr_RegionCaptureCancelled"));
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
            LinuxRegionSelectorPreference = captureSettings.LinuxRegionSelectorPreference,
            MacOSRegionSelectorPreference = captureSettings.MacOSRegionSelectorPreference,
            MacOSPlayCaptureSound = captureSettings.MacOSPlayCaptureSound,
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
                ShowToast(Resources.GetString("Qr_Title"), error);
                return;
            }

            if (results.Count == 0)
            {
                ShowToast(Resources.GetString("Qr_Title"), Resources.GetString("Qr_NoCodeDetected"));
                return;
            }

            ShowToast(Resources.GetString("Qr_Title"), Resources.GetString("Qr_DecodedSummary", results.Count));

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

    private static Task ShowDialogAsync(Window dialog, Window? owner)
    {
        if (owner != null)
        {
            dialog.Show(owner);
        }
        else
        {
            dialog.Show();
        }

        return Task.CompletedTask;
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
