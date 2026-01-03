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
using System.Drawing;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ShareX.Ava.Core;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.UI.ViewModels;

namespace ShareX.Ava.UI.Services
{
    public class AvaloniaUIService : IUIService
    {
        public async Task ShowEditorAsync(System.Drawing.Image image)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Create a clone of the image for independent editing
                var imageClone = (System.Drawing.Image)image.Clone();

                // Create independent Editor Window
                var editorWindow = new Views.EditorWindow();
                
                // Create independent ViewModel for this editor instance
                var editorViewModel = new MainViewModel();
                
                // Set DataContext BEFORE initializing preview so bindings update correctly
                editorWindow.DataContext = editorViewModel;
                
                // Initialize the preview image
                editorViewModel.UpdatePreview(imageClone);

                // Show the window
                editorWindow.Show();
            });
        }

        public async Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            System.Drawing.Image image,
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
