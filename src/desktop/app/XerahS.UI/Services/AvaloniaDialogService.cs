using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.UI.Views.Dialogs;
using ShareX.ImageEditor.Presentation.ViewModels;

namespace XerahS.UI.Services
{
    public class AvaloniaDialogService : IViewDialogService
    {
        public Task ShowDialogAsync<TWindow>(object dataContext) where TWindow : class, new()
        {
            throw new NotSupportedException(
                $"Window.ShowDialog is retired. Host '{typeof(TWindow).Name}' via ModalDialogHost / ModalContent.");
        }

        public Task<TResult?> ShowDialogAsync<TWindow, TResult>(object dataContext) where TWindow : class, new()
        {
            throw new NotSupportedException(
                $"Window.ShowDialog is retired. Host '{typeof(TWindow).Name}' via ModalDialogHost / ModalContent.");
        }

        public Task<bool> ShowPluginInstallerAsync(PluginInstallerViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.RequestClose = result => set(result ?? false),
                dismissResult: false,
                debugSource: nameof(PluginInstallerViewModel));
        }

        public Task<bool> ShowCustomUploaderEditorAsync(CustomUploaderEditorViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = set,
                dismissResult: false,
                debugSource: nameof(CustomUploaderEditorViewModel));
        }

        public Task<bool> ShowWorkflowEditorAsync(WorkflowEditorViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = set,
                dismissResult: false,
                debugSource: nameof(WorkflowEditorViewModel));
        }

        public Task ShowImageEffectsBrowserAsync(ImageEffectsViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = _ => set(true),
                dismissResult: true,
                debugSource: nameof(ImageEffectsViewModel));
        }

        public Task ShowFFmpegOptionsAsync(FFmpegOptionsViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = _ => set(true),
                dismissResult: true,
                debugSource: nameof(FFmpegOptionsViewModel));
        }

        public Task ShowMediaBrowserAsync(MediaBrowserViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = _ => set(true),
                dismissResult: true,
                debugSource: nameof(MediaBrowserViewModel));
        }

        public Task ShowShareLogsAsync(ShareLogsViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = _ => set(true),
                dismissResult: true,
                debugSource: nameof(ShareLogsViewModel));
        }

        public Task ShowQrCodeGeneratorAsync(QrCodeGeneratorViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = _ => set(true),
                dismissResult: true,
                debugSource: nameof(QrCodeGeneratorViewModel));
        }

        public Task<bool> ShowWatchFolderEditorAsync(WatchFolderEditViewModel viewModel)
        {
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = set,
                dismissResult: false,
                debugSource: nameof(WatchFolderEditViewModel));
        }

        public Task<OpenImageChoice> ShowOpenImageChoiceAsync()
        {
            var viewModel = new OpenImageChoiceViewModel();
            return ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = set,
                dismissResult: OpenImageChoice.Cancel,
                debugSource: nameof(OpenImageChoiceViewModel));
        }

        public async Task<string?> ShowFilePickerAsync(string title, IEnumerable<string>? filters = null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                };
                if (filters != null)
                {
                    options.FileTypeFilter = new[] { new FilePickerFileType("Files") { Patterns = filters.ToList() } };
                }
                var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(options);
                return files.FirstOrDefault()?.TryGetLocalPath();
            }
            return null;
        }

        public async Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string defaultExtension, IEnumerable<string>? filters = null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var options = new FilePickerSaveOptions
                {
                    Title = title,
                    SuggestedFileName = suggestedFileName,
                    DefaultExtension = defaultExtension
                };
                if (filters != null)
                {
                    options.FileTypeChoices = new[] { new FilePickerFileType("Files") { Patterns = filters.ToList() } };
                }
                var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(options);
                return file?.TryGetLocalPath();
            }
            return null;
        }

        public async Task<string?> ShowSecretInputAsync(string title, string label)
        {
            var viewModel = new SimplePromptViewModel
            {
                Title = title,
                Label = label,
                ShowCancel = true,
                ShowInput = true,
                IsPassword = true,
                PrimaryButtonText = "OK"
            };

            var ok = await ModalDialogHost.ShowAsync(
                viewModel,
                set => viewModel.CloseRequested = set,
                dismissResult: false,
                debugSource: "SecretInput");

            return ok ? viewModel.AcceptedInput : null;
        }

        public async Task<string?> ShowFolderPickerAsync(string title)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
                var folders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(options);
                return folders.FirstOrDefault()?.TryGetLocalPath();
            }
            return null;
        }

        public object? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        public IEnumerable<object> GetOpenWindows()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows;
            }
            return Enumerable.Empty<object>();
        }
    }
}
