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

using System.Drawing;
using Avalonia.Controls;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class ScrollingCaptureToolService
{
    /// <summary>
    /// The view model of the currently open scrolling capture window, if any.
    /// Used so the Scrolling Capture hotkey can stop an in-progress capture.
    /// </summary>
    internal static ScrollingCaptureViewModel? CurrentCapture { get; private set; }

    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner, TaskSettings? taskSettings = null)
    {
        return job switch
        {
            WorkflowType.ScrollingCapture => ShowScrollingCaptureWindowAsync(owner, taskSettings),
            _ => Task.CompletedTask
        };
    }

    /// <summary>
    /// Stops the current scrolling capture if one is active (e.g. when user presses the same hotkey again).
    /// </summary>
    internal static void StopCurrentCapture()
    {
        if (CurrentCapture?.IsCapturing == true)
        {
            CurrentCapture.StopCapture();
        }
    }

    private static Task ShowScrollingCaptureWindowAsync(Window? owner, TaskSettings? taskSettings)
    {
        var viewModel = new ScrollingCaptureViewModel();
        var window = new ScrollingCaptureWindow
        {
            DataContext = viewModel
        };

        CurrentCapture = viewModel;
        window.Closed += (_, _) => CurrentCapture = null;

        // Wire window selection callback
        viewModel.SelectWindowRequested = async () =>
        {
            return await SelectTargetWindowAsync(window);
        };

        // Wire upload callback
        viewModel.UploadRequested = async (image) =>
        {
            await UploadCapturedImageAsync(image, taskSettings);
        };

        if (CanUseOwner(owner))
        {
            window.Show(owner!);
        }
        else
        {
            window.Show();
        }

        return Task.CompletedTask;
    }

    private static async Task<(IntPtr Handle, Rectangle ClientBounds)?> SelectTargetWindowAsync(Window parentWindow)
    {
        if (!PlatformServices.IsInitialized)
        {
            return null;
        }

        // Hide parent window during selection
        bool wasVisible = parentWindow.IsVisible;
        var previousState = parentWindow.WindowState;

        if (wasVisible)
        {
            parentWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
        }

        try
        {
            await Task.Delay(300); // Allow window to minimize

            // Use the existing window selector dialog to pick a target window
            var windowService = PlatformServices.Window;
            var allWindows = windowService.GetAllWindows();

            if (allWindows.Length == 0)
            {
                return null;
            }

            // Show window selector
            var tcs = new TaskCompletionSource<WindowInfo?>();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var selectorViewModel = new WindowSelectorViewModel();
                var selectorDialog = new WindowSelectorDialog
                {
                    DataContext = selectorViewModel
                };

                var selectorWindow = new Window
                {
                    Title = "Select Window to Capture",
                    Width = 400,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = selectorDialog,
                    Topmost = true
                };

                selectorViewModel.OnWindowSelected = (selectedWindow) =>
                {
                    tcs.TrySetResult(selectedWindow);
                    selectorWindow.Close();
                };

                selectorWindow.Closed += (_, _) =>
                {
                    tcs.TrySetResult(null);
                };

                selectorWindow.Show();
            });

            var selected = await tcs.Task;

            if (selected == null || selected.Handle == IntPtr.Zero)
            {
                return null;
            }

            // Get client bounds of the selected window
            var clientBounds = windowService.GetWindowClientBounds(selected.Handle);
            if (clientBounds.IsEmpty)
            {
                // Fallback to window bounds
                clientBounds = windowService.GetWindowBounds(selected.Handle);
            }

            if (clientBounds.IsEmpty)
            {
                return null;
            }

            return (selected.Handle, clientBounds);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ScrollingCapture window selection");
            return null;
        }
        finally
        {
            // Restore parent window
            if (wasVisible)
            {
                parentWindow.WindowState = previousState;
                parentWindow.Show();
                parentWindow.Activate();
            }
        }
    }

    private static bool CanUseOwner(Window? owner)
    {
        return owner != null &&
               owner.IsVisible &&
               owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
               owner.ShowInTaskbar;
    }

    private static async Task UploadCapturedImageAsync(SKBitmap image, TaskSettings? taskSettings)
    {
        try
        {
            var effectiveTaskSettings = taskSettings ?? SettingsManager.DefaultTaskSettings
                ?? new TaskSettings();

            // Create a new task to process the captured image through the pipeline
            await TaskManager.Instance.StartTask(effectiveTaskSettings, image);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ScrollingCapture upload");
        }
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
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ScrollingCapture toast");
        }
    }
}
