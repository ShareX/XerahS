using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Ava.UI.Views;

public partial class ThemeShowcaseView : UserControl
{
    public ThemeShowcaseView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
