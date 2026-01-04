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
using ShareX.Ava.Platform.Abstractions;

namespace ShareX.Ava.Platform.MacOS
{
    /// <summary>
    /// macOS screen capture implementation using the native screencapture CLI.
    /// </summary>
    public class MacOSScreenshotService : IScreenCaptureService
    {
        public Task<Image?> CaptureRegionAsync()
        {
            return CaptureWithArgumentsAsync("-i -t png");
        }

        public Task<Image?> CaptureRectAsync(Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return Task.FromResult<Image?>(null);
            }

            var args = $"-R{rect.X},{rect.Y},{rect.Width},{rect.Height} -x -t png";
            return CaptureWithArgumentsAsync(args);
        }

        public Task<Image?> CaptureFullScreenAsync()
        {
            return CaptureWithArgumentsAsync("-x -t png");
        }

        public Task<Image?> CaptureActiveWindowAsync(IWindowService windowService)
        {
            return CaptureWithArgumentsAsync("-w -t png");
        }

        private static Task<Image?> CaptureWithArgumentsAsync(string arguments)
        {
            return Task.Run<Image?>(() =>
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_ava_{Guid.NewGuid():N}.png");

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "screencapture",
                        Arguments = $"{arguments} \"{tempFile}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        return null;
                    }

                    process.WaitForExit();
                    if (process.ExitCode != 0 || !File.Exists(tempFile))
                    {
                        return null;
                    }

                    using var fileStream = File.OpenRead(tempFile);
                    using var tempImage = Image.FromStream(fileStream);
                    return new Bitmap(tempImage);
                }
                catch
                {
                    return null;
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch
                        {
                        }
                    }
                }
            });
        }
    }
}
