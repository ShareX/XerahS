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
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class OcrToolService
{
    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        return job switch
        {
            WorkflowType.OCR => PerformOcrAsync(owner),
            _ => Task.CompletedTask
        };
    }

    private static async Task PerformOcrAsync(Window? owner)
    {
        var ocrService = PlatformServices.Ocr;

        if (ocrService == null || !ocrService.IsSupported)
        {
            ShowToast("OCR", "OCR is not supported on this platform.");
            return;
        }

        if (!PlatformServices.IsInitialized)
        {
            ShowToast("OCR", "Platform services are not initialized.");
            return;
        }

        // Capture region
        var captureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings
            ?? new TaskSettingsCapture();

        var captureOptions = new CaptureOptions
        {
            UseModernCapture = captureSettings.UseModernCapture,
            ShowCursor = captureSettings.ShowCursor,
            CaptureTransparent = captureSettings.CaptureTransparent,
            CaptureShadow = captureSettings.CaptureShadow,
            CaptureClientArea = captureSettings.CaptureClientArea
        };

        var bitmap = await PlatformServices.ScreenCapture.CaptureRegionAsync(captureOptions);

        if (bitmap == null)
        {
            return; // User cancelled, no toast needed
        }

        // Create ViewModel and wire callbacks
        var viewModel = new OcrViewModel(bitmap);

        viewModel.SelectRegionRequested = async () =>
        {
            return await CaptureNewRegionAsync(captureOptions);
        };

        var window = new OcrWindow
        {
            DataContext = viewModel
        };

        if (CanUseOwner(owner))
        {
            window.Show(owner!);
        }
        else
        {
            window.Show();
        }
    }

    private static async Task<SKBitmap?> CaptureNewRegionAsync(CaptureOptions captureOptions)
    {
        try
        {
            await Task.Delay(300); // Allow window to minimize
            return await PlatformServices.ScreenCapture.CaptureRegionAsync(captureOptions);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OCR region capture");
            return null;
        }
    }

    private static bool CanUseOwner(Window? owner)
    {
        return owner != null &&
               owner.IsVisible &&
               owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
               owner.ShowInTaskbar;
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
            DebugHelper.WriteException(ex, "OCR toast failed");
        }

        try
        {
            PlatformServices.Notification.ShowNotification(title, text);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OCR notification failed");
        }
    }
}
