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
using Avalonia.Threading;
using System.Linq;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.SendTo;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class UploadContentToolService
{
    private static UploadContentWindow? _window;

    /// <param name="background">
    /// When <c>true</c>, the window is shown without stealing focus (used by the
    /// clipboard-monitor auto-open path so menus and popups are not disrupted).
    /// </param>
    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner, bool background = false)
    {
        var window = ShowWindow(owner, background);

        if (job is WorkflowType.ClipboardUploadWithContentViewer or WorkflowType.ClipboardViewer)
        {
            window.ViewModel?.LoadFromClipboardCommand.Execute(null);
        }

        return Task.CompletedTask;
    }

    public static async Task ShowSelectionAsync(
        IEnumerable<string>? filePaths,
        IEnumerable<string>? folderPaths,
        Window? owner,
        SendToFolderPolicy folderPolicy = SendToFolderPolicy.IncludeTopLevelFiles)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = ShowWindow(owner, background: false);
            var viewModel = window.ViewModel;
            if (viewModel == null)
            {
                return;
            }

            UploadQueueItem? firstAddedItem = null;
            int addedFileCount = 0;
            int addedFolderCount = 0;

            SendToSelection selection = SendToSelectionClassifier.Create(
                filePaths ?? Enumerable.Empty<string>(),
                folderPaths ?? Enumerable.Empty<string>());
            SendToResolvedFiles resolvedFiles = SendToPolicyResolver.ResolveFiles(selection, folderPolicy);

            foreach (var filePath in resolvedFiles.FilePaths)
            {
                var addedItem = viewModel.AddFileItem(filePath);
                if (addedItem != null)
                {
                    firstAddedItem ??= addedItem;
                    addedFileCount++;
                }
            }

            addedFolderCount = selection.FolderPaths.Count;

            if (firstAddedItem != null)
            {
                viewModel.SelectedItem = firstAddedItem;
            }

            DebugHelper.WriteLine(
                $"UploadContent: Seeded Send-to selection with {addedFileCount} resolved file(s), " +
                $"{addedFolderCount} folder item(s), folderPolicy={folderPolicy}, " +
                $"folderDerivedFiles={resolvedFiles.FolderFileCount}, failedFolders={resolvedFiles.FailedFolderCount}.");
        });
    }

    private static UploadContentWindow ShowWindow(Window? owner, bool background)
    {
        if (_window != null)
        {
            try
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Normal;
                }

                _window.ShowActivated = !background;
                _window.Show();

                if (!background)
                {
                    _window.Activate();
                }

                return _window;
            }
            catch
            {
                _window = null;
            }
        }

        var viewModel = UiViewModelFactoryAccessor.GetRequired().CreateUploadContentViewModel();
        _window = new UploadContentWindow { ShowActivated = !background };
        _window.Initialize(viewModel);

        _window.Closed += (_, _) =>
        {
            _window = null;
        };

        // A tray-hidden main window is not a valid Avalonia owner (issue #296).
        if (owner is { IsVisible: true })
        {
            _window.Show(owner);
        }
        else
        {
            _window.Show();
        }

        if (!background)
        {
            _window.Activate();
        }

        DebugHelper.WriteLine($"UploadContent: Window shown (background={background}).");
        return _window;
    }
}
