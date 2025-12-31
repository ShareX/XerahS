using Avalonia;
using Avalonia.Controls;
using ShareX.Avalonia.UI.ViewModels;

namespace ShareX.Avalonia.UI.Views;

public partial class ProviderCatalogDialog : UserControl
{
    public ProviderCatalogDialog()
    {
        InitializeComponent();
    }

    public ProviderCatalogDialog(ProviderCatalogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
