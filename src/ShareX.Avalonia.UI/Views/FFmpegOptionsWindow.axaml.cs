using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;

namespace XerahS.UI.Views;

public partial class FFmpegOptionsWindow : Window
{
    public FFmpegOptionsWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private async void BrowseFFmpeg_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select FFmpeg executable",
            AllowMultiple = false,
            Filters =
            {
                new FileDialogFilter { Name = "FFmpeg", Extensions = { "exe" } },
                new FileDialogFilter { Name = "All files", Extensions = { "*" } }
            }
        };

        Window? owner = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            owner = desktop.MainWindow;
        }

        var result = await dialog.ShowAsync(owner ?? this);
        var path = result != null && result.Length > 0 ? result[0] : null;
        if (!string.IsNullOrWhiteSpace(path) && DataContext is ViewModels.FFmpegOptionsViewModel vm)
        {
            vm.OverrideCLIPath = true;
            vm.CLIPath = path;
        }
    }
}
