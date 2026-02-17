#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Breadcrumb entry: label shown in the bar, full path navigated to on click.
/// </summary>
public record BreadcrumbPart(string Label, string Path);

/// <summary>
/// Converts SortDescending (bool) → sort direction button label.
/// </summary>
public static class ProviderExplorerConverters
{
    public static Avalonia.Data.Converters.IValueConverter SortDirectionConverter { get; } =
        new Avalonia.Data.Converters.FuncValueConverter<bool, string>(desc => desc ? "↓ Desc" : "↑ Asc");
}

/// <summary>
/// ViewModel for the Media Explorer window.
/// Sourced from a specific <see cref="UploaderInstance"/> and its <see cref="IUploaderExplorer"/>.
/// Follows the HistoryViewModel patterns: pagination, grid/list toggle, async thumbnail loading.
/// </summary>
public partial class ProviderExplorerViewModel : ViewModelBase, IDisposable
{
    private readonly UploaderInstance _instance;
    private readonly IUploaderExplorer _explorer;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _thumbnailCts;

    // Navigation history (list of paths + current index)
    private readonly List<string> _navHistory = new();
    private int _navIndex = -1;

    [ObservableProperty] private ObservableCollection<MediaItemViewModel> _items = new();
    [ObservableProperty] private bool _isGridView = true;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingThumbnails;
    [ObservableProperty] private string _currentPath = "";
    [ObservableProperty] private string _windowTitle = "";
    [ObservableProperty] private ObservableCollection<BreadcrumbPart> _breadcrumbs = new();
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedFileTypeFilter = "All";
    [ObservableProperty] private ExplorerSortField _sortBy = ExplorerSortField.Date;
    [ObservableProperty] private bool _sortDescending = true;
    [ObservableProperty] private string? _continuationToken;
    [ObservableProperty] private bool _hasMoreItems;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorDetails = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanNavigateBack))]
    private int _navIndexProp; // mirrors _navIndex for binding

    public bool CanNavigateBack => _navIndex > 0;
    public bool CanNavigateForward => _navIndex < _navHistory.Count - 1;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorDetails);

    public UploaderInstance BoundInstance => _instance;

    public ProviderExplorerViewModel(UploaderInstance instance, IUploaderExplorer explorer)
    {
        _instance = instance;
        _explorer = explorer;

        var providerName = ProviderCatalog.GetProvider(instance.ProviderId)?.Name ?? instance.ProviderId;
        WindowTitle = $"{providerName} — {instance.DisplayName}";

        // Load root on creation
        _ = NavigateToFolderInternalAsync("", pushHistory: false);
    }

    // ─── Navigation ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NavigateToFolder(string path)
    {
        await NavigateToFolderInternalAsync(path, pushHistory: true);
    }

    [RelayCommand]
    private async Task NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        string trimmed = CurrentPath.TrimEnd('/');
        int lastSlash = trimmed.LastIndexOf('/');
        string parent = lastSlash > 0 ? trimmed[..lastSlash] + "/" : "";
        await NavigateToFolderInternalAsync(parent, pushHistory: true);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private async Task NavigateBack()
    {
        if (!CanNavigateBack) return;
        _navIndex--;
        NotifyNavChanged();
        await LoadItemsForCurrentPathAsync(resetPaging: true);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private async Task NavigateForward()
    {
        if (!CanNavigateForward) return;
        _navIndex++;
        NotifyNavChanged();
        await LoadItemsForCurrentPathAsync(resetPaging: true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadItemsForCurrentPathAsync(resetPaging: true);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadItemsForCurrentPathAsync(resetPaging: true);
    }

    [RelayCommand]
    private void ToggleView()
    {
        IsGridView = !IsGridView;
    }

    // ─── Item actions ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenItem(MediaItemViewModel? item)
    {
        if (item == null) return;
        if (item.Item.IsFolder)
        {
            _ = NavigateToFolderInternalAsync(item.Item.Path, pushHistory: true);
            return;
        }

        if (!string.IsNullOrEmpty(item.Item.Url))
        {
            XerahS.Platform.Abstractions.PlatformServices.System.OpenUrl(item.Item.Url);
        }
    }

    [RelayCommand]
    private async Task CopyUrl(MediaItemViewModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.Item.Url)) return;
        try
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow != null)
            {
                var clipboard = desktop.MainWindow.Clipboard;
                if (clipboard != null)
                    await clipboard.SetTextAsync(item.Item.Url);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ProviderExplorerViewModel - CopyUrl failed");
        }
    }

    [RelayCommand]
    private async Task DownloadItem(MediaItemViewModel? item)
    {
        if (item == null || item.Item.IsFolder) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var stream = await _explorer.GetContentAsync(item.Item, cts.Token);
            if (stream == null) return;

            string downloadsDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            System.IO.Directory.CreateDirectory(downloadsDir);
            string destPath = System.IO.Path.Combine(downloadsDir, item.Item.Name);

            using var fs = System.IO.File.Create(destPath);
            await stream.CopyToAsync(fs, cts.Token);

            StatusText = $"Downloaded: {item.Item.Name}";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ProviderExplorerViewModel - DownloadItem failed");
            StatusText = $"Download failed: {ex.Message}";
            SetError("DownloadItem", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteItem(MediaItemViewModel? item)
    {
        if (item == null) return;

        bool confirmed = await ShowConfirmDeleteDialogAsync(item.Item.Name);
        if (!confirmed) return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            bool success = await _explorer.DeleteAsync(item.Item, cts.Token);
            if (success)
            {
                Items.Remove(item);
                UpdateStatusText();
                DebugHelper.WriteLine($"[Explorer] Deleted: {item.Item.Name}");
            }
            else
            {
                StatusText = $"Failed to delete: {item.Item.Name}";
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ProviderExplorerViewModel - DeleteItem failed");
            StatusText = $"Delete failed: {ex.Message}";
            SetError("DeleteItem", ex);
        }
    }

    // ─── Pagination ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMoreItems || string.IsNullOrEmpty(ContinuationToken)) return;
        await LoadPageAsync(append: true);
    }

    // ─── Internal loading ─────────────────────────────────────────────────────

    private async Task NavigateToFolderInternalAsync(string path, bool pushHistory)
    {
        if (pushHistory)
        {
            // Discard any forward history
            if (_navIndex < _navHistory.Count - 1)
                _navHistory.RemoveRange(_navIndex + 1, _navHistory.Count - _navIndex - 1);
            _navHistory.Add(path);
            _navIndex = _navHistory.Count - 1;
            NotifyNavChanged();
        }
        else if (_navHistory.Count == 0)
        {
            _navHistory.Add(path);
            _navIndex = 0;
            NotifyNavChanged();
        }

        CurrentPath = path;
        UpdateBreadcrumbs(path);
        await LoadItemsForCurrentPathAsync(resetPaging: true);
    }

    private async Task LoadItemsForCurrentPathAsync(bool resetPaging)
    {
        if (resetPaging) ContinuationToken = null;
        CurrentPath = _navIndex >= 0 && _navIndex < _navHistory.Count
            ? _navHistory[_navIndex]
            : CurrentPath;
        UpdateBreadcrumbs(CurrentPath);
        await LoadPageAsync(append: false);
    }

    private async Task LoadPageAsync(bool append)
    {
        // Cancel any in-flight load
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        ClearError();
        try
        {
            var query = BuildQuery();
            var page = await _explorer.ListAsync(query, ct);

            if (ct.IsCancellationRequested) return;

            if (!append)
                Items.Clear();

            foreach (var item in page.Items)
                Items.Add(new MediaItemViewModel(item));

            ContinuationToken = page.ContinuationToken;
            HasMoreItems = !string.IsNullOrEmpty(page.ContinuationToken);
            UpdateStatusText();

            if (Items.Count > 0)
                _ = LoadThumbnailsInBackgroundAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ProviderExplorerViewModel - LoadPageAsync failed");
            StatusText = $"Error: {ex.Message}";
            SetError("LoadPageAsync", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadThumbnailsInBackgroundAsync()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts = new CancellationTokenSource();
        var ct = _thumbnailCts.Token;

        IsLoadingThumbnails = true;
        try
        {
            // Snapshot the current items list
            var snapshot = Items.ToList();
            foreach (var itemVm in snapshot)
            {
                if (ct.IsCancellationRequested) break;
                if (itemVm.Item.IsFolder) continue;

                // Skip very large files for thumbnails
                if (itemVm.Item.SizeBytes > 10 * 1024 * 1024) continue;

                try
                {
                    byte[]? thumbBytes = await _explorer.GetThumbnailAsync(itemVm.Item, 180, ct);
                    if (thumbBytes != null && thumbBytes.Length > 0)
                    {
                        using var stream = new MemoryStream(thumbBytes);
                        itemVm.Thumbnail = Bitmap.DecodeToWidth(stream, 180);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Silently skip thumbnails that fail
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ProviderExplorerViewModel - thumbnail loading failed");
        }
        finally
        {
            IsLoadingThumbnails = false;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private ExplorerQuery BuildQuery()
    {
        return new ExplorerQuery
        {
            SettingsJson = _instance.SettingsJson,
            FolderPath = CurrentPath,
            SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            FileTypeFilter = SelectedFileTypeFilter == "All" ? null : MapFileTypeFilter(SelectedFileTypeFilter),
            SortBy = SortBy,
            SortDescending = SortDescending,
            PageSize = 50,
            ContinuationToken = ContinuationToken
        };
    }

    private static string? MapFileTypeFilter(string filter) => filter switch
    {
        "Images" => "image/*",
        "Videos" => "video/*",
        "Text" => "text/*",
        _ => null
    };

    private void UpdateBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbPart("Root", ""));

        if (string.IsNullOrEmpty(path)) return;

        string[] parts = path.TrimEnd('/').Split('/');
        string accumulated = "";
        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            accumulated += part + "/";
            Breadcrumbs.Add(new BreadcrumbPart(part, accumulated));
        }
    }

    private void UpdateStatusText()
    {
        long totalBytes = Items.Sum(i => i.Item.SizeBytes);
        string sizeStr = totalBytes > 0 ? $" | {FormatBytes(totalBytes)} total" : "";
        StatusText = $"{Items.Count} items{sizeStr}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double value = bytes;
        while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
        return $"{value:0.##} {units[i]}";
    }

    private void NotifyNavChanged()
    {
        NavIndexProp = _navIndex; // triggers [NotifyPropertyChangedFor(CanNavigateBack/Forward)]
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
        NavigateBackCommand.NotifyCanExecuteChanged();
        NavigateForwardCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> ShowConfirmDeleteDialogAsync(string itemName)
    {
        var result = false;
        var dialog = new Avalonia.Controls.Window
        {
            Title = "Confirm Delete",
            Width = 420,
            Height = 190,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new Avalonia.Controls.StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 14,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        panel.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = $"Delete '{itemName}' from the provider?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 380,
            FontSize = 14
        });
        panel.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "This will permanently remove the file from the remote storage. This cannot be undone.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 380,
            FontSize = 12,
            Foreground = Avalonia.Media.Brushes.Orange,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });

        var buttonRow = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var cancelBtn = new Avalonia.Controls.Button { Content = "Cancel", Padding = new Avalonia.Thickness(20, 8), IsDefault = true };
        var deleteBtn = new Avalonia.Controls.Button
        {
            Content = "Delete",
            Padding = new Avalonia.Thickness(20, 8),
            Background = Avalonia.Media.Brushes.Red,
            Foreground = Avalonia.Media.Brushes.White
        };

        cancelBtn.Click += (_, _) => { result = false; dialog.Close(); };
        deleteBtn.Click += (_, _) => { result = true; dialog.Close(); };

        buttonRow.Children.Add(cancelBtn);
        buttonRow.Children.Add(deleteBtn);
        panel.Children.Add(buttonRow);
        dialog.Content = panel;

        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
        else
        {
            dialog.Show();
            var tcs = new TaskCompletionSource<bool>();
            dialog.Closed += (_, _) => tcs.TrySetResult(true);
            await tcs.Task;
        }

        return result;
    }

    private void SetError(string operation, Exception ex)
    {
        ErrorDetails = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {operation} failed:{Environment.NewLine}{ex}";
    }

    private void ClearError()
    {
        ErrorDetails = string.Empty;
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
    }
}
