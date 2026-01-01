using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.Ava.UI.ViewModels;

namespace ShareX.Ava.UI.Views;

public partial class ProviderCatalogView : UserControl
{
    public ProviderCatalogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
