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
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;

namespace Ava.ViewModels;

public class MobileUploadViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _statusText = "Share files to XerahS to upload them.";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isUploading;
    public bool IsUploading
    {
        get => _isUploading;
        set { _isUploading = value; OnPropertyChanged(); }
    }

    public ObservableCollection<UploadResultItem> Results { get; } = new();

    public ICommand CopyUrlCommand { get; }
    public ICommand CopyErrorCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenHistoryCommand { get; }

    private readonly UploadQueueService _uploadQueueService;

    /// <summary>
    /// Action to navigate to settings view - set by MobileApp
    /// </summary>
    public static Action? OnOpenSettings { get; set; }

    /// <summary>
    /// Action to navigate to history view - set by MobileApp
    /// </summary>
    public static Action? OnOpenHistory { get; set; }

    public MobileUploadViewModel()
    {
        _uploadQueueService = UploadQueueService.Instance;
        _uploadQueueService.StateChanged -= OnQueueStateChanged;
        _uploadQueueService.StateChanged += OnQueueStateChanged;
        _uploadQueueService.ItemCompleted -= OnQueueItemCompleted;
        _uploadQueueService.ItemCompleted += OnQueueItemCompleted;

        CopyUrlCommand = new RelayCommand<string>(CopyUrl);
        CopyErrorCommand = new RelayCommand<string>(CopyError);
        OpenSettingsCommand = new RelayCommand(_ => OnOpenSettings?.Invoke());
        OpenHistoryCommand = new RelayCommand(_ => OnOpenHistory?.Invoke());

        if (_uploadQueueService.IsProcessing || _uploadQueueService.PendingCount > 0)
        {
            var activeCount = _uploadQueueService.PendingCount + (_uploadQueueService.IsProcessing ? 1 : 0);
            IsUploading = true;
            StatusText = $"Uploading {activeCount} queued file(s)...";
        }
    }

    public void ProcessFiles(string[] filePaths)
    {
        if (filePaths.Length == 0)
        {
            StatusText = "No files received.";
            return;
        }

        var validPaths = new List<string>();

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                DebugHelper.WriteLine($"[Mobile] File not found: {filePath}");
                AddResult(Path.GetFileName(filePath), false, null, "File not found");
                continue;
            }

            validPaths.Add(filePath);
        }

        var queuedCount = _uploadQueueService.EnqueueFiles(validPaths);
        if (queuedCount > 0)
        {
            StatusText = $"Queued {queuedCount} file(s) for upload.";
        }
        else if (validPaths.Count == 0)
        {
            UpdateCompletionStatus();
        }
    }

    private void OnQueueStateChanged(object? sender, UploadQueueStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsUploading = e.IsProcessing;

            if (e.IsProcessing)
            {
                var activeCount = e.PendingCount + 1;
                StatusText = $"Uploading {activeCount} queued file(s)...";
                return;
            }

            UpdateCompletionStatus();
        });
    }

    private void OnQueueItemCompleted(object? sender, UploadQueueItemCompletedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddResult(e.FileName, e.Success, e.Url, e.Error);

            if (!_uploadQueueService.IsProcessing)
            {
                UpdateCompletionStatus();
            }
        });
    }

    private void AddResult(string fileName, bool success, string? url, string? error)
    {
        Results.Add(new UploadResultItem
        {
            FileName = fileName,
            Success = success,
            Url = url,
            Error = error,
            CopyUrlCommand = CopyUrlCommand,
            CopyErrorCommand = CopyErrorCommand
        });
    }

    private void UpdateCompletionStatus()
    {
        IsUploading = false;

        if (Results.Count == 0)
        {
            StatusText = "Share files to XerahS to upload them.";
            return;
        }

        var successCount = Results.Count(r => r.Success);
        StatusText = successCount == Results.Count
            ? $"All {successCount} file(s) uploaded successfully!"
            : $"{successCount} of {Results.Count} file(s) uploaded.";
    }

    private void CopyUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            PlatformServices.Clipboard.SetText(url);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[Mobile] CopyUrl");
        }
    }

    private void CopyError(string? error)
    {
        if (string.IsNullOrEmpty(error)) return;

        try
        {
            PlatformServices.Clipboard.SetText(error);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[Mobile] CopyError");
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class UploadResultItem
{
    public string FileName { get; set; } = "";
    public bool Success { get; set; }
    public string? Url { get; set; }
    public string? Error { get; set; }
    public bool HasUrl => !string.IsNullOrEmpty(Url);
    public bool HasError => !string.IsNullOrEmpty(Error);
    public ICommand? CopyUrlCommand { get; set; }
    public ICommand? CopyErrorCommand { get; set; }
}

internal class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;

    public RelayCommand(Action<T?> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute((T?)parameter);
}

internal class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
