using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XerahS.UI.Views;

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
