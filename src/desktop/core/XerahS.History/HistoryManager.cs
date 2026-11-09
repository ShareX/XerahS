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

namespace XerahS.History
{
    public abstract class HistoryManager
    {
        public string FilePath { get; private set; }
        public string BackupFolder { get; set; } = null!;
        public bool CreateBackup { get; set; }
        public bool CreateWeeklyBackup { get; set; }

        /// <summary>
        /// Gets a user-friendly description of the most recent history backup failure, or null if the last backup
        /// attempt succeeded (or no backup was configured). Cleared at the start of every <see cref="Backup"/> call
        /// so a subsequent successful backup resets the diagnostic. This is intentionally separate from the
        /// <see cref="HistoryManager"/> boolean return contract: the data write itself can succeed even when the
        /// backup step fails, and the caller needs a way to surface that distinction to the user.
        /// </summary>
        public string? LastBackupFailureReason { get; private set; }

        /// <summary>
        /// Raised (on the writing thread) after items were appended to any history store, so open history views can refresh.
        /// </summary>
        public static event Action? HistoryAppended;

        public HistoryManager(string filePath)
        {
            FilePath = filePath;
        }

        public List<HistoryItem> GetHistoryItems()
        {
            try
            {
                return Load();
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }

            return new List<HistoryItem>();
        }

        public async Task<List<HistoryItem>> GetHistoryItemsAsync()
        {
            return await Task.Run(GetHistoryItems);
        }

        public bool AppendHistoryItem(HistoryItem historyItem)
        {
            return AppendHistoryItems(new HistoryItem[] { historyItem });
        }

        public bool AppendHistoryItems(IEnumerable<HistoryItem> historyItems)
        {
            try
            {
                List<HistoryItem> validItems = historyItems.Where(IsValidHistoryItem).ToList();
                bool appended = Append(validItems);

                if (appended && validItems.Count > 0)
                {
                    try
                    {
                        HistoryAppended?.Invoke();
                    }
                    catch (Exception e)
                    {
                        DebugHelper.WriteException(e);
                    }
                }

                return appended;
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }

            return false;
        }

        private bool IsValidHistoryItem(HistoryItem historyItem)
        {
            return historyItem != null && !string.IsNullOrEmpty(historyItem.FileName) && historyItem.DateTime != DateTime.MinValue &&
                (!string.IsNullOrEmpty(historyItem.URL) || !string.IsNullOrEmpty(historyItem.FilePath));
        }

        internal List<HistoryItem> Load()
        {
            return Load(FilePath);
        }

        internal abstract List<HistoryItem> Load(string filePath);

        protected bool Append(IEnumerable<HistoryItem> historyItems)
        {
            return Append(FilePath, historyItems);
        }

        protected abstract bool Append(string filePath, IEnumerable<HistoryItem> historyItems);

        protected bool Backup(string filePath)
        {
            if (string.IsNullOrEmpty(BackupFolder))
            {
                LastBackupFailureReason = null;
                return true;
            }

            // Clear the previous diagnostic at the start of each backup attempt. A subsequent successful
            // backup step keeps the value null, and a failed step populates it with a user-friendly
            // description the caller can surface to the user.
            LastBackupFailureReason = null;

            if (CreateBackup)
            {
                string? backupPath = FileHelpers.BackupFileZip(filePath, BackupFolder);

                if (backupPath == null)
                {
                    string reason = $"Could not create zipped history backup in '{BackupFolder}'. The history file itself was updated, but a backup of the previous state was not written. Check that the folder exists, is writable, and has enough free space.";
                    DebugHelper.WriteLine($"History backup failed: {reason}");
                    LastBackupFailureReason = reason;
                    return false;
                }
            }

            if (CreateWeeklyBackup)
            {
                string? backupPath = FileHelpers.BackupFileWeekly(filePath, BackupFolder);

                if (backupPath == null && !WeeklyBackupAlreadyExists(filePath))
                {
                    string reason = $"Could not create weekly history backup in '{BackupFolder}'. The history file itself was updated, but a backup of the previous state was not written. Check that the folder exists, is writable, and has enough free space.";
                    DebugHelper.WriteLine($"History backup failed: {reason}");
                    LastBackupFailureReason = reason;
                    return false;
                }
            }

            return true;
        }

        private bool WeeklyBackupAlreadyExists(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                DateTime dateTime = DateTime.Now;
                string extension = Path.GetExtension(filePath);
                string backupFileName = $"{fileName}-{dateTime:yyyy-MM}-W{FileHelpers.WeekOfYear(dateTime):00}{extension}";
                string backupFilePath = Path.Combine(BackupFolder, backupFileName);

                return File.Exists(backupFilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                return false;
            }
        }

        public void Test(int itemCount)
        {
            Test(FilePath, itemCount);
        }

        public void Test(string filePath, int itemCount)
        {
            HistoryItem historyItem = new HistoryItem()
            {
                FileName = "Example.png",
                FilePath = @"C:\ShareX\Screenshots\Example.png",
                DateTime = DateTime.Now,
                Type = "Image",
                Host = "Imgur",
                URL = "https://example.com/Example.png",
                ThumbnailURL = "https://example.com/Example.png",
                DeletionURL = "https://example.com/Example.png",
                ShortenedURL = "https://example.com/Example.png"
            };

            HistoryItem[] historyItems = new HistoryItem[itemCount];
            for (int i = 0; i < itemCount; i++)
            {
                historyItems[i] = historyItem;
            }

            Thread.Sleep(1000);

            DebugTimer saveTimer = new DebugTimer($"Saved {itemCount} items");
            Append(filePath, historyItems);
            saveTimer.WriteElapsedMilliseconds();

            Thread.Sleep(1000);

            DebugTimer loadTimer = new DebugTimer($"Loaded {itemCount} items");
            Load(filePath);
            loadTimer.WriteElapsedMilliseconds();
        }
    }
}

