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

using System.Diagnostics;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core;

/// <summary>
/// Contains information about a running or completed task
/// </summary>
public class TaskInfo
{
    public TaskSettings TaskSettings { get; set; }

    public string Status { get; set; } = "";
    public TaskJob Job { get; set; }
    public bool SuppressCompletionNotification { get; set; }

    public bool IsUploadJob
    {
        get
        {
            return Job switch
            {
                TaskJob.Job => TaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost),
                TaskJob.DataUpload or TaskJob.FileUpload or TaskJob.TextUpload or
                TaskJob.ShortenURL or TaskJob.ShareURL or TaskJob.DownloadUpload => true,
                _ => false
            };
        }
    }

    public ProgressManager? Progress { get; set; }
    public event Action<ProgressManager>? UploadProgressChanged;

    private string filePath = "";

    public string FilePath
    {
        get => filePath;
        set
        {
            filePath = value ?? "";
            FileName = string.IsNullOrEmpty(filePath) ? "" : Path.GetFileName(filePath);
        }
    }

    public string FileName { get; private set; } = "";
    public string? TextContent { get; set; }
    public string ThumbnailFilePath { get; set; } = "";
    public EDataType DataType { get; set; }
    public TaskMetadata Metadata { get; set; }

    internal bool PrivacyReviewRequired { get; set; }

    internal Services.Privacy.PrivacyUploadApproval? PrivacyApproval { get; set; }

    internal string? ResolvedUploaderHost { get; set; }

    /// <summary>Uploader instance that produced the successful upload (recorded in History for "Delete from host").</summary>
    internal string? ResolvedUploaderInstanceId { get; set; }

    public string? UploaderHost
    {
        get
        {
            if (!IsUploadJob) return null;

            if (!string.IsNullOrWhiteSpace(ResolvedUploaderHost))
            {
                return ResolvedUploaderHost;
            }

            if (TryGetCategoryForDataType(DataType, out var category))
            {
                var instanceId = TaskSettings.GetDestinationInstanceIdForDataType(DataType);
                var instance = !string.IsNullOrEmpty(instanceId)
                    ? InstanceManager.Instance.GetInstance(instanceId)
                    : InstanceManager.Instance.GetDefaultInstance(category);

                if (instance != null)
                {
                    if (InstanceManager.IsAutoProvider(instance.ProviderId))
                    {
                        var resolved = InstanceManager.Instance.ResolveAutoInstance(category, instance.InstanceId);
                        if (resolved != null)
                        {
                            return resolved.DisplayName;
                        }
                    }

                    return instance.DisplayName;
                }

                if (!string.IsNullOrEmpty(instanceId))
                {
                    return instanceId;
                }
            }

            // Legacy: URL shortener / sharing still read from deprecated enum for display
#pragma warning disable CS0618
            if (DataType == EDataType.URL)
            {
                return Job == TaskJob.ShareURL
                    ? EnumExtensions.GetDescription(TaskSettings.URLSharingServiceDestination)
                    : EnumExtensions.GetDescription(TaskSettings.URLShortenerDestination);
            }
#pragma warning restore CS0618

            return null;
        }
    }

    public DateTime TaskStartTime { get; set; }
    public DateTime TaskEndTime { get; set; }

    public TimeSpan TaskDuration => TaskEndTime - TaskStartTime;

    public Stopwatch? UploadDuration { get; set; }

    public UploadResult Result { get; set; }

    /// <summary>
    /// Database identifier of the durable history row created for this task. This is carried
    /// into the completion toast so shared History actions operate on the real row.
    /// </summary>
    public long? HistoryItemId { get; set; }

    /// <summary>
    /// Correlation identifier for structured logging across capture, save, and upload stages.
    /// [2026-01-10T14:40:00+08:00]
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    public TaskInfo(TaskSettings? taskSettings = null)
    {
        TaskSettings = taskSettings ?? new TaskSettings();
        Metadata = new TaskMetadata();
        Result = new UploadResult();
    }

    public void SetFileName(string? fileName)
    {
        FileName = fileName ?? string.Empty;
    }

    public void ReportUploadProgress(ProgressManager progress)
    {
        Progress = progress;
        UploadProgressChanged?.Invoke(progress);
    }

    public Dictionary<string, string>? GetTags()
    {
        if (Metadata == null) return null;

        var tags = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(Metadata.WindowTitle))
        {
            tags.Add("WindowTitle", Metadata.WindowTitle);
        }

        if (!string.IsNullOrEmpty(Metadata.ProcessName))
        {
            tags.Add("ProcessName", Metadata.ProcessName);
        }

        if (!string.IsNullOrWhiteSpace(Metadata.OcrText))
        {
            tags.Add(nameof(Metadata.OcrText), Metadata.OcrText);
        }

        return tags.Count > 0 ? tags : null;
    }

    public override string ToString()
    {
        string text = Result?.ToString() ?? "";

        if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(FilePath))
        {
            text = FilePath;
        }

        return text;
    }

    private static bool TryGetCategoryForDataType(EDataType dataType, out UploaderCategory category)
    {
        category = dataType switch
        {
            EDataType.Image => UploaderCategory.Image,
            EDataType.Text => UploaderCategory.Text,
            EDataType.File => UploaderCategory.File,
            EDataType.URL => UploaderCategory.UrlShortener,
            _ => UploaderCategory.File
        };

        return dataType is EDataType.Image or EDataType.Text or EDataType.File or EDataType.URL;
    }

    // TODO: Add GetHistoryItem() when HistoryLib is ported
}
