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
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using XerahS.Platform.Abstractions;
using BitmapConversionHelpers = XerahS.Editor.Helpers.BitmapConversionHelpers;

namespace XerahS.UI.ViewModels;

public enum UploadQueueItemStatus
{
    Pending,
    Uploading,
    Completed,
    Failed
}

public partial class UploadQueueItem : ObservableObject
{
    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private EDataType _dataType;

    [ObservableProperty]
    private UploadQueueItemStatus _status = UploadQueueItemStatus.Pending;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string? _resultURL;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isPending = true;

    [ObservableProperty]
    private bool _isFailed;

    public string? FilePath { get; set; }
    public string? TextContent { get; set; }
    public SKBitmap? Image { get; set; }

    partial void OnStatusChanged(UploadQueueItemStatus value)
    {
        IsPending = value == UploadQueueItemStatus.Pending;
        IsFailed = value == UploadQueueItemStatus.Failed;
    }
}

public partial class UploadContentViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private CancellationTokenSource? _uploadCts;
    private Bitmap? _selectedPreviewImage;
    private string _selectedFileMetadata = "No file selected.";

    public ObservableCollection<UploadQueueItem> Items { get; } = new();

    [ObservableProperty]
    private UploadQueueItem? _selectedItem;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private string _statusText = "No items";

    [ObservableProperty]
    private int _overallProgressPercent;

    [ObservableProperty]
    private bool _hasPendingItems;

    [ObservableProperty]
    private bool _hasItems;

    public Bitmap? SelectedPreviewImage
    {
        get => _selectedPreviewImage;
        private set
        {
            if (ReferenceEquals(_selectedPreviewImage, value))
            {
                return;
            }

            _selectedPreviewImage?.Dispose();
            _selectedPreviewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedPreviewImage));
            OnPropertyChanged(nameof(ShowImagePreview));
        }
    }

    public string SelectedFileMetadata
    {
        get => _selectedFileMetadata;
        private set
        {
            if (_selectedFileMetadata == value)
            {
                return;
            }

            _selectedFileMetadata = value;
            OnPropertyChanged();
        }
    }

    public bool HasSelectedItem => SelectedItem != null;
    public bool HasSelectedPreviewImage => SelectedPreviewImage != null;
    public bool ShowImagePreview => SelectedItem?.DataType == EDataType.Image && HasSelectedPreviewImage;
    public bool ShowTextPreview => SelectedItem?.DataType is EDataType.Text or EDataType.URL;
    public bool ShowFilePreview => SelectedItem?.DataType == EDataType.File;
    public string SelectedTextPreview => SelectedItem?.TextContent ?? string.Empty;
    public string SelectedItemTypeText => $"Type: {GetDataTypeDisplayText(SelectedItem?.DataType)}";

    public event EventHandler? FilePickerRequested;
    public event EventHandler? FolderPickerRequested;
    public event EventHandler? TextInputRequested;
    public event EventHandler? URLInputRequested;

    public UploadContentViewModel()
    {
        Items.CollectionChanged += (_, _) => UpdateStatus();
    }

    partial void OnSelectedItemChanged(UploadQueueItem? value)
    {
        UpdateSelectedPreview();
    }

    [RelayCommand]
    private void LoadFromClipboard()
    {
        if (!PlatformServices.IsInitialized || PlatformServices.Clipboard == null) return;

        var content = ClipboardContentHelper.ParseClipboard(PlatformServices.Clipboard);
        if (content == null)
        {
            DebugHelper.WriteLine("UploadContent: Clipboard is empty or unsupported.");
            return;
        }

        UploadQueueItem? firstAddedItem = null;

        switch (content.DataType)
        {
            case EDataType.Image when content.Image != null:
                firstAddedItem = new UploadQueueItem
                {
                    DisplayName = "Clipboard Image",
                    Description = $"{content.Image.Width}x{content.Image.Height}",
                    DataType = EDataType.Image,
                    Image = content.Image
                };
                Items.Add(firstAddedItem);
                break;

            case EDataType.Text when !string.IsNullOrEmpty(content.Text):
                firstAddedItem = new UploadQueueItem
                {
                    DisplayName = "Clipboard Text",
                    Description = $"{content.Text.Length} characters",
                    DataType = EDataType.Text,
                    TextContent = content.Text
                };
                Items.Add(firstAddedItem);
                break;

            case EDataType.File when content.Files != null:
                foreach (var file in content.Files)
                {
                    var addedItem = AddFileItem(file);
                    if (firstAddedItem == null && addedItem != null)
                    {
                        firstAddedItem = addedItem;
                    }
                }
                break;
        }

        if (firstAddedItem != null)
        {
            SelectedItem = firstAddedItem;
        }

        DebugHelper.WriteLine(
            $"[UploadContentDebug] Clipboard parsed: dataType={content.DataType}, " +
            $"textLength={(content.Text?.Length ?? 0)}, fileCount={(content.Files?.Length ?? 0)}, " +
            $"image={(content.Image != null ? $"{content.Image.Width}x{content.Image.Height}" : "null")}.");
        DebugHelper.WriteLine($"UploadContent: Loaded {content.DataType} from clipboard.");
    }

    [RelayCommand]
    private void AddFiles()
    {
        FilePickerRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AddFolder()
    {
        FolderPickerRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AddText()
    {
        TextInputRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AddURL()
    {
        URLInputRequested?.Invoke(this, EventArgs.Empty);
    }

    public UploadQueueItem? AddFileItem(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

        var fileInfo = new FileInfo(filePath);
        var item = new UploadQueueItem
        {
            DisplayName = Path.GetFileName(filePath),
            Description = FormatFileSize(fileInfo.Length),
            DataType = EDataType.File,
            FilePath = filePath
        };

        Items.Add(item);
        return item;
    }

    public void AddFolderFiles(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

        var files = Directory.GetFiles(folderPath);
        UploadQueueItem? firstAddedItem = null;
        foreach (var file in files)
        {
            var addedItem = AddFileItem(file);
            if (firstAddedItem == null && addedItem != null)
            {
                firstAddedItem = addedItem;
            }
        }

        if (firstAddedItem != null)
        {
            SelectedItem = firstAddedItem;
        }

        DebugHelper.WriteLine($"UploadContent: Added {files.Length} files from folder '{folderPath}'.");
    }

    public void AddTextItem(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var item = new UploadQueueItem
        {
            DisplayName = "Text",
            Description = $"{text.Length} characters",
            DataType = EDataType.Text,
            TextContent = text
        };

        Items.Add(item);
        SelectedItem = item;
    }

    public void AddURLItem(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        var item = new UploadQueueItem
        {
            DisplayName = url.Length > 60 ? url.Substring(0, 57) + "..." : url,
            Description = "URL",
            DataType = EDataType.URL,
            TextContent = url
        };

        Items.Add(item);
        SelectedItem = item;
    }

    [RelayCommand]
    private async Task UploadAllAsync()
    {
        if (IsUploading) return;

        var pendingItems = Items.Where(i => i.Status == UploadQueueItemStatus.Pending).ToList();
        if (pendingItems.Count == 0) return;

        DebugHelper.WriteLine($"[UploadContentDebug] UploadAll started. pendingItems={pendingItems.Count}");

        IsUploading = true;
        _uploadCts = new CancellationTokenSource();

        try
        {
            foreach (var item in pendingItems)
            {
                if (_uploadCts.Token.IsCancellationRequested) break;

                await UploadItemAsync(item);
                UpdateStatus();
            }
        }
        finally
        {
            IsUploading = false;
            _uploadCts?.Dispose();
            _uploadCts = null;
            UpdateStatus();
        }
    }

    private async Task UploadItemAsync(UploadQueueItem item)
    {
        item.Status = UploadQueueItemStatus.Uploading;
        item.ProgressPercent = 0;
        item.ErrorMessage = null;

        DebugHelper.WriteLine(
            $"[UploadContentDebug] UploadItem start: dataType={item.DataType}, " +
            $"displayName=\"{item.DisplayName}\", filePath=\"{item.FilePath ?? string.Empty}\", " +
            $"textLength={(item.TextContent?.Length ?? 0)}");

        WorkerTask? capturedTask = null;

        void OnTaskStarted(object? sender, WorkerTask task)
        {
            capturedTask = task;
            TaskManager.Instance.TaskStarted -= OnTaskStarted;

            task.Info.UploadProgressChanged += progress =>
            {
                if (!double.IsNaN(progress.Percentage) && !double.IsInfinity(progress.Percentage))
                {
                    item.ProgressPercent = (int)Math.Clamp(progress.Percentage, 0, 99);
                }
            };
        }

        TaskManager.Instance.TaskStarted += OnTaskStarted;

        try
        {
            var settings = CreateUploadTaskSettings(item.DataType);
            DebugHelper.WriteLine(
                $"[UploadContentDebug] TaskSettings resolved for item: job={settings.Job}, workflowId=\"{settings.WorkflowId ?? string.Empty}\", " +
                $"destinationInstanceId=\"{settings.DestinationInstanceId ?? string.Empty}\", " +
                $"urlShortenerInstanceId=\"{settings.UrlShortenerDestinationInstanceId ?? string.Empty}\", " +
                $"afterCapture={settings.AfterCaptureJob}, afterUpload={settings.AfterUploadJob}");

            switch (item.DataType)
            {
                case EDataType.Image when item.Image != null:
                    settings.Job = WorkflowType.PrintScreen;
                    await TaskManager.Instance.StartTask(settings, item.Image);
                    break;

                case EDataType.File when !string.IsNullOrEmpty(item.FilePath):
                    settings.Job = WorkflowType.FileUpload;
                    await TaskManager.Instance.StartFileTask(settings, item.FilePath);
                    break;

                case EDataType.Text when !string.IsNullOrEmpty(item.TextContent):
                    settings.Job = WorkflowType.ClipboardUploadWithContentViewer;
                    DebugHelper.WriteLine(
                        $"[UploadContentDebug] Starting text upload task. textLength={item.TextContent.Length}, " +
                        $"textPreview=\"{GetTextPreview(item.TextContent)}\"");
                    await TaskManager.Instance.StartTextTask(settings, item.TextContent);
                    break;

                case EDataType.URL when !string.IsNullOrEmpty(item.TextContent):
                    settings.Job = WorkflowType.ClipboardUploadWithContentViewer;
                    DebugHelper.WriteLine(
                        $"[UploadContentDebug] Starting URL upload task. urlLength={item.TextContent.Length}, " +
                        $"urlPreview=\"{GetTextPreview(item.TextContent)}\"");
                    await TaskManager.Instance.StartTextTask(settings, item.TextContent);
                    break;

                default:
                    item.Status = UploadQueueItemStatus.Failed;
                    item.ErrorMessage = "No content to upload";
                    return;
            }

            if (capturedTask?.IsSuccessful == true)
            {
                item.Status = UploadQueueItemStatus.Completed;
                item.ProgressPercent = 100;
                item.ResultURL = capturedTask.Info?.Result?.URL ?? capturedTask.Info?.Metadata?.UploadURL;
                DebugHelper.WriteLine($"[UploadContentDebug] UploadItem success: resultUrl=\"{item.ResultURL ?? string.Empty}\"");
            }
            else
            {
                item.Status = UploadQueueItemStatus.Failed;
                item.ErrorMessage = capturedTask?.Error?.Message ?? "Upload failed";
                DebugHelper.WriteLine($"[UploadContentDebug] UploadItem failed: error=\"{item.ErrorMessage}\"");
            }
        }
        catch (Exception ex)
        {
            item.Status = UploadQueueItemStatus.Failed;
            item.ErrorMessage = ex.Message;
            DebugHelper.WriteLine($"[UploadContentDebug] UploadItem exception: {ex.Message}");
            DebugHelper.WriteException(ex, "UploadContent: Upload failed");
        }
        finally
        {
            TaskManager.Instance.TaskStarted -= OnTaskStarted;
        }
    }

    private static TaskSettings CreateUploadTaskSettings(EDataType dataType)
    {
        var preferredWorkflowJob = dataType == EDataType.File
            ? WorkflowType.FileUpload
            : WorkflowType.ClipboardUploadWithContentViewer;

        var workflow = SettingsManager.GetFirstWorkflow(preferredWorkflowJob);

        // Upload Content fallback:
        // if no clipboard workflow exists, use FileUpload workflow settings.
        if (workflow == null && preferredWorkflowJob == WorkflowType.ClipboardUploadWithContentViewer)
        {
            workflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
        }

        DebugHelper.WriteLine(
            $"[UploadContentDebug] CreateUploadTaskSettings: dataType={dataType}, preferredJob={preferredWorkflowJob}, " +
            $"workflowFound={(workflow != null)}, workflowId=\"{workflow?.Id ?? string.Empty}\", workflowJob={(workflow?.Job.ToString() ?? "None")}");

        TaskSettings settings;
        if (workflow?.TaskSettings != null)
        {
            settings = CloneTaskSettings(workflow.TaskSettings);
            settings.WorkflowId = workflow.Id;
        }
        else
        {
            settings = CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());
        }

        if (settings.Job == WorkflowType.None)
        {
            settings.Job = preferredWorkflowJob;
        }

        DebugHelper.WriteLine(
            $"[UploadContentDebug] CreateUploadTaskSettings result: job={settings.Job}, " +
            $"destinationInstanceId=\"{settings.DestinationInstanceId ?? string.Empty}\", " +
            $"urlShortenerInstanceId=\"{settings.UrlShortenerDestinationInstanceId ?? string.Empty}\"");

        return settings;
    }

    private static TaskSettings CloneTaskSettings(TaskSettings source)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        string json = JsonConvert.SerializeObject(source, serializerSettings);
        return JsonConvert.DeserializeObject<TaskSettings>(json, serializerSettings) ?? new TaskSettings();
    }

    [RelayCommand]
    private void RemoveItem(UploadQueueItem? item)
    {
        if (item == null) return;

        if (item.Image != null && item.Status != UploadQueueItemStatus.Uploading)
        {
            item.Image.Dispose();
            item.Image = null;
        }

        Items.Remove(item);

        if (ReferenceEquals(SelectedItem, item))
        {
            SelectedItem = Items.FirstOrDefault();
        }
    }

    [RelayCommand]
    private async Task RetryItemAsync(UploadQueueItem? item)
    {
        if (item == null || item.Status != UploadQueueItemStatus.Failed) return;

        item.Status = UploadQueueItemStatus.Pending;
        item.ErrorMessage = null;
        item.ProgressPercent = 0;
        item.ResultURL = null;

        await UploadItemAsync(item);
        UpdateStatus();
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        var completed = Items.Where(i =>
            i.Status is UploadQueueItemStatus.Completed or UploadQueueItemStatus.Failed).ToList();

        foreach (var item in completed)
        {
            if (item.Image != null)
            {
                item.Image.Dispose();
                item.Image = null;
            }
            Items.Remove(item);
        }

        if (SelectedItem == null || !Items.Contains(SelectedItem))
        {
            SelectedItem = Items.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void CancelUpload()
    {
        _uploadCts?.Cancel();
    }

    private void UpdateStatus()
    {
        int total = Items.Count;
        int completed = Items.Count(i => i.Status == UploadQueueItemStatus.Completed);
        int failed = Items.Count(i => i.Status == UploadQueueItemStatus.Failed);
        int pending = Items.Count(i => i.Status == UploadQueueItemStatus.Pending);

        HasItems = total > 0;
        HasPendingItems = pending > 0;

        if (total == 0)
        {
            StatusText = "No items";
            OverallProgressPercent = 0;
        }
        else
        {
            StatusText = $"{total} items | {completed} completed | {failed} failed";
            OverallProgressPercent = total > 0 ? (int)((double)(completed + failed) / total * 100) : 0;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.#} GB";
    }

    private static string GetDataTypeDisplayText(EDataType? dataType)
    {
        if (!dataType.HasValue)
        {
            return "None";
        }

        return dataType.Value switch
        {
            EDataType.Image => "Image",
            EDataType.Text => "Text",
            EDataType.File => "File",
            EDataType.URL => "URL",
            _ => dataType.Value.ToString()
        };
    }

    private static string GetTextPreview(string text, int maxLength = 120)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string compact = text.Replace("\r", "\\r").Replace("\n", "\\n");
        return compact.Length <= maxLength ? compact : compact.Substring(0, maxLength) + "...";
    }

    private void UpdateSelectedPreview()
    {
        SelectedPreviewImage = null;
        SelectedFileMetadata = "No file selected.";

        if (SelectedItem?.DataType == EDataType.Image && SelectedItem.Image != null)
        {
            SelectedPreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(SelectedItem.Image);
        }

        if (SelectedItem?.DataType == EDataType.File && !string.IsNullOrWhiteSpace(SelectedItem.FilePath))
        {
            if (File.Exists(SelectedItem.FilePath))
            {
                var fileInfo = new FileInfo(SelectedItem.FilePath);
                SelectedFileMetadata =
                    $"Name: {fileInfo.Name}{Environment.NewLine}" +
                    $"Extension: {fileInfo.Extension}{Environment.NewLine}" +
                    $"Size: {FormatFileSize(fileInfo.Length)}{Environment.NewLine}" +
                    $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                    $"Path: {fileInfo.FullName}";
            }
            else
            {
                SelectedFileMetadata = $"File not found: {SelectedItem.FilePath}";
            }
        }

        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(ShowTextPreview));
        OnPropertyChanged(nameof(ShowFilePreview));
        OnPropertyChanged(nameof(SelectedTextPreview));
        OnPropertyChanged(nameof(SelectedItemTypeText));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _uploadCts?.Cancel();
        _uploadCts?.Dispose();
        SelectedPreviewImage = null;

        foreach (var item in Items)
        {
            if (item.Image != null)
            {
                item.Image.Dispose();
                item.Image = null;
            }
        }

        Items.Clear();
    }
}
