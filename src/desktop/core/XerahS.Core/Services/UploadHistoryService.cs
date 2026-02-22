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

using XerahS.Common;
using XerahS.History;

namespace XerahS.Core.Services;

public static class UploadHistoryService
{
    public static IReadOnlyList<UploadHistoryEntry> GetRecentEntries(int limit = 100)
    {
        if (limit <= 0)
        {
            return Array.Empty<UploadHistoryEntry>();
        }

        try
        {
            var historyPath = SettingsManager.GetHistoryFilePath();
            if (!File.Exists(historyPath))
            {
                return Array.Empty<UploadHistoryEntry>();
            }

            using var historyManager = new HistoryManagerSQLite(historyPath);
            var historyItems = historyManager.GetHistoryItems(0, limit);

            return historyItems.Select(item => new UploadHistoryEntry(
                Id: item.Id,
                FileName: string.IsNullOrWhiteSpace(item.FileName)
                    ? Path.GetFileName(item.FilePath ?? string.Empty)
                    : item.FileName,
                FilePath: item.FilePath ?? string.Empty,
                Url: item.URL ?? string.Empty,
                Host: item.Host ?? string.Empty,
                DateTime: item.DateTime)).ToList();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to load upload history");
            return Array.Empty<UploadHistoryEntry>();
        }
    }

    public static bool DeleteEntry(long id)
    {
        if (id <= 0)
        {
            return false;
        }

        try
        {
            var historyPath = SettingsManager.GetHistoryFilePath();
            if (!File.Exists(historyPath))
            {
                return false;
            }

            using var historyManager = new HistoryManagerSQLite(historyPath);
            historyManager.Delete(new HistoryItem { Id = id });
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to delete upload history entry");
            return false;
        }
    }

    public static int ClearEntries()
    {
        try
        {
            var historyPath = SettingsManager.GetHistoryFilePath();
            if (!File.Exists(historyPath))
            {
                return 0;
            }

            using var historyManager = new HistoryManagerSQLite(historyPath);
            var count = historyManager.GetTotalCount();
            if (count <= 0)
            {
                return 0;
            }

            var items = historyManager.GetHistoryItems(0, count);
            if (items.Count == 0)
            {
                return 0;
            }

            historyManager.Delete(items.ToArray());
            return items.Count;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to clear upload history");
            return 0;
        }
    }
}

public sealed record UploadHistoryEntry(
    long Id,
    string FileName,
    string FilePath,
    string Url,
    string Host,
    DateTime DateTime);
