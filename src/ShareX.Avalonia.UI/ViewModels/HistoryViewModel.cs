using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.History;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace XerahS.UI.ViewModels
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

        [ObservableProperty]
        private bool _isLoadingThumbnails = false;



        // Pagination Properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanGoNext))]
        [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _currentPage = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanGoNext))]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _totalPages = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _totalItems = 0;

        [ObservableProperty]
        private int _pageSize = 50;

        public bool CanGoNext => CurrentPage < TotalPages;
        public bool CanGoPrevious => CurrentPage > 1;

        public string PageInfo => $"Page {CurrentPage} of {Math.Max(1, TotalPages)} ({TotalItems} items)"; // Prevent "Page 1 of 0" looking weird

        private readonly HistoryManagerSQLite _historyManager;
        private CancellationTokenSource? _thumbnailCancellationTokenSource;

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

            // Start loading history asynchronously WITHOUT blocking UI
            // Use fire-and-forget to let view display immediately
            _ = BeginHistoryLoadAsync();
        }

        /// <summary>
        /// Starts history loading asynchronously without blocking the UI thread.
        /// This allows the empty panel to display immediately.
        /// </summary>
        private async Task BeginHistoryLoadAsync()
        {
            // Small delay to allow UI to render the empty history view first
            await Task.Delay(100);
            await LoadHistoryAsync();
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

                // calculating offset
                int offset = (CurrentPage - 1) * PageSize;

                // Load total count first
                TotalItems = await _historyManager.GetTotalCountAsync();
                TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
                if (TotalPages == 0) TotalPages = 1; // Ensure at least 1 page even if empty

                // Adjust CurrentPage if out of bounds (e.g. after deletion)
                if (CurrentPage > TotalPages) CurrentPage = TotalPages;
                if (CurrentPage < 1) CurrentPage = 1;

                // Load paged history on background thread
                var items = await _historyManager.GetHistoryItemsAsync(offset, PageSize);

                // Clear and populate on UI thread
                HistoryItems.Clear();
                foreach (var item in items)
                {
                    HistoryItems.Add(item);
                }

                DebugHelper.WriteLine($"History loaded: {items.Count} items (Page {CurrentPage}/{TotalPages})");

                // Start loading thumbnails in background after history is displayed
                if (HistoryItems.Count > 0)
                {
                    _ = LoadThumbnailsInBackgroundAsync();
                }
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
        private async Task NextPage()
        {
            if (CanGoNext)
            {
                CurrentPage++;
                await LoadHistoryAsync();
            }
        }

        [RelayCommand]
        private async Task PreviousPage()
        {
            if (CanGoPrevious)
            {
                CurrentPage--;
                await LoadHistoryAsync();
            }
        }

        /// <summary>
        /// Loads thumbnails asynchronously on a background thread.
        /// This allows history items to display immediately while thumbnails load gradually.
        /// </summary>
        private async Task LoadThumbnailsInBackgroundAsync()
        {
            // Cancel any previous thumbnail loading
            _thumbnailCancellationTokenSource?.Cancel();
            _thumbnailCancellationTokenSource = new CancellationTokenSource();

            IsLoadingThumbnails = true;
            try
            {
                await Task.Run(() =>
                {
                    int loadedCount = 0;
                    foreach (var item in HistoryItems)
                    {
                        // Check cancellation token
                        _thumbnailCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        // Pre-load thumbnail by accessing the converter
                        // This forces the thumbnail to be cached for faster display
                        if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
                        {
                            try
                            {
                                var ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp")
                                {
                                    using var stream = File.OpenRead(item.FilePath);
                                    _ = Bitmap.DecodeToWidth(stream, 180);
                                    loadedCount++;
                                }
                            }
                            catch
                            {
                                // Silently skip thumbnails that fail to load
                            }
                        }

                        // Add small delay to prevent CPU saturation
                        if (loadedCount % 5 == 0)
                        {
                            System.Threading.Thread.Sleep(50);
                        }
                    }

                    DebugHelper.WriteLine($"Thumbnails pre-loaded: {loadedCount} images");
                }, _thumbnailCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                DebugHelper.WriteLine("Thumbnail loading was cancelled");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Error while loading thumbnails");
            }
            finally
            {
                IsLoadingThumbnails = false;
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
            // Cancel any ongoing thumbnail loading
            _thumbnailCancellationTokenSource?.Cancel();
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
                await XerahS.Platform.Abstractions.PlatformServices.UI.ShowEditorAsync(skBitmap);
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
                XerahS.Platform.Abstractions.PlatformServices.System.OpenFile(item.FilePath);
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

            XerahS.Platform.Abstractions.PlatformServices.System.ShowFileInExplorer(item.FilePath);
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
            _thumbnailCancellationTokenSource?.Cancel();
            _thumbnailCancellationTokenSource?.Dispose();
            _historyManager?.Dispose();
        }
    }
}
