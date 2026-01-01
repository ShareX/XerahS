using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Ava.UI.Controls;

public partial class FileTypeSelectorControl : UserControl
{
    public FileTypeSelectorControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
