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

using System;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using XerahS.Common;

namespace XerahS.History
{
    public class HistoryManagerSQLite : HistoryManager, IDisposable
    {
        private SqliteConnection? connection;

        public HistoryManagerSQLite(string filePath) : base(filePath)
        {
            Connect(filePath);
            EnsureDatabase();
        }

        private void Connect(string filePath)
        {
            FileHelpers.CreateDirectoryFromFilePath(filePath);

            string connectionString = $"Data Source={filePath};Pooling=False";
            connection = new SqliteConnection(connectionString);
            connection.Open();

            // Enable WAL mode for better concurrent access (allows readers while writing)
            SetWalMode();
            SetBusyTimeout(5000);
        }

        private void SetWalMode()
        {
            using (SqliteCommand cmd = EnsureConnection().CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }
        }

        private void SetBusyTimeout(int milliseconds)
        {
            using (SqliteCommand cmd = EnsureConnection().CreateCommand())
            {
                cmd.CommandText = $"PRAGMA busy_timeout = {milliseconds};";
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureDatabase()
        {
            using (SqliteCommand cmd = EnsureConnection().CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS History (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName TEXT,
    FilePath TEXT,
    DateTime TEXT,
    Type TEXT,
    Host TEXT,
    URL TEXT,
    ThumbnailURL TEXT,
    DeletionURL TEXT,
    ShortenedURL TEXT,
    Tags TEXT
);
";
                cmd.ExecuteNonQuery();
            }
        }

        internal override List<HistoryItem> Load(string dbPath)
        {
            List<HistoryItem> items = new List<HistoryItem>();

            using (SqliteCommand cmd = new SqliteCommand("SELECT * FROM History;", EnsureConnection()))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    items.Add(ReadHistoryItem(reader));
                }
            }

            return items;
        }

        /// <summary>
        /// Loads the complete history for statistics. Read failures propagate
        /// so callers can distinguish an unavailable database from an empty history.
        /// </summary>
        public List<HistoryItem> GetStatisticsItems() => Load();

        public int GetTotalCount()
        {
            using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM History;", EnsureConnection()))
            {
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await Task.Run(GetTotalCount);
        }

        /// <summary>
        /// Retrieves a paged list of history items, ordered by DateTime DESC (newest first).
        /// </summary>
        public List<HistoryItem> GetHistoryItems(int offset, int limit)
        {
            List<HistoryItem> items = new List<HistoryItem>();

            using (SqliteCommand cmd = new SqliteCommand("SELECT * FROM History ORDER BY DateTime DESC LIMIT @Limit OFFSET @Offset;", EnsureConnection()))
            {
                cmd.Parameters.AddWithValue("@Limit", limit);
                cmd.Parameters.AddWithValue("@Offset", offset);

                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(ReadHistoryItem(reader));
                    }
                }
            }

            return items;
        }

        public async Task<List<HistoryItem>> GetHistoryItemsAsync(int offset, int limit)
        {
            return await Task.Run(() => GetHistoryItems(offset, limit));
        }

        public HistoryItem? GetHistoryItem(long id)
        {
            if (id <= 0)
            {
                return null;
            }

            using SqliteCommand cmd = new SqliteCommand("SELECT * FROM History WHERE Id = @Id LIMIT 1;", EnsureConnection());
            cmd.Parameters.AddWithValue("@Id", id);
            using SqliteDataReader reader = cmd.ExecuteReader();
            return reader.Read() ? ReadHistoryItem(reader) : null;
        }

