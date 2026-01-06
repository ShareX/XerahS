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

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using ShareX.Ava.Core;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.UI.Views.RegionCapture;
using SkiaSharp;
// REMOVED: System.Drawing (except for temporary conversion if needed, but strict replacement preferred if possible)

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
#if DEBUG
            var totalStopwatch = Stopwatch.StartNew();
            StreamWriter? debugLog = null;
            try
            {
                debugLog = CreateFullscreenDebugLog();
                DebugLog(debugLog, "INIT", "Fullscreen capture started");
            }
            catch
            {
                debugLog = null;
            }
#endif

            var captureStopwatch = Stopwatch.StartNew();
            var result = await _platformImpl.CaptureFullScreenAsync(options);
            captureStopwatch.Stop();

#if DEBUG
            var resultText = result == null ? "null" : $"{result.Width}x{result.Height}";
            DebugLog(debugLog, "CAPTURE", $"Platform capture finished in {captureStopwatch.ElapsedMilliseconds}ms, Result={resultText}");
            totalStopwatch.Stop();
            DebugLog(debugLog, "TOTAL", $"Fullscreen capture total elapsed: {totalStopwatch.ElapsedMilliseconds}ms");
            debugLog?.Flush();
            debugLog?.Dispose();
#endif

            return result;
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureActiveWindowAsync(windowService, options);
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            SKRectI selection = SKRectI.Empty;
            string? logPath = null;
            var regionStopwatch = Stopwatch.StartNew();

            // Show UI window on UI thread
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var window = new RegionCaptureWindow();
#if DEBUG
                logPath = window.DebugLogPath;
#endif
                
                // Window will handle background capture in OnOpened
                window.Show();
                selection = await window.GetResultAsync();
            });

            if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
            {
                AppendLog(logPath, "CANCEL", "Selection was empty or cancelled. Aborting.");
                return null;
            }

            AppendLog(logPath, "SELECTION", $"Received selection: {selection}, Delaying 200ms...");

            // Small delay to allow window to close fully
            await Task.Delay(200);

            // Delegate capture to platform implementation
            bool effectiveModern = options?.UseModernCapture ?? SettingManager.Settings.DefaultTaskSettings.CaptureSettings.UseModernCapture;
            AppendLog(logPath, "CONFIG", $"Capture Configuration: UseModernCapture={effectiveModern} (Explicit={options?.UseModernCapture.ToString() ?? "null"}), ShowCursor={options?.ShowCursor ?? SettingManager.Settings.DefaultTaskSettings.CaptureSettings.ShowCursor}");
            
            AppendLog(logPath, "CAPTURE", "Calling Platform.CaptureRectAsync...");
            var skRect = new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom);
            
            var captureStopwatch = Stopwatch.StartNew();
            var result = await _platformImpl.CaptureRectAsync(skRect, options);
            captureStopwatch.Stop();
            
            AppendLog(logPath, "CAPTURE", $"Platform.CaptureRectAsync returned in {captureStopwatch.ElapsedMilliseconds}ms. Result={(result != null ? "Bitmap" : "Null")}");
            
            regionStopwatch.Stop();
            AppendLog(logPath, "TOTAL", $"Total region capture workflow time: {regionStopwatch.ElapsedMilliseconds}ms");

            return result;
        }

        [Conditional("DEBUG")]
        private void AppendLog(string? path, string category, string message)
        {
#if DEBUG
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                using var sw = new StreamWriter(path, append: true);
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                sw.WriteLine($"[{timestamp}] {category,-12} | {message}");
            }
            catch { }
#endif
        }

#if DEBUG
        private static StreamWriter CreateFullscreenDebugLog()
        {
            var debugFolder = Path.Combine(SettingManager.PersonalFolder, "debug");
            Directory.CreateDirectory(debugFolder);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var path = Path.Combine(debugFolder, $"fullscreen-capture-{timestamp}.log");
            return new StreamWriter(path, append: true) { AutoFlush = true };
        }

        [Conditional("DEBUG")]
        private static void DebugLog(StreamWriter? writer, string category, string message)
        {
            if (writer == null)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            writer.WriteLine($"[{timestamp}] {category,-12} | {message}");
        }
#endif
    }
}
