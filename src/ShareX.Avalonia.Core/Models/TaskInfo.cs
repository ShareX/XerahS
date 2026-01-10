#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using XerahS.Common;
using XerahS.Uploaders;
using System.Diagnostics;

namespace XerahS.Core;

/// <summary>
/// Contains information about a running or completed task
/// </summary>
public class TaskInfo
{
    public TaskSettings TaskSettings { get; set; }

    public string Status { get; set; } = "";
    public TaskJob Job { get; set; }

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
    public string ThumbnailFilePath { get; set; } = "";
    public EDataType DataType { get; set; }
    public TaskMetadata Metadata { get; set; }

    public EDataType UploadDestination
    {
        get
        {
            if ((DataType == EDataType.Image && TaskSettings.ImageDestination == ImageDestination.FileUploader) ||
                (DataType == EDataType.Text && TaskSettings.TextDestination == TextDestination.FileUploader))
            {
                return EDataType.File;
            }

            return DataType;
        }
    }

    public string? UploaderHost
    {
        get
        {
            if (!IsUploadJob) return null;

            return UploadDestination switch
            {
                EDataType.Image => EnumExtensions.GetDescription(TaskSettings.ImageDestination),
                EDataType.Text => EnumExtensions.GetDescription(TaskSettings.TextDestination),
                EDataType.File => DataType switch
                {
                    EDataType.Image => EnumExtensions.GetDescription(TaskSettings.ImageFileDestination),
                    EDataType.Text => EnumExtensions.GetDescription(TaskSettings.TextFileDestination),
                    _ => EnumExtensions.GetDescription(TaskSettings.FileDestination)
                },
                EDataType.URL => Job == TaskJob.ShareURL
                    ? EnumExtensions.GetDescription(TaskSettings.URLSharingServiceDestination)
                    : EnumExtensions.GetDescription(TaskSettings.URLShortenerDestination),
                _ => null
            };
        }
    }

    public DateTime TaskStartTime { get; set; }
    public DateTime TaskEndTime { get; set; }

    public TimeSpan TaskDuration => TaskEndTime - TaskStartTime;

    public Stopwatch? UploadDuration { get; set; }

    public UploadResult Result { get; set; }

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

    // TODO: Add GetHistoryItem() when HistoryLib is ported
}
