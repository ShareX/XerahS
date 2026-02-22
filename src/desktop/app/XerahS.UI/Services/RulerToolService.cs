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
using XerahS.Core.Services;
using XerahS.RegionCapture;
using XerahS.RegionCapture.Services;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Services;

public static class RulerToolService
{
    public static async Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        if (job == WorkflowType.Ruler)
        {
            await ShowRulerAsync();
        }
    }

    private static async Task ShowRulerAsync()
    {
        if (!IsRulerSupported())
        {
            NotifyRulerUnavailable();
            return;
        }

        var captureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings ?? new TaskSettingsCapture();
        var captureOptions = new CaptureOptions
        {
            UseModernCapture = captureSettings.UseModernCapture,
            ShowCursor = false,
            CaptureTransparent = captureSettings.CaptureTransparent,
            CaptureShadow = captureSettings.CaptureShadow,
            CaptureClientArea = captureSettings.CaptureClientArea
        };

        // Capture full-screen background for magnifier
        SKBitmap? fullScreenBitmap = null;
        try
        {
            fullScreenBitmap = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(captureOptions);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Ruler initial capture failed");
            // Continue without background - magnifier won't work but ruler will
        }

        try
        {
            // Configure ruler options
            var rulerOptions = new XerahS.RegionCapture.RegionCaptureOptions
            {
                Mode = RegionCaptureMode.Ruler,
                QuickCrop = false,                  // Don't auto-complete on click
                UseLightResizeNodes = true,         // Use lighter resize handles
                EnableWindowSnapping = false,       // Disable window snapping for ruler
                EnableMagnifier = true,             // Enable magnifier for precision
                EnableKeyboardNudge = true,         // Allow arrow key adjustments
                ShowCursor = false,                 // Hide cursor (overlay draws crosshair)
                EditorOptions = RegionCaptureAnnotationOptionsStore.GetEditorOptions(workflowType: WorkflowType.Ruler),
                BackgroundImage = fullScreenBitmap
            };

            var regionCaptureService = new RegionCaptureService { Options = rulerOptions };
            try
            {
                _ = await regionCaptureService.CaptureRegionAsync();
            }
            finally
            {
                RegionCaptureAnnotationOptionsStore.Persist();
            }

            // Result contains the measured region if user confirmed
            // For now, we just dismiss - could add clipboard copy of measurements later
        }
        finally
        {
            // Clean up background bitmap
            fullScreenBitmap?.Dispose();
        }
    }

    private static bool IsRulerSupported()
    {
        try
        {
            return PlatformServices.IsInitialized && PlatformServices.PlatformInfo.IsWindows;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to check ruler platform support");
            return false;
        }
    }

    private static void NotifyRulerUnavailable()
    {
        try
        {
            if (!PlatformServices.IsToastServiceInitialized)
            {
                return;
            }

            PlatformServices.Toast.ShowToast(new ToastConfig
            {
                Title = "Ruler",
                Text = "Ruler tool is currently available on Windows builds.",
                Duration = 4f
            });
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Ruler unavailable notification failed");
        }
    }
}
