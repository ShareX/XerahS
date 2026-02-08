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

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using XerahS.Core;
using XerahS.Services.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class MonitorTestToolService
{
    private static MonitorTestWindow? _window;

    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        if (job == WorkflowType.MonitorTest)
        {
            ShowMonitorTestWindow(owner);
        }

        return Task.CompletedTask;
    }

    private static void ShowMonitorTestWindow(Window? owner)
    {
        if (_window != null)
        {
            try
            {
                _window.Show();
                _window.Activate();
                return;
            }
            catch
            {
                _window = null;
            }
        }

        var viewModel = new MonitorTestViewModel();

        // Setup clipboard callback
        viewModel.CopyToClipboardRequested = (text) =>
        {
            try
            {
                var clipboard = _window?.Clipboard;
                if (clipboard != null)
                {
                    _ = clipboard.SetTextAsync(text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
            }
        };

        // Setup save file callback
        viewModel.SaveFileRequested = async (fileName, filter) =>
        {
            if (_window == null) return null;

            try
            {
                var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Diagnostics",
                    SuggestedFileName = fileName,
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
                    }
                });

                return file?.Path.LocalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show save dialog: {ex.Message}");
                return null;
            }
        };

        _window = new MonitorTestWindow
        {
            DataContext = viewModel
        };

        _window.Closed += (_, _) => _window = null;

        if (owner != null)
        {
            _window.Show(owner);
        }
        else
        {
            _window.Show();
        }
    }
}
