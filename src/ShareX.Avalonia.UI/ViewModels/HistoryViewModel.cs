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
using SkiaSharp;

namespace ShareX.Ava.UI.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase, IDisposable
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

        private readonly HistoryManagerSQLite _historyManager;

        public HistoryViewModel()
        {
            HistoryItems = new ObservableCollection<HistoryItem>();
            
            // Create history manager with centralized path
            var historyPath = SettingManager.GetHistoryFilePath();
            DebugHelper.WriteLine($"HistoryViewModel - History file path: {historyPath}");

            _historyManager = new HistoryManagerSQLite(historyPath);
            
            // Configure backup settings similar to JSON files
            _historyManager.BackupFolder = SettingManager.HistoryBackupFolder;
            _historyManager.CreateBackup = true;
            _historyManager.CreateWeeklyBackup = true;
            
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
                // Load the image from file directly as SKBitmap
                using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);
                var skBitmap = SKBitmap.Decode(fs);
                if (skBitmap == null) return;

                // Open in Editor using the platform service
                await ShareX.Ava.Platform.Abstractions.PlatformServices.UI.ShowEditorAsync(skBitmap);
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
        private async Task DeleteItem(HistoryItem? item)
        {
            if (item == null) return;

            // Show confirmation dialog
            var confirmDelete = await ShowDeleteConfirmationDialog(item.FileName);
            if (!confirmDelete) return;
            
            // Remove from the observable collection (UI update)
            HistoryItems.Remove(item);
            
            // Persist deletion to database
            _historyManager.Delete(item);
            DebugHelper.WriteLine($"Deleted history item: {item.FileName}");
        }

        private async Task<bool> ShowDeleteConfirmationDialog(string fileName)
        {
            var result = false;

            var confirmDialog = new Avalonia.Controls.Window
            {
                Title = "Confirm Delete",
                Width = 400,
                Height = 180,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var messageText = new Avalonia.Controls.TextBlock
            {
                Text = $"Are you sure you want to delete '{fileName}' from history?",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 360,
                FontSize = 14
            };

            var warningText = new Avalonia.Controls.TextBlock
            {
                Text = "This action cannot be undone.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 360,
                FontSize = 12,
                Foreground = Avalonia.Media.Brushes.Orange,
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            };

            var buttonPanel = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };

            var deleteButton = new Avalonia.Controls.Button
            {
                Content = "Delete",
                Padding = new Avalonia.Thickness(24, 8),
                Background = Avalonia.Media.Brushes.Red,
                Foreground = Avalonia.Media.Brushes.White
            };

            var cancelButton = new Avalonia.Controls.Button
            {
                Content = "Cancel",
                Padding = new Avalonia.Thickness(24, 8),
                IsDefault = true
            };

            deleteButton.Click += (s, e) =>
            {
                result = true;
                confirmDialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                result = false;
                confirmDialog.Close();
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(deleteButton);

            panel.Children.Add(messageText);
            panel.Children.Add(warningText);
            panel.Children.Add(buttonPanel);

            confirmDialog.Content = panel;

            // Get the main window as the owner
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow != null)
            {
                await confirmDialog.ShowDialog(desktop.MainWindow);
            }
            else
            {
                await confirmDialog.ShowDialog((Avalonia.Controls.Window?)null);
            }

            return result;
        }

        public void Dispose()
        {
            _historyManager?.Dispose();
        }
    }
}
