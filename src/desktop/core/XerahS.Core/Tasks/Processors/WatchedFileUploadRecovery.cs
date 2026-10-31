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

using System.IO.Enumeration;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.History;
using XerahS.Uploaders;

namespace XerahS.Core.Tasks.Processors;

/// <summary>
/// When omaxerahs is handed a screenshot that the watch-folder daemon has already moved and
/// uploaded, return that recorded URL instead of failing on the path that no longer exists.
/// </summary>
internal static class WatchedFileUploadRecovery
{
    internal static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan InFlightGrace = TimeSpan.FromSeconds(12);

    internal static UploadResult? ToAdoptedResult(HistoryItem? item)
    {
        if (item == null || !IsHttpUrl(item.URL))
        {
            return null;
        }

        return new UploadResult
        {
            IsSuccess = true,
            URL = item.URL,
            Response = item.URL
        };
    }

    internal static HistoryItem? FindRecordedUpload(string sourcePath, DateTime? notBefore = null)
    {
        string? fileName = Path.GetFileName(sourcePath);
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        try
        {
            string historyPath = SettingsManager.GetHistoryFilePath();
            if (!File.Exists(historyPath))
            {
                return null;
            }

            using var history = new HistoryManagerSQLite(historyPath);
            return history.FindBySourceFileName(fileName, notBefore);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to look up an upload already recorded for a moved file");
            return null;
        }
    }

    internal static string? FindMovedPath(string sourcePath)
    {
        TaskSettings? settings = SettingsManager.DefaultTaskSettings;
        if (settings?.WatchFolderList == null)
        {
            return null;
        }

        foreach (WatchFolderSettings folder in settings.WatchFolderList)
        {
            if (!WouldMove(sourcePath, folder))
            {
                continue;
            }

            WorkflowSettings? workflow = string.IsNullOrEmpty(folder.WorkflowId)
                ? null
                : SettingsManager.GetWorkflowById(folder.WorkflowId);
            TaskSettings taskSettings = workflow?.TaskSettings ?? settings;
            string candidate = Path.Combine(TaskHelpers.GetScreenshotsFolder(taskSettings), Path.GetFileName(sourcePath));
            if (!File.Exists(candidate))
            {
                continue;
            }

            if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    internal static bool WouldMove(string sourcePath, WatchFolderSettings folder)
    {
        if (!folder.Enabled || !folder.MoveFilesToScreenshotsFolder || string.IsNullOrWhiteSpace(folder.FolderPath))
        {
            return false;
        }

        string watchRoot = Path.GetFullPath(folder.FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
        if (string.IsNullOrEmpty(sourceDir))
        {
            return false;
        }

        bool inFolder = string.Equals(sourceDir, watchRoot, StringComparison.OrdinalIgnoreCase);
        if (!inFolder && folder.IncludeSubdirectories)
        {
            inFolder = sourceDir.StartsWith(watchRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        if (!inFolder)
        {
            return false;
        }

        return NameMatchesFilter(Path.GetFileName(sourcePath), folder.Filter);
    }

    internal static bool NameMatchesFilter(string fileName, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        string[] parts = filter.Split([';', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return true;
        }

        foreach (string part in parts)
        {
            string expression = string.IsNullOrWhiteSpace(part) ? "*.*" : part;
            if (expression == "*.*" || FileSystemName.MatchesSimpleExpression(expression, fileName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHttpUrl(string? url)
    {
        return url != null &&
               (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }
}
