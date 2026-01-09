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

using Avalonia.Threading;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views.RegionCapture;
using SkiaSharp;
using System.Diagnostics;

namespace XerahS.UI.Services
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly IScreenCaptureService _platformImpl;

        public ScreenCaptureService(IScreenCaptureService platformImpl)
        {
            _platformImpl = platformImpl;
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureRectAsync(rect, options);
        }

        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            var totalStopwatch = Stopwatch.StartNew();
            TroubleshootingHelper.Log("FullscreenCapture", "INIT", "Fullscreen capture started");

            var captureStopwatch = Stopwatch.StartNew();
            var result = await _platformImpl.CaptureFullScreenAsync(options);
            captureStopwatch.Stop();

            var resultText = result == null ? "null" : $"{result.Width}x{result.Height}";
            TroubleshootingHelper.Log("FullscreenCapture", "CAPTURE", $"Platform capture finished in {captureStopwatch.ElapsedMilliseconds}ms, Result={resultText}");
            totalStopwatch.Stop();
            TroubleshootingHelper.Log("FullscreenCapture", "TOTAL", $"Fullscreen capture total elapsed: {totalStopwatch.ElapsedMilliseconds}ms");

            return result;
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureActiveWindowAsync(windowService, options);
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            SKRectI selection = SKRectI.Empty;
            var regionStopwatch = Stopwatch.StartNew();

            // Show UI window on UI thread
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var window = new RegionCaptureWindow();

                // Window will handle background capture in OnOpened
                window.Show();
                selection = await window.GetResultAsync();
            });

            if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
            {
                TroubleshootingHelper.Log("RegionCapture", "CANCEL", "Selection was empty or cancelled. Aborting.");
                return null;
            }

            TroubleshootingHelper.Log("RegionCapture", "SELECTION", $"Received selection: {selection}, Delaying 200ms...");

            // Small delay to allow window to close fully
            await Task.Delay(200);

            // Delegate capture to platform implementation
            var workflowTaskSettings = SettingManager.GetOrCreateWorkflowTaskSettings(HotkeyType.None);
            bool effectiveModern = options?.UseModernCapture ?? workflowTaskSettings.CaptureSettings.UseModernCapture;
            TroubleshootingHelper.Log("RegionCapture", "CONFIG", $"Capture Configuration: UseModernCapture={effectiveModern} (Explicit={options?.UseModernCapture.ToString() ?? "null"}), ShowCursor={options?.ShowCursor ?? workflowTaskSettings.CaptureSettings.ShowCursor}");

            TroubleshootingHelper.Log("RegionCapture", "CAPTURE", "Calling Platform.CaptureRectAsync...");
            var skRect = new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom);

            var captureStopwatch = Stopwatch.StartNew();
            var result = await _platformImpl.CaptureRectAsync(skRect, options);
            captureStopwatch.Stop();

            TroubleshootingHelper.Log("RegionCapture", "CAPTURE", $"Platform.CaptureRectAsync returned in {captureStopwatch.ElapsedMilliseconds}ms. Result={(result != null ? "Bitmap" : "Null")}");

            regionStopwatch.Stop();
            TroubleshootingHelper.Log("RegionCapture", "TOTAL", $"Total region capture workflow time: {regionStopwatch.ElapsedMilliseconds}ms");

            return result;
        }

        public Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureWindowAsync(windowHandle, windowService, options);
        }
    }
}
