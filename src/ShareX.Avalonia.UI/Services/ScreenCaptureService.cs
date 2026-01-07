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

using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;
using ShareX.Ava.Core;
using ShareX.Ava.Core.Helpers;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.UI.Views.RegionCapture;
using SkiaSharp;

namespace ShareX.Ava.UI.Services
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
            bool effectiveModern = options?.UseModernCapture ?? SettingManager.Settings.DefaultTaskSettings.CaptureSettings.UseModernCapture;
            TroubleshootingHelper.Log("RegionCapture", "CONFIG", $"Capture Configuration: UseModernCapture={effectiveModern} (Explicit={options?.UseModernCapture.ToString() ?? "null"}), ShowCursor={options?.ShowCursor ?? SettingManager.Settings.DefaultTaskSettings.CaptureSettings.ShowCursor}");
            
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
