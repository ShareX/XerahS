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
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class HashCheckToolService
{
    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        return job switch
        {
            WorkflowType.HashCheck => OpenHashCheckAsync(owner, null),
            _ => Task.CompletedTask
        };
    }

    private static Task OpenHashCheckAsync(Window? owner, string? filePath)
    {
        var viewModel = new HashCheckViewModel(filePath);

        viewModel.BrowseFileRequested = async () =>
        {
            return await BrowseForFileAsync(owner);
        };

        var window = new HashCheckWindow
        {
            DataContext = viewModel
        };

        if (owner != null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        return Task.CompletedTask;
    }

    private static async Task<string?> BrowseForFileAsync(Window? owner)
    {
        var topLevel = owner != null ? TopLevel.GetTopLevel(owner) : null;
        if (topLevel == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = "Select File",
            AllowMultiple = false
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count < 1) return null;

        return files[0].TryGetLocalPath();
    }
}
