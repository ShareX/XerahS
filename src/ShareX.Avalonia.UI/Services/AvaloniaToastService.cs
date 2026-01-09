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
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

/// <summary>
/// Avalonia implementation of IToastService that displays custom toast windows.
/// This service is cross-platform and works on Windows, Linux, and macOS.
/// </summary>
public class AvaloniaToastService : IToastService
{
    private static ToastWindow? _activeToast;
    private static readonly object _lock = new();

    /// <summary>
    /// Shows a toast notification with the specified configuration.
    /// If a toast is already visible, it will be closed before showing the new one.
    /// </summary>
    /// <param name="config">Toast configuration</param>
    public void ShowToast(ToastConfig config)
    {
        if (!config.IsValid)
        {
            DebugHelper.WriteLine("ToastConfig is not valid, skipping toast display.");
            return;
        }

        // Ensure we're on UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowToast(config));
            return;
        }

        try
        {
            lock (_lock)
            {
                // Close any existing toast
                CloseActiveToastInternal();

                // Create and show new toast
                var toast = new ToastWindow();
                toast.Initialize(config);
                toast.Closed += OnToastClosed;

                _activeToast = toast;

                // Show without activating (don't steal focus)
                toast.Show();

                DebugHelper.WriteLine($"Toast displayed: {config.Title ?? config.Text ?? "Image"}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to show toast notification");
        }
    }

    /// <summary>
    /// Closes any currently visible toast notification.
    /// </summary>
    public void CloseActiveToast()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(CloseActiveToast);
            return;
        }

        lock (_lock)
        {
            CloseActiveToastInternal();
        }
    }

    private void CloseActiveToastInternal()
    {
        if (_activeToast != null)
        {
            try
            {
                _activeToast.Closed -= OnToastClosed;
                _activeToast.Close();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to close active toast");
            }
            finally
            {
                _activeToast = null;
            }
        }
    }

    private void OnToastClosed(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (sender == _activeToast)
            {
                _activeToast = null;
            }
        }
    }
}
