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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace XerahS.Common
{
    public class Logger : IDisposable
    {
        public delegate void MessageAddedEventHandler(string message);

        public event MessageAddedEventHandler? MessageAdded;

        public string MessageFormat { get; set; } = "{0:yyyy-MM-dd HH:mm:ss.fff} - {1}";
        public bool AsyncWrite { get; set; } = true;
        public bool DebugWrite { get; set; } = true;
#if DEBUG
        public bool ConsoleWrite { get; set; } = true;
#else
        public bool ConsoleWrite { get; set; } = false;
#endif
        public bool StringWrite { get; set; } = true;
        public bool FileWrite { get; set; } = false;
        public string LogFilePath { get; private set; } = string.Empty;
        public string LogFilePathTemplate { get; private set; } = string.Empty;

        private readonly object loggerLock = new object();
        private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        private StringBuilder sbMessages = new StringBuilder();
        private string _currentDate = DateTime.Now.ToString("yyyy-MM-dd");
        private string _baseFileName = string.Empty;
        private bool _disposed = false;
        private int _consecutiveFileWriteFailures = 0;
        private const int MaxFileWriteFailures = 5;
        private static readonly Regex DateSuffixRegex = new Regex(@"-\d{8}$", RegexOptions.Compiled);

        public Logger()
        {
        }

        public Logger(string logFilePath)
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                FileWrite = true;
                LogFilePathTemplate = logFilePath;
                LogFilePath = logFilePath;
                _currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                // Extract base filename without date suffix (e.g., "XerahS-20260209" -> "XerahS")
                string filename = Path.GetFileNameWithoutExtension(logFilePath);
                _baseFileName = DateSuffixRegex.Replace(filename, string.Empty);

                string? directory = Path.GetDirectoryName(LogFilePath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        protected void OnMessageAdded(string message)
        {
            MessageAdded?.Invoke(message);
        }

        private string GetCurrentLogFilePath()
        {
            // Check if we need to rotate to a new date
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (today != _currentDate)
            {
                _currentDate = today;

                // Build the path with yyyy-MM folder structure
                string baseTemplate = LogFilePathTemplate;
                string? directory = Path.GetDirectoryName(baseTemplate);
                string extension = Path.GetExtension(baseTemplate);

                // Extract the base directory (before Logs folder)
                string currentMonthFolder = DateTime.Now.ToString("yyyy-MM");

                // Reconstruct path: replace the date in the directory structure
                if (!string.IsNullOrEmpty(directory) && directory.Contains("Logs"))
                {
                    // Find the Logs folder in the path
                    int logsIndex = directory.LastIndexOf("Logs");
                    if (logsIndex >= 0)
                    {
                        string baseDir = directory.Substring(0, logsIndex);
                        directory = Path.Combine(baseDir, "Logs", currentMonthFolder);
                    }
                }

                string logDirectory = directory ?? string.Empty;
                // Use _baseFileName (without date suffix) to avoid duplicate dates like "XerahS-20260209-2026-02-10.log"
                LogFilePath = Path.Combine(logDirectory, $"{_baseFileName}-{today}{extension}");
            }

            return LogFilePath;
        }

        public void ProcessMessageQueue()
        {
            lock (loggerLock)
            {
                while (messageQueue.TryDequeue(out string? message))
                {
                    if (message == null)
                    {
                        continue;
                    }

                    if (DebugWrite)
                    {
                        Debug.Write(message);
                    }

                    if (ConsoleWrite)
                    {
                        Console.Write(message);
                    }

                    if (StringWrite && sbMessages != null)
                    {
                        sbMessages.Append(message);
                    }

                    if (FileWrite && !string.IsNullOrEmpty(LogFilePathTemplate))
                    {
                        try
                        {
                            string currentLogPath = GetCurrentLogFilePath();
                            string? directory = Path.GetDirectoryName(currentLogPath);

                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            File.AppendAllText(currentLogPath, message, Encoding.UTF8);
                            _consecutiveFileWriteFailures = 0; // Reset on success
                        }
                        catch (Exception e)
                        {
                            _consecutiveFileWriteFailures++;
                            Debug.WriteLine($"Logger file write failed ({_consecutiveFileWriteFailures}/{MaxFileWriteFailures}): {e.Message}");

                            if (_consecutiveFileWriteFailures >= MaxFileWriteFailures)
                            {
                                FileWrite = false;
                                Debug.WriteLine($"Logger: Disabled file writing after {MaxFileWriteFailures} consecutive failures");
                            }
                        }
                    }

                    OnMessageAdded(message);
                }
            }
        }

        public void Write(string message)
        {
            if (message != null)
            {
                // Capture format once to avoid race condition if MessageFormat changes
                string format = MessageFormat;
                try
                {
                    message = string.Format(format, DateTime.Now, message);
                }
                catch (FormatException ex)
                {
                    // Fallback if format string is invalid or changed concurrently
                    message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
                    Debug.WriteLine($"Logger format error: {ex.Message}");
                }

                messageQueue.Enqueue(message);

                if (AsyncWrite)
                {
                    Task.Run(() => ProcessMessageQueue());
                }
                else
                {
                    ProcessMessageQueue();
                }
            }
        }

        public void Write(string format, params object[] args)
        {
            Write(string.Format(format, args));
        }

        public void WriteLine(string message)
        {
            Write(message + Environment.NewLine);
        }

        public void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        public void WriteException(string exception, string message = "Exception")
        {
            WriteLine($"{message}:{Environment.NewLine}{exception}");
        }

        public void WriteException(Exception exception, string message = "Exception")
        {
            WriteException(exception.ToString(), message);
        }

        public void Clear()
        {
            lock (loggerLock)
            {
                if (sbMessages != null)
                {
                    sbMessages.Clear();
                }
            }
        }

        public override string ToString()
        {
            lock (loggerLock)
            {
                if (sbMessages != null && sbMessages.Length > 0)
                {
                    return sbMessages.ToString();
                }

                return string.Empty;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Flush remaining messages before disposal
                try
                {
                    // Disable async writes to ensure synchronous flush
                    bool wasAsync = AsyncWrite;
                    AsyncWrite = false;

                    // Process any remaining queued messages
                    ProcessMessageQueue();

                    AsyncWrite = wasAsync;
                }
                catch (Exception ex)
                {
                    // Log disposal errors to Debug output as fallback
                    Debug.WriteLine($"Logger disposal error: {ex.Message}");
                }
            }

            _disposed = true;
        }
    }
}
