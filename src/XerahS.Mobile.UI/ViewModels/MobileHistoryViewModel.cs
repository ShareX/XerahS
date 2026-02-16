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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using XerahS.Common;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;

namespace XerahS.Mobile.UI.ViewModels;

public class MobileHistoryViewModel : INotifyPropertyChanged
{
    private const int MaxHistoryItems = 200;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Action? OnCloseRequested { get; set; }

    private readonly List<MobileHistoryItem> _allItems = [];

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value ?? string.Empty;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    private string _statusText = "Loading upload history...";
    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<MobileHistoryItem> HistoryItems { get; } = [];

    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand DeleteItemCommand { get; }

    public MobileHistoryViewModel()
    {
        BackCommand = new RelayCommand(_ => OnCloseRequested?.Invoke());
        RefreshCommand = new RelayCommand(_ => Refresh());
        ClearHistoryCommand = new RelayCommand(_ => ClearHistory());
        CopyUrlCommand = new RelayCommand<string>(CopyUrl);
        DeleteItemCommand = new RelayCommand<MobileHistoryItem>(DeleteItem);

        Refresh();
    }

    private void Refresh()
    {
        _allItems.Clear();

        foreach (var entry in UploadHistoryService.GetRecentEntries(MaxHistoryItems))
        {
            _allItems.Add(new MobileHistoryItem
            {
                Id = entry.Id,
                FileName = string.IsNullOrWhiteSpace(entry.FileName) ? "(Unnamed file)" : entry.FileName,
                Url = entry.Url,
                Host = entry.Host,
                DateTime = entry.DateTime,
                CopyUrlCommand = CopyUrlCommand,
                DeleteItemCommand = DeleteItemCommand
            });
        }

        ApplyFilter();
    }

    private void ClearHistory()
    {
        var deletedCount = UploadHistoryService.ClearEntries();
        if (deletedCount <= 0 && _allItems.Count == 0)
        {
            StatusText = "History is already empty.";
            return;
        }

        Refresh();
        StatusText = deletedCount > 0
            ? $"Cleared {deletedCount} upload history item(s)."
            : "No history items were removed.";
    }

    private void DeleteItem(MobileHistoryItem? item)
    {
        if (item == null)
        {
            return;
        }

        if (!UploadHistoryService.DeleteEntry(item.Id))
        {
            StatusText = "Could not delete this history item.";
            return;
        }

        _allItems.RemoveAll(x => x.Id == item.Id);
        ApplyFilter();
    }

    private void CopyUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            PlatformServices.Clipboard.SetText(url);
            StatusText = "Copied URL to clipboard.";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[MobileHistory] CopyUrl");
            StatusText = "Failed to copy URL.";
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<MobileHistoryItem> filtered = _allItems;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = _allItems.Where(item =>
                item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Url.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Host.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered.ToList();
        HistoryItems.Clear();

        foreach (var item in items)
        {
            HistoryItems.Add(item);
        }

        if (_allItems.Count == 0)
        {
            StatusText = "No uploads in history yet.";
        }
        else if (items.Count == 0)
        {
            StatusText = "No uploads match your search.";
        }
        else
        {
            StatusText = $"Showing {items.Count} of {_allItems.Count} upload(s).";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class MobileHistoryItem
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string DateText => DateTime == DateTime.MinValue
        ? string.Empty
        : DateTime.ToLocalTime().ToString("g");
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
    public ICommand? CopyUrlCommand { get; set; }
    public ICommand? DeleteItemCommand { get; set; }
}
