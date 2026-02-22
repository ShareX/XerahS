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
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;

namespace XerahS.Mobile.Maui.ViewModels;

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
    public IAsyncRelayCommand PickFilesCommand { get; }

    private readonly UploadQueueService _uploadQueueService;

    /// <summary>
    /// Action to navigate to settings view - set by MobileUploadPage
    /// </summary>
    public static Action? OnOpenSettings { get; set; }

    /// <summary>
    /// Action to navigate to upload history view - set by MobileUploadPage
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
        OpenSettingsCommand = new RelayCommand(() => OnOpenSettings?.Invoke());
        OpenHistoryCommand = new RelayCommand(() => OnOpenHistory?.Invoke());
        PickFilesCommand = new AsyncRelayCommand(PickFilesAsync);

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
        MainThread.BeginInvokeOnMainThread(() =>
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
        MainThread.BeginInvokeOnMainThread(() =>
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

    private async Task PickFilesAsync()
    {
        IReadOnlyList<FileResult> files;

        try
        {
            var pickedFiles = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Choose photos or files"
            });

            files = pickedFiles?.OfType<FileResult>().ToList() ?? [];
        }
        catch (OperationCanceledException)
        {
            StatusText = "No files selected.";
            return;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[Mobile] PickFiles");
            StatusText = "Could not open file picker.";
            return;
        }

        if (files.Count == 0)
        {
            StatusText = "No files selected.";
            return;
        }

        var localPaths = new List<string>(files.Count);

        foreach (var file in files)
        {
            if (!string.IsNullOrWhiteSpace(file.FullPath))
            {
                localPaths.Add(file.FullPath);
                continue;
            }

            try
            {
                await using var source = await file.OpenReadAsync();
                var extension = Path.GetExtension(file.FileName);
                var targetPath = Path.Combine(FileSystem.CacheDirectory, $"xerahs_mobile_{Guid.NewGuid():N}{extension}");
                await using var target = File.Create(targetPath);
                await source.CopyToAsync(target);
                localPaths.Add(targetPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"[Mobile] Failed to read picked file: {file.FileName}");
            }
        }

        if (localPaths.Count == 0)
        {
            StatusText = "No readable files were selected.";
            return;
        }

        ProcessFiles(localPaths.ToArray());
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
