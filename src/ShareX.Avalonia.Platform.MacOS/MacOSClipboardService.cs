#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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
using System.Drawing;
using System.Threading.Tasks;
using ShareX.Ava.Common;
using ShareX.Ava.Platform.Abstractions;

namespace ShareX.Ava.Platform.MacOS
{
    /// <summary>
    /// macOS clipboard service using pbcopy/pbpaste for text (MVP).
    /// </summary>
    public class MacOSClipboardService : IClipboardService
    {
        private static bool _loggedUnsupported;

        public void Clear()
        {
            SetText(string.Empty);
        }

        public bool ContainsText()
        {
            var text = GetText();
            return !string.IsNullOrEmpty(text);
        }

        public bool ContainsImage()
        {
            LogUnsupported("ContainsImage");
            return false;
        }

        public bool ContainsFileDropList()
        {
            LogUnsupported("ContainsFileDropList");
            return false;
        }

        public string? GetText()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pbpaste",
                    Arguments = string.Empty,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSClipboardService.GetText failed");
                return null;
            }
        }

        public void SetText(string text)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pbcopy",
                    Arguments = string.Empty,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return;
                }

                process.StandardInput.Write(text ?? string.Empty);
                process.StandardInput.Close();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSClipboardService.SetText failed");
            }
        }

        public Image? GetImage()
        {
            LogUnsupported("GetImage");
            return null;
        }

        public void SetImage(Image image)
        {
            LogUnsupported("SetImage");
        }

        public string[]? GetFileDropList()
        {
            LogUnsupported("GetFileDropList");
            return null;
        }

        public void SetFileDropList(string[] files)
        {
            LogUnsupported("SetFileDropList");
        }

        public object? GetData(string format)
        {
            LogUnsupported("GetData");
            return null;
        }

        public void SetData(string format, object data)
        {
            LogUnsupported("SetData");
        }

        public bool ContainsData(string format)
        {
            LogUnsupported("ContainsData");
            return false;
        }

        public Task<string?> GetTextAsync()
        {
            return Task.Run(GetText);
        }

        public Task SetTextAsync(string text)
        {
            return Task.Run(() => SetText(text));
        }

        private static void LogUnsupported(string member)
        {
            if (_loggedUnsupported)
            {
                return;
            }

            _loggedUnsupported = true;
            DebugHelper.WriteLine($"MacOSClipboardService: {member} is not implemented yet.");
        }
    }
}
