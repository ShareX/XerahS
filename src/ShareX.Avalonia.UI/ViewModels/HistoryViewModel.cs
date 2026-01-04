using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Core;
using ShareX.Ava.Common;
using ShareX.Ava.Core.Tasks;
using ShareX.Ava.History;

namespace ShareX.Ava.UI.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase
    {
        // Converter for view toggle button text
        public static IValueConverter ViewToggleConverter { get; } = new FuncValueConverter<bool, string>(
            isGrid => isGrid ? "📋 List View" : "🔲 Grid View");

        // Converter to load thumbnail from file path (resource-efficient)
        public static IValueConverter ThumbnailConverter { get; } = new FuncValueConverter<string?, Bitmap?>(
            filePath =>
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;
                
                try
                {
                    // Check if it's an image file
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif" && ext != ".bmp" && ext != ".webp")
                        return null;
                    
                    // Load with decode size for memory efficiency (thumbnail size)
                    using var stream = File.OpenRead(filePath);
                    return Bitmap.DecodeToWidth(stream, 180); // Decode to thumbnail width
                }
                catch
                {
                    return null;
                }
            });

        [ObservableProperty]
        private ObservableCollection<HistoryItem> _historyItems;

        [ObservableProperty]
        private bool _isGridView = true;

        [ObservableProperty]
        private bool _isLoading = false;

        private readonly HistoryManager _historyManager;

        public HistoryViewModel()
        {
            HistoryItems = new ObservableCollection<HistoryItem>();
            
            // Create history manager with centralized path
            var historyPath = SettingManager.GetHistoryFilePath();
            DebugHelper.WriteLine($"HistoryViewModel - History file path: {historyPath}");

            _historyManager = new HistoryManagerXML(historyPath);
            
            // Don't load history in constructor - do it asynchronously after view is displayed
            LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            try
            {
                var historyPath = SettingManager.GetHistoryFilePath();
                DebugHelper.WriteLine($"History.xml location: {historyPath} (exists={File.Exists(historyPath)})");
                
                // Load history on background thread to avoid blocking UI
                var items = await _historyManager.GetHistoryItemsAsync();
                
                HistoryItems.Clear();
                foreach (var item in items)
                {
                    HistoryItems.Add(item);
                }
                
                DebugHelper.WriteLine($"History loaded: {HistoryItems.Count} items");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load history");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ToggleView()
        {
            IsGridView = !IsGridView;
        }

        [RelayCommand]
        private async Task RefreshHistory()
        {
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task EditImage(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!File.Exists(item.FilePath)) return;

            try
            {
                // Load the image from file
                using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);
                var image = System.Drawing.Image.FromStream(fs);
                
                // Open in Editor using the platform service
                await ShareX.Ava.Platform.Abstractions.PlatformServices.UI.ShowEditorAsync(image);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open image in editor: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenFile(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!File.Exists(item.FilePath)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open file: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenFolder(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            
            FileHelpers.OpenFolderWithFile(item.FilePath);
        }

        [RelayCommand]
        private async Task CopyFilePath(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;

            try
            {
                // Get clipboard from the main window
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    && desktop.MainWindow != null)
                {
                    var clipboard = desktop.MainWindow.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(item.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy file path: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CopyURL(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.URL)) return;

            try
            {
                // Get clipboard from the main window
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    && desktop.MainWindow != null)
                {
                    var clipboard = desktop.MainWindow.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(item.URL);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy URL: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DeleteItem(HistoryItem? item)
        {
            if (item == null) return;
            
            // Remove from the observable collection (UI update)
            HistoryItems.Remove(item);
            
            // TODO: Persist deletion to history file
            DebugHelper.WriteLine($"Deleted history item: {item.FileName}");
        }
    }
}
