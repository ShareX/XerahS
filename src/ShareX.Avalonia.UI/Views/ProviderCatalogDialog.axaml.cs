using Avalonia.Controls;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

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
