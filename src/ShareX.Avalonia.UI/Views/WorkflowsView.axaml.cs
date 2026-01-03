using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ShareX.Ava.UI.ViewModels;

namespace ShareX.Ava.UI.Views;

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
            var editVm = new HotkeyEditViewModel(settings);
            var dialog = new HotkeyEditView
            {
                DataContext = editVm
            };
            
            if (VisualRoot is Window window)
            {
               return await dialog.ShowDialog<bool>(window);
            }
            
            return false;
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
