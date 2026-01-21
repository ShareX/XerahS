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

using DrawingPoint = System.Drawing.Point;
using Avalonia.Controls;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.UI.Services;

/// <summary>
/// Handles UI workflows for the color picker and screen color picker tools.
/// </summary>
public static class ColorPickerToolService
{
    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        return job switch
        {
            WorkflowType.ColorPicker => ShowColorPickerAsync(owner),
            WorkflowType.ScreenColorPicker => PickFromScreenAsync(owner, copyToClipboard: true),
            _ => Task.CompletedTask
        };
    }

    public static async Task ShowColorPickerAsync(Window? owner)
    {
        var viewModel = new ColorPickerViewModel();
        var dialog = new ColorPickerDialog
        {
            DataContext = viewModel
        };

        viewModel.ScreenPickerRequested = dialog.PickFromScreenAsync;

        await ShowDialogAsync(dialog, owner);
    }

    public static async Task<PointInfo?> PickFromScreenAsync(Window? owner, bool copyToClipboard)
    {
        if (!PlatformServices.IsInitialized)
        {
            ShowToast("Color Picker", "Platform services are not initialized.");
            return null;
        }

        var selection = await CapturePointAsync();
        if (selection == null)
        {
            ShowToast("Color Picker", "Screen color picking was cancelled.");
            return null;
        }

        if (copyToClipboard)
        {
            await CopyResultAsync(selection);
        }

        return selection;
    }

    /// <summary>
    /// Samples the screen at a given point and returns the pixel color.
    /// </summary>
    public static async Task<System.Drawing.Color?> GetColorFromScreenPoint(DrawingPoint position, CaptureOptions? options = null)
    {
        if (!PlatformServices.IsInitialized)
        {
            return null;
        }

        SKBitmap? bitmap = null;
        try
        {
            var rect = new SKRect(position.X, position.Y, position.X + 1, position.Y + 1);
            bitmap = await PlatformServices.ScreenCapture.CaptureRectAsync(rect, options);

            if (bitmap == null || bitmap.Width == 0 || bitmap.Height == 0)
            {
                return null;
            }

            var skColor = bitmap.GetPixel(0, 0);
            return System.Drawing.Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private static async Task<PointInfo?> CapturePointAsync()
    {
        var captureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings ?? new TaskSettingsCapture();
        var captureOptions = new CaptureOptions
        {
            UseModernCapture = captureSettings.UseModernCapture,
            ShowCursor = false,
            CaptureTransparent = captureSettings.CaptureTransparent,
            CaptureShadow = captureSettings.CaptureShadow,
            CaptureClientArea = captureSettings.CaptureClientArea
        };

        var pickerOptions = new XerahS.RegionCapture.RegionCaptureOptions
        {
            Mode = RegionCaptureMode.ScreenColorPicker,
            EnableWindowSnapping = false,
            EnableMagnifier = true,
            ShowCursor = false
        };

        var captureService = new RegionCaptureService { Options = pickerOptions };
        var result = await captureService.CaptureRegionAsync();
        if (result == null)
        {
            return null;
        }

        var point = result.Value.CursorPosition;
        int x = (int)Math.Round(point.X);
        int y = (int)Math.Round(point.Y);

        var color = await GetColorFromScreenPoint(new DrawingPoint(x, y), captureOptions);
        if (!color.HasValue)
        {
            return null;
        }

        return new PointInfo
        {
            Position = new DrawingPoint(x, y),
            Color = color.Value
        };
    }

    private static async Task CopyResultAsync(PointInfo result)
    {
        var toolsSettings = SettingsManager.DefaultTaskSettings?.ToolsSettings;
        var clipboardText = ColorPickerService.GetClipboardText(toolsSettings, result.Color, result.Position, useCtrlFormat: false);
        var infoText = ColorPickerService.GetInfoText(toolsSettings, result.Color, result.Position);

        try
        {
            await PlatformServices.Clipboard.SetTextAsync(clipboardText);
            ShowToast("Color Picker", infoText);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Color picker clipboard failed");
            ShowToast("Color Picker", "Failed to copy color to clipboard.");
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
                    Size = new SizeI(420, 140),
                    AutoHide = true,
                    LeftClickAction = ToastClickAction.CloseNotification
                });
                return;
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Color picker toast failed");
        }

        try
        {
            PlatformServices.Notification.ShowNotification(title, text);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Color picker notification failed");
        }
    }
}
