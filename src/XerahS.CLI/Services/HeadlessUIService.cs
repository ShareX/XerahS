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

using SkiaSharp;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Services
{
    /// <summary>
    /// Minimal IUIService implementation for headless CLI execution.
    /// Editor and AfterCapture UI cannot be shown in CLI mode.
    /// </summary>
    public class HeadlessUIService : IUIService
    {
        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image)
        {
            Console.Error.WriteLine("[WARNING] Image editor not available in CLI mode.");
            Console.Error.WriteLine("Image dimensions: {0}x{1}", image.Width, image.Height);
            return Task.FromResult<SKBitmap?>(image);
        }

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload)
        {
            // Return the tasks as-is without modification (no UI to change them)
            Console.WriteLine("[INFO] After-capture window not available in CLI mode. Using configured defaults.");
            return Task.FromResult((afterCapture, afterUpload, false));
        }
    }
}
