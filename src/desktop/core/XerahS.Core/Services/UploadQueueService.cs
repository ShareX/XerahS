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

using Newtonsoft.Json;
using XerahS.Common;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;

namespace XerahS.Core.Services;

/// <summary>
/// Persistent, sequential upload queue for mobile surfaces.
/// The queue survives process restarts and retries failed uploads.
/// </summary>
public sealed class UploadQueueService
{
    private const int MaxRetryCount = 2;
    private const string QueueFileName = "MobileUploadQueue.json";

    private readonly object _queueLock = new();
    private readonly Queue<UploadQueueItem> _queue = new();

    private bool _isProcessing;
    private static readonly Lazy<UploadQueueService> LazyInstance = new(() => new UploadQueueService());

    public static UploadQueueService Instance => LazyInstance.Value;

    public event EventHandler<UploadQueueStateChangedEventArgs>? StateChanged;
    public event EventHandler<UploadQueueItemCompletedEventArgs>? ItemCompleted;

    private string QueueFilePath => Path.Combine(SettingsManager.SettingsFolder, QueueFileName);

    private UploadQueueService()
    {
        LoadQueueFromDisk();
    }

    public int EnqueueFiles(IEnumerable<string> filePaths)
    {
        return EnqueueFiles(filePaths, ownedTempFiles: null);
    }

    /// <summary>
    /// Enqueue file paths for upload. Paths listed in
    /// <paramref name="ownedTempFiles"/> are best-effort deleted after the
    /// queue finishes processing them (success or final failure). Callers that
    /// materialize picker streams into <c>Path.GetTempPath()</c> must mark those
    /// copies so they do not accumulate indefinitely on mobile.
    /// </summary>
    public int EnqueueFiles(IEnumerable<string> filePaths, IEnumerable<string>? ownedTempFiles)
    {
        if (filePaths == null)
        {
            return 0;
        }

        HashSet<string>? owned = null;
        if (ownedTempFiles != null)
        {
            owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in ownedTempFiles)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    owned.Add(path);
                }
            }
        }

        var addedCount = 0;

        lock (_queueLock)
        {
            foreach (var filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                _queue.Enqueue(new UploadQueueItem
                {
                    FilePath = filePath,
                    EnqueuedUtc = DateTime.UtcNow,
                    // Ownership is in-memory only: snapshots intentionally
                    // deserialize IsOwnedTempFile as false so a restarted
                    // process never auto-deletes a stale path.
                    IsOwnedTempFile = owned != null && owned.Contains(filePath)
                });
                addedCount++;
            }

            SaveQueueSnapshotLocked();
        }

        if (addedCount > 0)
        {
            RaiseStateChanged();
            StartProcessingIfNeeded();
        }

        return addedCount;
    }

    public void ResumePendingReviews()
    {
        lock (_queueLock)
        {
            foreach (var item in _queue) item.ReviewRequired = false;
            SaveQueueSnapshotLocked();
        }
        StartProcessingIfNeeded();
    }

    public int PendingCount
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.Count;
            }
        }
    }

    public bool IsProcessing
    {
        get
        {
            lock (_queueLock)
            {
                return _isProcessing;
            }
        }
    }

    private void StartProcessingIfNeeded()
    {
        lock (_queueLock)
        {
            if (_isProcessing || !_queue.Any(item => !item.ReviewRequired))
            {
                return;
            }

            _isProcessing = true;
            SaveQueueSnapshotLocked();
        }

        RaiseStateChanged();
        _ = Task.Run(ProcessQueueAsync);
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (true)
            {
                UploadQueueItem? item;

                lock (_queueLock)
                {
                    if (!_queue.Any(queued => !queued.ReviewRequired))
                    {
                        _isProcessing = false;
                        SaveQueueSnapshotLocked();
                        break;
                    }

                    item = _queue.Dequeue();
                    if (item.ReviewRequired) { _queue.Enqueue(item); continue; }
                    SaveQueueSnapshotLocked();
                }

                if (item == null)
                {
                    continue;
                }

                var result = await ProcessItemAsync(item).ConfigureAwait(false);
                if (item.ReviewRequired)
                {
                    lock (_queueLock) { _queue.Enqueue(item); SaveQueueSnapshotLocked(); }
                }
                ItemCompleted?.Invoke(this, result);
                RaiseStateChanged();
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Upload queue worker crashed");

            lock (_queueLock)
            {
                _isProcessing = false;
                SaveQueueSnapshotLocked();
            }
        }

        RaiseStateChanged();
    }

    private static async Task<UploadQueueItemCompletedEventArgs> ProcessItemAsync(UploadQueueItem item)
    {
        var fileName = Path.GetFileName(item.FilePath);
        try
        {
            if (!File.Exists(item.FilePath))
            {
                return new UploadQueueItemCompletedEventArgs(fileName, success: false, url: null, error: "File not found");
            }

            var attempts = 0;
            string? lastError = null;

            while (attempts < MaxRetryCount)
            {
                attempts++;

                try
                {
                    using var task = CreateWorkerTask(item.FilePath, item.PrivacyRequired);
                    await task.StartAsync().ConfigureAwait(false);

                    var url = task.Info?.Result?.URL;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        return new UploadQueueItemCompletedEventArgs(fileName, success: true, url: url, error: null);
                    }

                    if (task.Info?.PrivacyReviewRequired == true)
                    {
                        item.ReviewRequired = true;
                        item.PrivacyRequired = true;
                        return new UploadQueueItemCompletedEventArgs(fileName, false, null, "Privacy review required. Resume from XerahS.");
                    }
                    lastError = task.Error?.Message ?? task.Info?.Result?.Response ?? "Upload failed";
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    DebugHelper.WriteException(ex, $"Upload queue failed for: {item.FilePath}");
                }
            }

            return new UploadQueueItemCompletedEventArgs(fileName, success: false, url: null, error: lastError ?? "Upload failed");
        }
        finally
        {
            // Only delete after the queue is done with the path (success or
            // final failure). Retries still see the file while MaxRetryCount
            // is being exhausted.
            TryDeleteOwnedTempFile(item);
        }
    }

    /// <summary>
    /// Best-effort delete of a picker-materialized temp file after the queue
    /// finishes with it. No-op unless <see cref="UploadQueueItem.IsOwnedTempFile"/>
    /// is set by the enqueuer.
    /// </summary>
    internal static void TryDeleteOwnedTempFile(UploadQueueItem item)
    {
        if (item == null || item.ReviewRequired || !item.IsOwnedTempFile || string.IsNullOrWhiteSpace(item.FilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(item.FilePath))
            {
                File.Delete(item.FilePath);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"[UploadQueue] Failed to delete owned temp file: {item.FilePath}");
        }
    }

    private static WorkerTask CreateWorkerTask(string filePath, bool privacyRequired = false)
    {
        var taskSettings = WatchFolderManager.CloneTaskSettings(SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload)?.TaskSettings ?? SettingsManager.DefaultTaskSettings);
        taskSettings.Job = WorkflowType.FileUpload;
        if (privacyRequired) taskSettings.PrivacySettings.Enabled = true;
        taskSettings.AfterUploadJob = AfterUploadTasks.CopyURLToClipboard;

        var task = WorkerTask.Create(taskSettings);
        task.Info.FilePath = filePath;
        task.Info.DataType = EDataType.File;
        task.Info.Job = TaskJob.FileUpload;
        return task;
    }

    private void LoadQueueFromDisk()
    {
        bool shouldStartProcessor;

        lock (_queueLock)
        {
            shouldStartProcessor = false;

            try
            {
                if (!File.Exists(QueueFilePath))
                {
                    return;
                }

                var json = File.ReadAllText(QueueFilePath);
                var snapshot = JsonConvert.DeserializeObject<UploadQueueSnapshot>(json);
                if (snapshot?.Items == null || snapshot.Items.Count == 0)
                {
                    return;
                }

                foreach (var item in snapshot.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.FilePath))
                    {
                        continue;
                    }

                    _queue.Enqueue(item);
                }

                SaveQueueSnapshotLocked();
                shouldStartProcessor = _queue.Count > 0;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load upload queue snapshot");
            }
        }

        if (shouldStartProcessor)
        {
            StartProcessingIfNeeded();
        }
    }

    private void SaveQueueSnapshotLocked()
    {
        try
        {
            if (_queue.Count == 0)
            {
                if (File.Exists(QueueFilePath))
                {
                    File.Delete(QueueFilePath);
                }
                return;
            }

            FileHelpers.CreateDirectoryFromFilePath(QueueFilePath);

            var snapshot = new UploadQueueSnapshot
            {
                Items = _queue.ToList()
            };

            var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            File.WriteAllText(QueueFilePath, json);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to save upload queue snapshot");
        }
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, new UploadQueueStateChangedEventArgs(PendingCount, IsProcessing));
    }
}

