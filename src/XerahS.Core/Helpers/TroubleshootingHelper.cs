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

namespace XerahS.Core.Helpers
{
    /// <summary>
    /// Centralized troubleshooting and debug logging helper.
    /// All methods are compiled out in Release builds via [Conditional("DEBUG")].
    /// Call sites do not require #if DEBUG guards.
    /// </summary>
    public static class TroubleshootingHelper
    {
#if DEBUG
        private static readonly object _lock = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, StreamWriter> _writers = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _logPaths = new();
#endif

        /// <summary>
        /// Log a message to the troubleshooting log for the specified category.
        /// Creates category folder and session log file on first call.
        /// </summary>
        /// <param name="category">Category name (e.g., "RegionCapture", "CustomWindow")</param>
        /// <param name="message">Message to log</param>
        [Conditional("DEBUG")]
        public static void Log(string category, string message)
        {
#if DEBUG
            try
            {
                var writer = GetOrCreateWriter(category);
                if (writer != null)
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    writer.WriteLine($"[{timestamp}] {message}");
                }
            }
            catch
            {
                // Silently ignore logging errors
            }
#endif
        }

        /// <summary>
        /// Log a message with category prefix for detailed categorization within a log file.
        /// Format: [timestamp] SUBCATEGORY  | message
        /// </summary>
        /// <param name="category">Log file category (folder/file naming)</param>
        /// <param name="subCategory">Message subcategory (e.g., "INIT", "CAPTURE")</param>
        /// <param name="message">Message to log</param>
        [Conditional("DEBUG")]
        public static void Log(string category, string subCategory, string message)
        {
#if DEBUG
            try
            {
                var writer = GetOrCreateWriter(category);
                if (writer != null)
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    writer.WriteLine($"[{timestamp}] {subCategory,-12} | {message}");
                }
            }
            catch
            {
                // Silently ignore logging errors
            }
#endif
        }

        /// <summary>
        /// Log a message using a caller-managed StreamWriter.
        /// Useful for callers that need explicit control over writer lifecycle.
        /// </summary>
        /// <param name="writer">StreamWriter to write to (can be null)</param>
        /// <param name="subCategory">Message subcategory</param>
        /// <param name="message">Message to log</param>
        [Conditional("DEBUG")]
        public static void Log(StreamWriter? writer, string subCategory, string message)
        {
#if DEBUG
            if (writer == null) return;
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                writer.WriteLine($"[{timestamp}] {subCategory,-12} | {message}");
            }
            catch
            {
                // Silently ignore logging errors
            }
