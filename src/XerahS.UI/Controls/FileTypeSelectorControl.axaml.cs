using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XerahS.UI.Controls;

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
