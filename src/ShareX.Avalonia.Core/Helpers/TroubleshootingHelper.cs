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

namespace ShareX.Ava.Core.Helpers
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
                var folder = GetTroubleshootingFolder(category);
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
        public static string GetTroubleshootingFolder(string category)
        {
            return Path.Combine(SettingManager.PersonalFolder, "Troubleshooting", category);
        }

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
                    var folder = GetTroubleshootingFolder(category);
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
