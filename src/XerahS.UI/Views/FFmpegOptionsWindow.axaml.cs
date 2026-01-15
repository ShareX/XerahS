using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

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
        var topLevel = TopLevel.GetTopLevel(this);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Select FFmpeg executable",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("FFmpeg")
                {
                    Patterns = new[] { "ffmpeg", "ffmpeg.exe", "*.exe" }
                },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        };

        var results = await storageProvider.OpenFilePickerAsync(options);
        var path = results?.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path) && DataContext is ViewModels.FFmpegOptionsViewModel vm)
        {
            vm.OverrideCLIPath = true;
            vm.CLIPath = path;
        }
    }
}
