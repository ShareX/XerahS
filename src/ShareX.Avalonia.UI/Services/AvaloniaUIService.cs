using System;
using System.Drawing;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
    }
}
