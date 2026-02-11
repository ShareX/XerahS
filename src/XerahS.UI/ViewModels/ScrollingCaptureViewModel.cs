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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Services;
using BitmapConversionHelpers = ShareX.ImageEditor.Helpers.BitmapConversionHelpers;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;

namespace XerahS.UI.ViewModels;

public partial class ScrollingCaptureViewModel : ViewModelBase
{
    private CancellationTokenSource? _captureCts;
    private SKBitmap? _capturedSkBitmap;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _previewImage;

    [ObservableProperty]
    private string _statusText = "Ready. Click Capture to begin.";

    [ObservableProperty]
    private ScrollingCaptureStatus _status;

    [ObservableProperty]
    private string _resultSize = "";

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private int _framesCaptured;

    [ObservableProperty]
    private bool _hasResult;

    /// <summary>
    /// Callback to select a window and return its handle + client bounds.
    /// Set by the tool service.
    /// </summary>
    public Func<Task<(IntPtr Handle, System.Drawing.Rectangle ClientBounds)?>>? SelectWindowRequested { get; set; }

    /// <summary>
    /// Callback to upload the captured image through the task pipeline.
    /// Set by the tool service.
    /// </summary>
    public Func<SKBitmap, Task>? UploadRequested { get; set; }

    [RelayCommand]
    private async Task CaptureAsync()
    {
        if (IsCapturing)
        {
            StopCapture();
            return;
        }

        if (!PlatformServices.IsInitialized || PlatformServices.ScrollingCapture == null)
        {
            StatusText = "Scrolling capture is not supported on this platform.";
            return;
        }

        if (!PlatformServices.ScrollingCapture.IsSupported)
        {
            StatusText = "Scrolling capture is not supported on this platform.";
            return;
        }

        // Select target window
        if (SelectWindowRequested == null)
        {
            StatusText = "Window selection not available.";
            return;
        }

        var selection = await SelectWindowRequested();
        if (selection == null)
        {
            StatusText = "Window selection was cancelled.";
            return;
        }

        var (windowHandle, clientBounds) = selection.Value;
        if (windowHandle == IntPtr.Zero || clientBounds.IsEmpty)
        {
            StatusText = "Invalid window selected.";
            return;
        }

        // Get options from settings
        var options = SettingsManager.DefaultTaskSettings?.CaptureSettings?.ScrollingCaptureOptions
            ?? new ScrollingCaptureOptions();

        // Start capture
        IsCapturing = true;
        FramesCaptured = 0;
        HasResult = false;
        StatusText = "Capturing...";
        _captureCts = new CancellationTokenSource();

        try
        {
            var captureRegion = new SKRect(
                clientBounds.X,
                clientBounds.Y,
                clientBounds.Right,
                clientBounds.Bottom);

            var manager = new ScrollingCaptureManager(
                PlatformServices.ScrollingCapture,
                PlatformServices.ScreenCapture,
                PlatformServices.Window);

            var progress = new Progress<ScrollingCaptureProgress>(p =>
            {
                FramesCaptured = p.FramesCaptured;
                StatusText = $"Capturing frame {p.FramesCaptured}...";
            });

            var result = await manager.CaptureAsync(
                windowHandle,
                captureRegion,
                scrollMethod: options.ScrollMethod,
                scrollAmount: options.ScrollAmount,
                startDelayMs: options.StartDelay,
                scrollDelayMs: options.ScrollDelay,
                autoScrollTop: options.AutoScrollTop,
                autoIgnoreBottomEdge: options.AutoIgnoreBottomEdge,
                progress: progress,
                cancellationToken: _captureCts.Token);

            // Store result
            _capturedSkBitmap?.Dispose();
            _capturedSkBitmap = result.Image;
            Status = result.Status;
            FramesCaptured = result.FramesCaptured;

            if (result.Image != null)
            {
                PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(result.Image);
                ResultSize = $"{result.Image.Width} x {result.Image.Height}";
                HasResult = true;

                StatusText = result.Status switch
                {
                    ScrollingCaptureStatus.Successful => $"Capture complete. {result.FramesCaptured} frames stitched.",
                    ScrollingCaptureStatus.PartiallySuccessful => $"Capture complete with some estimation. {result.FramesCaptured} frames.",
                    ScrollingCaptureStatus.Failed => $"Capture completed but overlap detection failed. {result.FramesCaptured} frames.",
                    _ => "Capture complete."
                };
            }
            else
            {
                StatusText = "Capture failed - no image produced.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Capture was cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Capture failed: {ex.Message}";
            DebugHelper.WriteException(ex, "ScrollingCapture");
        }
        finally
        {
            IsCapturing = false;
            _captureCts?.Dispose();
            _captureCts = null;
        }
    }

    [RelayCommand]
    private void StopCapture()
    {
        _captureCts?.Cancel();
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (_capturedSkBitmap == null || UploadRequested == null) return;
        await UploadRequested(_capturedSkBitmap.Copy());
        StatusText = "Image sent to upload pipeline.";
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (_capturedSkBitmap == null) return;

        try
        {
            PlatformServices.Clipboard.SetImage(_capturedSkBitmap);
            StatusText = "Image copied to clipboard.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to copy: {ex.Message}";
        }
    }

    public void Cleanup()
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _capturedSkBitmap?.Dispose();
        PreviewImage = null;
    }
}

