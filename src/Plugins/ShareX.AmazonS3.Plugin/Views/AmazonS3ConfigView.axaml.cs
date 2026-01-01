using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.AmazonS3.Plugin.Views;

public partial class AmazonS3ConfigView : UserControl
{
    public AmazonS3ConfigView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
