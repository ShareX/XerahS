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
using System.IO;
using System.Threading.Tasks;
using ShareX.Ava.Common;
using ShareX.Ava.Platform.Abstractions;

namespace ShareX.Ava.Platform.MacOS
{
    /// <summary>
    /// macOS clipboard service using pbcopy/pbpaste for text and osascript for PNG images (MVP).
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
            try
            {
                return TryExportClipboardImage(out _);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSClipboardService.ContainsImage failed");
                return false;
            }
        }

        public bool ContainsFileDropList()
        {
            var files = GetFileDropList();
            return files != null && files.Length > 0;
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
            if (!TryExportClipboardImage(out var tempFile))
            {
                return null;
            }

            try
            {
                using var fileStream = File.OpenRead(tempFile);
                using var tempImage = Image.FromStream(fileStream);
                return new Bitmap(tempImage);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSClipboardService.GetImage failed");
                return null;
            }
            finally
            {
                TryDelete(tempFile);
            }
        }

        public void SetImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_ava_clip_{Guid.NewGuid():N}.png");
            try
            {
                image.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);
                var script = $"set the clipboard to (read (POSIX file \\\"{tempFile}\\\") as «class PNGf»)";
                RunOsaScript(script);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSClipboardService.SetImage failed");
            }
            finally
            {
                TryDelete(tempFile);
            }
        }

        public string[]? GetFileDropList()
        {
            try
            {
                var script = "try\n" +
                             "set fileList to the clipboard as list\n" +
                             "set output to \"\"\n" +
                             "repeat with f in fileList\n" +
                             "set output to output & (POSIX path of f) & \"\\n\"\n" +
                             "end repeat\n" +
                             "return output\n" +
                             "end try\n" +
                             "return \"\"";

                var output = RunOsaScriptWithOutput(script);
                if (string.IsNullOrWhiteSpace(output))
                {
                    return null;
                }

                var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                return lines.Length == 0 ? null : lines;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSClipboardService.GetFileDropList failed");
                return null;
            }
        }

        public void SetFileDropList(string[] files)
        {
            if (files == null || files.Length == 0)
            {
                return;
            }

            try
            {
                var fileList = string.Join(", ", BuildPosixFileList(files));
                var script = $"set the clipboard to {{{fileList}}}";
                RunOsaScript(script);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSClipboardService.SetFileDropList failed");
            }
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

        private static bool TryExportClipboardImage(out string tempFile)
        {
            tempFile = Path.Combine(Path.GetTempPath(), $"sharex_ava_clip_{Guid.NewGuid():N}.png");
            var script = $"try\nwrite (the clipboard as «class PNGf») to (POSIX file \\\"{tempFile}\\\")\nreturn \\\"1\\\"\nend try\nreturn \\\"0\\\"";

            var result = RunOsaScript(script);
            if (!result || !File.Exists(tempFile))
            {
                TryDelete(tempFile);
                return false;
            }

            return true;
        }

        private static bool RunOsaScript(string script)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }

        private static string? RunOsaScriptWithOutput(string script)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{script}\"",
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

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }

        private static IEnumerable<string> BuildPosixFileList(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                var escaped = file.Replace("\\", "\\\\").Replace("\"", "\\\"");
                yield return $"POSIX file \\\"{escaped}\\\"";
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