        public (List<HistoryItem> Items, int TotalCount) SearchHistoryItems(string query, int offset, int limit)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return (GetHistoryItems(offset, limit), GetTotalCount());
            }

            HistoryOcrIndexStore.EnsureDatabase(EnsureConnection());
            string normalizedQuery = query.Trim();
            int clampedOffset = Math.Max(0, offset);
            int clampedLimit = Math.Max(1, limit);

            const string predicate = """
                instr(lower(coalesce(FileName, '')), lower(@Query)) > 0 OR
                instr(lower(coalesce(FilePath, '')), lower(@Query)) > 0 OR
                instr(lower(coalesce(URL, '')), lower(@Query)) > 0 OR
                instr(lower(coalesce(Host, '')), lower(@Query)) > 0 OR
                instr(lower(coalesce(Tags, '')), lower(@Query)) > 0 OR
                EXISTS (
                    SELECT 1
                    FROM HistoryOcrIndex AS Ocr
                    WHERE Ocr.HistoryItemId = History.Id
                      AND Ocr.Status = 'indexed'
                      AND Ocr.OcrText IS NOT NULL
                      AND instr(lower(Ocr.OcrText), lower(@Query)) > 0
                )
                """;

            int totalCount;
            using (SqliteCommand countCommand = EnsureConnection().CreateCommand())
            {
                countCommand.CommandText = $"SELECT COUNT(*) FROM History WHERE {predicate};";
                countCommand.Parameters.AddWithValue("@Query", normalizedQuery);
                object? result = countCommand.ExecuteScalar();
                totalCount = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }

            List<HistoryItem> items = new(Math.Min(totalCount, clampedLimit));
            using (SqliteCommand command = EnsureConnection().CreateCommand())
            {
                command.CommandText = $"SELECT * FROM History WHERE {predicate} ORDER BY DateTime DESC LIMIT @Limit OFFSET @Offset;";
                command.Parameters.AddWithValue("@Query", normalizedQuery);
                command.Parameters.AddWithValue("@Limit", clampedLimit);
                command.Parameters.AddWithValue("@Offset", clampedOffset);

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(ReadHistoryItem(reader));
                }
            }

            return (items, totalCount);
        }

        public bool ContainsFilePath(string filePath, int pageSize = 500)
        {
            return GetLatestByFilePath(filePath, pageSize) != null;
        }

        public HistoryItem? GetLatestByFilePath(string filePath, int pageSize = 500)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            IReadOnlyList<string>? normalizedPaths = GetComparablePaths(filePath);
            if (normalizedPaths == null)
            {
                return null;
            }

            int offset = 0;
            int clampedPageSize = Math.Max(1, pageSize);

            while (true)
            {
                List<HistoryItem> items = GetHistoryItems(offset, clampedPageSize);
                if (items.Count == 0)
                {
                    return null;
                }

                HistoryItem? match = items.FirstOrDefault(item => IsSamePath(item.FilePath, normalizedPaths));
                if (match != null)
                {
                    return match;
                }

                if (items.Count < clampedPageSize)
                {
                    return null;
                }

                offset += items.Count;
            }
        }

        /// <summary>
        /// Newest history row whose stored path uses <paramref name="sourceFileName"/> and whose URL is http(s).
        /// When <paramref name="notBefore"/> is set, an older row is ignored.
        /// The watch folder keeps this file name when it moves a screenshot into the screenshots folder.
        /// </summary>
        public HistoryItem? FindBySourceFileName(string sourceFileName, DateTime? notBefore = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFileName) || sourceFileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            {
                return null;
            }

            string escaped = EscapeLike(sourceFileName);
            using SqliteCommand command = new SqliteCommand(@"
SELECT * FROM History
WHERE (URL LIKE 'http://%' OR URL LIKE 'https://%')
AND (
    FilePath = @Name
    OR FilePath LIKE @Suffix ESCAPE '\'
    OR FilePath LIKE @SuffixWin ESCAPE '\'
)
ORDER BY DateTime DESC
LIMIT 1;", EnsureConnection());
            command.Parameters.AddWithValue("@Name", sourceFileName);
            command.Parameters.AddWithValue("@Suffix", "%/" + escaped);
            command.Parameters.AddWithValue("@SuffixWin", "%\\\\" + escaped);

            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            HistoryItem item = ReadHistoryItem(reader);
            if (notBefore.HasValue && item.DateTime < notBefore.Value)
            {
                return null;
            }

            return item;
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
        }

        protected override bool Append(string dbPath, IEnumerable<HistoryItem> historyItems)
        {
            if (connection == null)
            {
                DebugHelper.WriteLine("Cannot append history: connection is null");
                return false;
            }

            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (HistoryItem item in historyItems)
                    {
                        using (SqliteCommand cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = @"
INSERT INTO History
(FileName, FilePath, DateTime, Type, Host, URL, ThumbnailURL, DeletionURL, ShortenedURL, Tags)
VALUES (@FileName, @FilePath, @DateTime, @Type, @Host, @URL, @ThumbnailURL, @DeletionURL, @ShortenedURL, @Tags);
SELECT last_insert_rowid();";
                            cmd.Parameters.AddWithValue("@FileName", item.FileName ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@FilePath", item.FilePath ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@DateTime", item.DateTime.ToString("o") ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Type", item.Type ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Host", item.Host ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@URL", item.URL ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@ThumbnailURL", item.ThumbnailURL ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@DeletionURL", item.DeletionURL ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@ShortenedURL", item.ShortenedURL ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Tags", item.Tags != null ? JsonConvert.SerializeObject(item.Tags) : (object)DBNull.Value);
                            object? result = cmd.ExecuteScalar();
                            item.Id = result != null ? (long)result : 0;
                        }
                    }

                    transaction.Commit();

                    // Backup database after successful write
                    if (!Backup(FilePath))
                    {
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "Failed to append history items");
                    transaction.Rollback();
                    return false;
                }
            }
        }

        public void Edit(HistoryItem item)
        {
            using (SqliteTransaction transaction = EnsureConnection().BeginTransaction())
            using (SqliteCommand cmd = EnsureConnection().CreateCommand())
            {
                cmd.CommandText = @"
UPDATE History SET
FileName = @FileName,
FilePath = @FilePath,
DateTime = @DateTime,
Type = @Type,
Host = @Host,
URL = @URL,
ThumbnailURL = @ThumbnailURL,
DeletionURL = @DeletionURL,
ShortenedURL = @ShortenedURL,
Tags = @Tags
WHERE Id = @Id;";
                cmd.Parameters.AddWithValue("@FileName", item.FileName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@FilePath", item.FilePath ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateTime", item.DateTime.ToString("o") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Type", item.Type ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Host", item.Host ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@URL", item.URL ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ThumbnailURL", item.ThumbnailURL ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DeletionURL", item.DeletionURL ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ShortenedURL", item.ShortenedURL ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Tags", item.Tags != null ? JsonConvert.SerializeObject(item.Tags) : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Id", item.Id);
                cmd.ExecuteNonQuery();

                transaction?.Commit();
            }
        }

        public void Delete(params HistoryItem[] items)
        {
            if (items != null && items.Length > 0)
            {
                HistoryOcrIndexStore.EnsureDatabase(EnsureConnection());

                using (SqliteTransaction transaction = EnsureConnection().BeginTransaction())
                using (SqliteCommand cmd = EnsureConnection().CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM History WHERE Id = @Id;";

                    foreach (HistoryItem item in items)
                    {
                        SqliteParameter idParam = cmd.CreateParameter();
                        idParam.ParameterName = "@Id";
                        idParam.Value = item.Id;
                        cmd.Parameters.Add(idParam);
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }

                    cmd.CommandText = "DELETE FROM HistoryOcrIndex WHERE HistoryItemId = @Id;";

                    foreach (HistoryItem item in items)
                    {
                        SqliteParameter idParam = cmd.CreateParameter();
                        idParam.ParameterName = "@Id";
                        idParam.Value = item.Id;
                        cmd.Parameters.Add(idParam);
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }

                    transaction.Commit();
                }
            }
        }

        public void MigrateFromJSON(string jsonFilePath)
        {
            HistoryManagerJSON jsonManager = new HistoryManagerJSON(jsonFilePath);
            List<HistoryItem> items = jsonManager.Load(jsonFilePath);

            if (items.Count > 0)
            {
                Append(items);
            }
        }

        public void Dispose()
        {
            if (connection != null)
            {
                connection.Dispose();
                connection = null;
            }
        }

        private SqliteConnection EnsureConnection()
        {
            return connection ?? throw new InvalidOperationException("Database connection is not initialized.");
        }

        private static HistoryItem ReadHistoryItem(SqliteDataReader reader)
        {
            return new HistoryItem
            {
                Id = reader["Id"] == DBNull.Value ? 0L : Convert.ToInt64(reader["Id"]),
                FileName = reader["FileName"] == DBNull.Value ? string.Empty : reader["FileName"]?.ToString() ?? string.Empty,
                FilePath = reader["FilePath"]?.ToString() ?? string.Empty,
                DateTime = DateTime.TryParse(reader["DateTime"]?.ToString(), out DateTime dateTime) ? dateTime : DateTime.MinValue,
                Type = reader["Type"]?.ToString() ?? string.Empty,
                Host = reader["Host"]?.ToString() ?? string.Empty,
                URL = reader["URL"]?.ToString() ?? string.Empty,
                ThumbnailURL = reader["ThumbnailURL"]?.ToString() ?? string.Empty,
                DeletionURL = reader["DeletionURL"]?.ToString() ?? string.Empty,
                ShortenedURL = reader["ShortenedURL"]?.ToString() ?? string.Empty,
                Tags = JsonConvert.DeserializeObject<Dictionary<string, string?>>(reader["Tags"]?.ToString() ?? "{}") ?? new Dictionary<string, string?>()
            };
        }

        private static bool IsSamePath(string? left, IReadOnlyList<string> rightPaths)
        {
            IReadOnlyList<string>? leftPaths = GetComparablePaths(left);
            if (leftPaths == null)
            {
                return false;
            }

            StringComparison comparison = GetPathComparison();
            return leftPaths.Any(leftPath => rightPaths.Any(rightPath => string.Equals(leftPath, rightPath, comparison)));
        }

        private static IReadOnlyList<string>? GetComparablePaths(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }

            List<string> paths = new() { fullPath };
            string? resolvedPath = TryResolveLinkTarget(fullPath);
            if (!string.IsNullOrEmpty(resolvedPath) && !paths.Any(path => string.Equals(path, resolvedPath, GetPathComparison())))
            {
                paths.Add(resolvedPath);
            }

            return paths;
        }

        private static string? TryResolveLinkTarget(string path)
        {
            try
            {
                FileSystemInfo fileSystemInfo = File.Exists(path)
                    ? new FileInfo(path)
                    : new DirectoryInfo(path);
                FileSystemInfo? target = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true);
                return target == null ? null : Path.GetFullPath(target.FullName);
            }
            catch
            {
                return null;
            }
        }

        private static StringComparison GetPathComparison()
        {
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }
}