#endif
        }

        /// <summary>
        /// Get the current session log file path for a category.
        /// Returns null if no log has been created for this category.
        /// </summary>
        public static string? GetLogPath(string category)
        {
#if DEBUG
            return _logPaths.TryGetValue(category, out var path) ? path : null;
#else
            return null;
#endif
        }

        /// <summary>
        /// Create a new session log file for a category and return the path.
        /// Does not return a writer - use Log() methods to write.
        /// </summary>
        public static string? CreateSessionLog(string category)
        {
#if DEBUG
            try
            {
                var folder = GetTroubleshootingFolder();
                Directory.CreateDirectory(folder);

                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                var fileName = $"{category.ToLowerInvariant()}-{timestamp}.log";
                var path = Path.Combine(folder, fileName);

                // Create the writer immediately so it's ready
                var writer = new StreamWriter(path, append: true) { AutoFlush = true };
                _writers[category] = writer;
                _logPaths[category] = path;

                return path;
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>
        /// Get the troubleshooting folder path for a category.
        /// </summary>
        public static string GetTroubleshootingFolder()
        {
            return Path.Combine(SettingsManager.PersonalFolder, "Troubleshooting");
        }

        #region DPI Troubleshooting Logging

        /// <summary>
        /// Log all monitor information including DPI and resolution.
        /// Captures both Avalonia's screen.Scaling and Win32 GetDpiForMonitor values.
        /// </summary>
        /// <param name="category">Log file category</param>
        /// <param name="screens">Enumerable of Avalonia Platform.Screen objects</param>
        [Conditional("DEBUG")]
        public static void LogMonitorInfo(string category, IEnumerable<Avalonia.Platform.Screen> screens)
        {
#if DEBUG
            try
            {
                Log(category, "MONITORS", "=== Monitor Configuration ===");
                int index = 0;
                foreach (var screen in screens)
                {
                    // Get Win32 DPI for comparison
                    var centerX = screen.Bounds.X + screen.Bounds.Width / 2;
                    var centerY = screen.Bounds.Y + screen.Bounds.Height / 2;
                    XerahS.Common.NativeMethods.TryGetMonitorDpi(centerX, centerY, out uint dpiX, out uint dpiY);
                    double win32Scale = dpiX / 96.0;

                    Log(category, "MONITORS", $"Screen {index}: " +
                        $"Bounds=({screen.Bounds.X},{screen.Bounds.Y}) {screen.Bounds.Width}x{screen.Bounds.Height}, " +
                        $"IsPrimary={screen.IsPrimary}, " +
                        $"Avalonia.Scaling={screen.Scaling:F3}, " +
                        $"Win32.DPI={dpiX}x{dpiY} (Scale={win32Scale:F3})");
                    index++;
                }
                Log(category, "MONITORS", $"Total monitors: {index}");
            }
            catch (Exception ex)
            {
                Log(category, "ERROR", $"LogMonitorInfo failed: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Log window selection coordinate conversion details.
        /// Critical for diagnosing DPI-related selection rectangle misalignment.
        /// </summary>
        /// <param name="category">Log file category</param>
        /// <param name="windowTitle">Title of the window being selected</param>
        /// <param name="processId">Process ID of the window</param>
        /// <param name="physicalBounds">Physical bounds returned by GetWindowRectangle</param>
        /// <param name="overlayScaling">Current overlay RenderScaling</param>
        /// <param name="logicalX">Computed logical X coordinate</param>
        /// <param name="logicalY">Computed logical Y coordinate</param>
        /// <param name="logicalW">Computed logical width</param>
        /// <param name="logicalH">Computed logical height</param>
        /// <param name="screenIndex">Index of the screen containing the window</param>
        /// <param name="screenScaling">Scaling factor of that screen</param>
        [Conditional("DEBUG")]
        public static void LogWindowSelection(string category, string windowTitle, int processId,
            System.Drawing.Rectangle physicalBounds, double overlayScaling,
            double logicalX, double logicalY, double logicalW, double logicalH,
            int screenIndex, double screenScaling)
        {
#if DEBUG
            try
            {
                // Get per-monitor DPI at window center for comparison
                var centerX = physicalBounds.X + physicalBounds.Width / 2;
                var centerY = physicalBounds.Y + physicalBounds.Height / 2;
                double perMonitorScale = XerahS.Common.NativeMethods.GetMonitorScaleFactorFromPoint(centerX, centerY);

                // Calculate what the logical coords would be using per-monitor DPI
                double altLogicalX = (physicalBounds.X - (logicalX * overlayScaling)) / perMonitorScale;
                double altLogicalW = physicalBounds.Width / perMonitorScale;

                var titleSnippet = string.IsNullOrEmpty(windowTitle) ? "(no title)" :
                    (windowTitle.Length > 40 ? windowTitle.Substring(0, 40) + "..." : windowTitle);

                Log(category, "SELECTION", $"Window: \"{titleSnippet}\" (PID={processId})");
                Log(category, "SELECTION", $"  Physical: ({physicalBounds.X},{physicalBounds.Y}) {physicalBounds.Width}x{physicalBounds.Height}");
                Log(category, "SELECTION", $"  Overlay RenderScaling: {overlayScaling:F3}");
                Log(category, "SELECTION", $"  Screen[{screenIndex}] Scaling: {screenScaling:F3}");
                Log(category, "SELECTION", $"  Per-monitor DPI scale: {perMonitorScale:F3}");
                Log(category, "SELECTION", $"  Computed logical: ({logicalX:F1},{logicalY:F1}) {logicalW:F1}x{logicalH:F1}");
                Log(category, "SELECTION", $"  Alt (per-monitor): ({altLogicalX:F1},?) {altLogicalW:F1}x?");

                if (Math.Abs(overlayScaling - perMonitorScale) > 0.01)
                {
                    Log(category, "WARNING", $"  ** SCALING MISMATCH: Overlay={overlayScaling:F3} vs PerMonitor={perMonitorScale:F3} **");
                }
            }
            catch (Exception ex)
            {
                Log(category, "ERROR", $"LogWindowSelection failed: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Log environment and configuration details.
        /// Captures OS version, DPI awareness, and monitor arrangement.
        /// </summary>
        /// <param name="category">Log file category</param>
        [Conditional("DEBUG")]
        public static void LogEnvironment(string category)
        {
#if DEBUG
            try
            {
                Log(category, "ENVIRONMENT", "=== Environment Details ===");
                Log(category, "ENVIRONMENT", $"Machine: {Environment.MachineName}");
                Log(category, "ENVIRONMENT", $"User: {Environment.UserName}");
                Log(category, "ENVIRONMENT", $"OS: {Environment.OSVersion.VersionString}");
                Log(category, "ENVIRONMENT", $"OS Version: {Environment.OSVersion.Version}");
                Log(category, "ENVIRONMENT", $".NET: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
                Log(category, "ENVIRONMENT", $"Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
                Log(category, "ENVIRONMENT", $"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");

                // Note about DPI awareness - actual value would require additional P/Invoke
                Log(category, "ENVIRONMENT", "DPI Awareness: (check app manifest for dpiAwareness setting)");

                // Log current time for correlation with user observations
                Log(category, "ENVIRONMENT", $"Log time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
            }
            catch (Exception ex)
            {
                Log(category, "ERROR", $"LogEnvironment failed: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Log virtual screen bounds and overlay dimensions.
        /// </summary>
        /// <param name="category">Log file category</param>
        /// <param name="virtualMinX">Virtual screen minimum X</param>
        /// <param name="virtualMinY">Virtual screen minimum Y</param>
        /// <param name="virtualMaxX">Virtual screen maximum X</param>
        /// <param name="virtualMaxY">Virtual screen maximum Y</param>
        /// <param name="overlayWidth">Overlay window width</param>
        /// <param name="overlayHeight">Overlay window height</param>
        /// <param name="overlayScaling">Overlay RenderScaling</param>
        [Conditional("DEBUG")]
        public static void LogVirtualScreenBounds(string category,
            int virtualMinX, int virtualMinY, int virtualMaxX, int virtualMaxY,
            double overlayWidth, double overlayHeight, double overlayScaling)
        {
#if DEBUG
            try
            {
                var virtualWidth = virtualMaxX - virtualMinX;
                var virtualHeight = virtualMaxY - virtualMinY;

                Log(category, "VIRTUAL", "=== Virtual Screen Bounds ===");
                Log(category, "VIRTUAL", $"Virtual screen: ({virtualMinX},{virtualMinY}) to ({virtualMaxX},{virtualMaxY})");
                Log(category, "VIRTUAL", $"Virtual size: {virtualWidth}x{virtualHeight} px");
                Log(category, "VIRTUAL", $"Overlay size: {overlayWidth:F0}x{overlayHeight:F0} logical");
                Log(category, "VIRTUAL", $"Overlay RenderScaling: {overlayScaling:F3}");

                // Check if overlay covers virtual screen
                var expectedLogicalWidth = virtualWidth / overlayScaling;
                var expectedLogicalHeight = virtualHeight / overlayScaling;
                var widthDiff = Math.Abs(overlayWidth - expectedLogicalWidth);
                var heightDiff = Math.Abs(overlayHeight - expectedLogicalHeight);

                if (widthDiff > 5 || heightDiff > 5)
                {
                    Log(category, "WARNING", $"  ** OVERLAY SIZE MISMATCH: Expected {expectedLogicalWidth:F0}x{expectedLogicalHeight:F0}, " +
                        $"Got {overlayWidth:F0}x{overlayHeight:F0} (diff: {widthDiff:F0}x{heightDiff:F0}) **");
                }
            }
            catch (Exception ex)
            {
                Log(category, "ERROR", $"LogVirtualScreenBounds failed: {ex.Message}");
            }
#endif
        }

        #endregion DPI Troubleshooting Logging

        /// <summary>
        /// Close all active writers. Call on application shutdown.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Shutdown()
        {
#if DEBUG
            foreach (var kvp in _writers)
            {
                try
                {
                    kvp.Value.Flush();
                    kvp.Value.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _writers.Clear();
            _logPaths.Clear();
#endif
        }

#if DEBUG
        private static StreamWriter? GetOrCreateWriter(string category)
        {
            if (_writers.TryGetValue(category, out var existingWriter))
            {
                return existingWriter;
            }

            lock (_lock)
            {
                // Double-check after acquiring lock
                if (_writers.TryGetValue(category, out existingWriter))
                {
                    return existingWriter;
                }

                try
                {
                    var folder = GetTroubleshootingFolder();
                    Directory.CreateDirectory(folder);

                    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                    var fileName = $"{category.ToLowerInvariant()}-{timestamp}.log";
                    var path = Path.Combine(folder, fileName);

                    var writer = new StreamWriter(path, append: true) { AutoFlush = true };
                    _writers[category] = writer;
                    _logPaths[category] = path;

                    return writer;
                }
                catch
                {
                    return null;
                }
            }
        }
#endif
    }
}
