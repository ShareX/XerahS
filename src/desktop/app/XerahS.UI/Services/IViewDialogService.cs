using System.Collections.Generic;
using System.Threading.Tasks;
using XerahS.UI.ViewModels;
using XerahS.UI.Views.Dialogs;

namespace XerahS.UI.Services
{
    public interface IViewDialogService
    {
        Task ShowDialogAsync<TWindow>(object dataContext) where TWindow : class, new();
        Task<TResult?> ShowDialogAsync<TWindow, TResult>(object dataContext) where TWindow : class, new();
        Task<bool> ShowPluginInstallerAsync(PluginInstallerViewModel viewModel);
        Task<bool> ShowCustomUploaderEditorAsync(CustomUploaderEditorViewModel viewModel);
        Task<bool> ShowWorkflowEditorAsync(WorkflowEditorViewModel viewModel);
        Task ShowImageEffectsBrowserAsync(ImageEffectsViewModel viewModel);
        Task ShowFFmpegOptionsAsync(FFmpegOptionsViewModel viewModel);
        Task ShowMediaBrowserAsync(MediaBrowserViewModel viewModel);
        Task ShowQrCodeGeneratorAsync(QrCodeGeneratorViewModel viewModel);
        Task ShowShareLogsAsync(ShareLogsViewModel viewModel);
        Task<bool> ShowWatchFolderEditorAsync(WatchFolderEditViewModel viewModel);
        Task<OpenImageChoice> ShowOpenImageChoiceAsync();
        Task<string?> ShowFilePickerAsync(string title, IEnumerable<string>? filters = null);
        Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string defaultExtension, IEnumerable<string>? filters = null);
        Task<string?> ShowSecretInputAsync(string title, string label);
        Task<string?> ShowFolderPickerAsync(string title);
        object? GetMainWindow();
        IEnumerable<object> GetOpenWindows();
    }
}