public sealed class UploadQueueStateChangedEventArgs : EventArgs
{
    public int PendingCount { get; }
    public bool IsProcessing { get; }

    public UploadQueueStateChangedEventArgs(int pendingCount, bool isProcessing)
    {
        PendingCount = pendingCount;
        IsProcessing = isProcessing;
    }
}

public sealed class UploadQueueItemCompletedEventArgs : EventArgs
{
    public string FileName { get; }
    public bool Success { get; }
    public string? Url { get; }
    public string? Error { get; }

    public UploadQueueItemCompletedEventArgs(string fileName, bool success, string? url, string? error)
    {
        FileName = fileName;
        Success = success;
        Url = url;
        Error = error;
    }
}

public sealed class UploadQueueItem
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime EnqueuedUtc { get; set; }
    public bool ReviewRequired { get; set; }
    public bool PrivacyRequired { get; set; }

    /// <summary>
    /// When true, the queue owns this path and will best-effort delete it after
    /// processing completes. Not persisted across process restarts (snapshots
    /// deserialize this as false) so a restart never auto-deletes a stale path.
    /// </summary>
    [JsonIgnore]
    public bool IsOwnedTempFile { get; set; }
}

internal sealed class UploadQueueSnapshot
{
    public List<UploadQueueItem> Items { get; set; } = [];
}
