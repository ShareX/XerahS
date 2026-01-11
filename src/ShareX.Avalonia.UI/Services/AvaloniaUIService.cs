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

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using ShareX.Editor.ViewModels;
using SkiaSharp;

namespace XerahS.UI.Services
{
    public class AvaloniaUIService : IUIService
    {
        public async Task ShowEditorAsync(SKBitmap image)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Create independent Editor Window
                var editorWindow = new Views.EditorWindow();

                // Create independent ViewModel for this editor instance
                var editorViewModel = new MainViewModel();
                editorViewModel.ShowCaptureToolbar = false;
                editorViewModel.ApplicationName = ShareXResources.AppName;

                // Set DataContext BEFORE initializing preview so bindings update correctly
                editorWindow.DataContext = editorViewModel;

                // Initialize the preview image
                editorViewModel.UpdatePreview(image);

                // Show the window
                editorWindow.Show();
            });
        }

        public async Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new AfterCaptureViewModel(image, afterCapture, afterUpload);
                var window = new Views.AfterCaptureWindow
                {
                    DataContext = viewModel
                };

                viewModel.RequestClose += () => window.Close();

                Window? owner = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                if (owner != null)
                {
                    await window.ShowDialog(owner);
                }
                else
                {
                    var closedTcs = new TaskCompletionSource<bool>();
                    window.Closed += (_, _) => closedTcs.TrySetResult(true);
                    window.Show();
                    await closedTcs.Task;
                }

                return (viewModel.AfterCaptureTasks, viewModel.AfterUploadTasks, viewModel.Cancelled);
            });
        }
    }
}
