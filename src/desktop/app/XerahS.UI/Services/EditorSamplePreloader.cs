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

using Avalonia.Threading;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using XerahS.Common;

namespace XerahS.UI.Services;

/// <summary>
/// Loads the editor's placeholder image (Sample.png) on first use instead of at startup,
/// so tray-only sessions never decode it (XIP0091). Never overwrites a real capture.
/// </summary>
internal static class EditorSamplePreloader
{
    private static int _started;

    public static void PreloadIfEmpty(MainViewModel? viewModel)
    {
        if (viewModel == null || viewModel.HasPreviewImage || Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        MainViewModel vm = viewModel;
        Task.Run(async () =>
        {
            SKBitmap? sampleBitmap = null;
            try
            {
                var sampleUri = new Uri("avares://ShareX.ImageEditor/Assets/Sample.png");
                using var sampleStream = Avalonia.Platform.AssetLoader.Open(sampleUri);
                using var ms = new MemoryStream();
                sampleStream.CopyTo(ms);
                ms.Position = 0;
                sampleBitmap = SKBitmap.Decode(ms);
                if (sampleBitmap == null)
                {
                    DebugHelper.WriteLine("SKBitmap.Decode returned null for Sample.png");
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // A capture may have arrived while decoding; keep it.
                    if (!vm.HasPreviewImage)
                    {
                        vm.UpdatePreview(sampleBitmap, clearAnnotations: true);
                        sampleBitmap = null;
                    }
                });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to pre-load default editor image (Sample.png): {ex}");
            }
            finally
            {
                sampleBitmap?.Dispose();
            }
        });
    }
}
