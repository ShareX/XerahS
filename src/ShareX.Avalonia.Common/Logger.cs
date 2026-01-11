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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace XerahS.Common
{
    public class Logger
    {
        public delegate void MessageAddedEventHandler(string message);

        public event MessageAddedEventHandler MessageAdded;

        public string MessageFormat { get; set; } = "{0:yyyy-MM-dd HH:mm:ss.fff} - {1}";
        public bool AsyncWrite { get; set; } = true;
        public bool DebugWrite { get; set; } = true;
        public bool StringWrite { get; set; } = true;
        public bool FileWrite { get; set; } = false;
        public string LogFilePath { get; private set; }
        public string LogFilePathTemplate { get; private set; }

        private readonly object loggerLock = new object();
        private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        private StringBuilder sbMessages = new StringBuilder();
        private string _currentDate;

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

                string directory = Path.GetDirectoryName(LogFilePath);

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
                string directory = Path.GetDirectoryName(baseTemplate);
                string filename = Path.GetFileNameWithoutExtension(baseTemplate);
                string extension = Path.GetExtension(baseTemplate);

                // Extract the base directory (before Logs folder)
                string currentMonthFolder = DateTime.Now.ToString("yyyy-MM");

                // Reconstruct path: replace the date in the directory structure
                if (directory.Contains("Logs"))
                {
                    // Find the Logs folder in the path
                    int logsIndex = directory.LastIndexOf("Logs");
                    if (logsIndex >= 0)
                    {
                        string baseDir = directory.Substring(0, logsIndex);
                        directory = Path.Combine(baseDir, "Logs", currentMonthFolder);
                    }
                }

                LogFilePath = Path.Combine(directory, $"{filename}-{today}{extension}");
            }

            return LogFilePath;
        }

        public void ProcessMessageQueue()
        {
            lock (loggerLock)
            {
                while (messageQueue.TryDequeue(out string message))
                {
                    if (DebugWrite)
                    {
                        Debug.Write(message);
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
                            string directory = Path.GetDirectoryName(currentLogPath);

                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            File.AppendAllText(currentLogPath, message, Encoding.UTF8);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
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
                message = string.Format(MessageFormat, DateTime.Now, message);
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

                return null;
            }
        }
    }
}
