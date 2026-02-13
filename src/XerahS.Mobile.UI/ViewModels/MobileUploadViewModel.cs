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
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using XerahS.Platform.Abstractions;

namespace XerahS.Mobile.UI.ViewModels;

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

    private int _pendingCount;

    public MobileUploadViewModel()
    {
        CopyUrlCommand = new RelayCommand<string>(CopyUrl);
    }

    public async void ProcessFiles(string[] filePaths)
    {
        if (filePaths.Length == 0)
        {
            StatusText = "No files received.";
            return;
        }

        IsUploading = true;
        _pendingCount = filePaths.Length;
        StatusText = $"Uploading {filePaths.Length} file(s)...";

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                DebugHelper.WriteLine($"[Mobile] File not found: {filePath}");
                DecrementPending(Path.GetFileName(filePath), false, null, "File not found");
                continue;
            }

            try
            {
                var defaultSettings = SettingsManager.DefaultTaskSettings;
                var clonedSettings = WatchFolderManager.CloneTaskSettings(defaultSettings);
                clonedSettings.Job = WorkflowType.FileUpload;
                clonedSettings.AfterUploadJob = AfterUploadTasks.CopyURLToClipboard;

                var fileName = Path.GetFileName(filePath);

                TaskManager.Instance.TaskCompleted += OnTaskCompleted;
                await TaskManager.Instance.StartFileTask(clonedSettings, filePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "[Mobile] ProcessFiles");
                DecrementPending(Path.GetFileName(filePath), false, null, ex.Message);
            }
        }
    }

    private void OnTaskCompleted(object? sender, WorkerTask task)
    {
        TaskManager.Instance.TaskCompleted -= OnTaskCompleted;

        var fileName = task.Info?.FileName ?? task.Info?.FilePath ?? "Unknown";
        fileName = Path.GetFileName(fileName);
        var url = task.Info?.Result?.URL;
        var success = !string.IsNullOrEmpty(url);
        var error = success ? null : (task.Error?.Message ?? task.Info?.Result?.Response ?? "Upload failed");

        Dispatcher.UIThread.Post(() => DecrementPending(fileName, success, url, error));
    }

    private void DecrementPending(string fileName, bool success, string? url, string? error)
    {
        Results.Add(new UploadResultItem
        {
            FileName = fileName,
            Success = success,
            Url = url,
            Error = error
        });

        _pendingCount--;

        if (_pendingCount <= 0)
        {
            IsUploading = false;
            var successCount = Results.Count(r => r.Success);
            StatusText = successCount == Results.Count
                ? $"All {successCount} file(s) uploaded successfully!"
                : $"{successCount} of {Results.Count} file(s) uploaded.";
        }
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
