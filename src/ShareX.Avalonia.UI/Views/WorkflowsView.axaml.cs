using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class WorkflowsView : UserControl
{
    public WorkflowsView()
    {
        InitializeComponent();
        var vm = new WorkflowsViewModel();
        DataContext = vm;

        // Wire up the edit requester (same pattern as ApplicationSettingsView)
        vm.EditHotkeyRequester = async (settings) =>
        {
            var editVm = new WorkflowEditorViewModel(settings);
            var dialog = new WorkflowEditorView
            {
                DataContext = editVm
            };

            if (VisualRoot is Window window)
            {
                return await dialog.ShowDialog<bool>(window);
            }

            return false;
        };

        // Wire up confirmation dialog
        vm.ConfirmByUi = async (title, message) =>
        {
            var dialog = new FluentAvalonia.UI.Controls.ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                DefaultButton = FluentAvalonia.UI.Controls.ContentDialogButton.Close
            };

            if (VisualRoot is Window window)
            {
                var result = await dialog.ShowAsync();
                return result == FluentAvalonia.UI.Controls.ContentDialogResult.Primary;
            }

            return false;
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
