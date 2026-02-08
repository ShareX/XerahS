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

using Avalonia;
using Avalonia.Threading;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class PinToScreenManager
{
    private static readonly List<PinnedImageWindow> _windows = new();
    private static readonly object _lock = new();

    public static int Count
    {
        get { lock (_lock) { return _windows.Count; } }
    }

    public static void PinImage(SKBitmap bitmap, PixelPoint? location, PinToScreenOptions options)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => PinImage(bitmap, location, options));
            return;
        }

        try
        {
            var viewModel = new PinnedImageViewModel(bitmap, options);
            var window = new PinnedImageWindow();
            window.Initialize(viewModel, location, options);

            window.Closed += OnWindowClosed;

            lock (_lock)
            {
                _windows.Add(window);
            }

            window.Show();

            DebugHelper.WriteLine($"Pinned image: {bitmap.Width}x{bitmap.Height}, total pinned={Count}");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PinToScreen pin image");
        }
    }

    public static void CloseAll()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(CloseAll);
            return;
        }

        List<PinnedImageWindow> toClose;
        lock (_lock)
        {
            toClose = new List<PinnedImageWindow>(_windows);
            _windows.Clear();
        }

        foreach (var window in toClose)
        {
            try
            {
                window.Closed -= OnWindowClosed;
                window.Close();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "PinToScreen close window");
            }
        }

        DebugHelper.WriteLine($"Closed all pinned windows ({toClose.Count})");
    }

    private static void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is PinnedImageWindow window)
        {
            window.Closed -= OnWindowClosed;

            lock (_lock)
            {
                _windows.Remove(window);
            }
        }
    }
}
