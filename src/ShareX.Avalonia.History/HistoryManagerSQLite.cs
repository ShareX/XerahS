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

            string connectionString = $"Data Source={filePath}";
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
                    HistoryItem item = new HistoryItem()
                    {
                        Id = Convert.ToInt64(reader["Id"] ?? 0L),
                        FileName = reader["FileName"]?.ToString() ?? string.Empty,
                        FilePath = reader["FilePath"]?.ToString() ?? string.Empty,
                        DateTime = DateTime.TryParse(reader["DateTime"]?.ToString(), out var dt) ? dt : DateTime.MinValue,
                        Type = reader["Type"]?.ToString() ?? string.Empty,
                        Host = reader["Host"]?.ToString() ?? string.Empty,
                        URL = reader["URL"]?.ToString() ?? string.Empty,
                        ThumbnailURL = reader["ThumbnailURL"]?.ToString() ?? string.Empty,
                        DeletionURL = reader["DeletionURL"]?.ToString() ?? string.Empty,
                        ShortenedURL = reader["ShortenedURL"]?.ToString() ?? string.Empty,
                        Tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader["Tags"]?.ToString() ?? "{}") ?? new Dictionary<string, string>()
                    };

                    items.Add(item);
                }
            }

            return items;
        }

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
                        HistoryItem item = new HistoryItem()
                        {
                            Id = Convert.ToInt64(reader["Id"] ?? 0L),
                            FileName = reader["FileName"]?.ToString() ?? string.Empty,
                            FilePath = reader["FilePath"]?.ToString() ?? string.Empty,
                            DateTime = DateTime.TryParse(reader["DateTime"]?.ToString(), out var dt) ? dt : DateTime.MinValue,
                            Type = reader["Type"]?.ToString() ?? string.Empty,
                            Host = reader["Host"]?.ToString() ?? string.Empty,
                            URL = reader["URL"]?.ToString() ?? string.Empty,
                            ThumbnailURL = reader["ThumbnailURL"]?.ToString() ?? string.Empty,
                            DeletionURL = reader["DeletionURL"]?.ToString() ?? string.Empty,
                            ShortenedURL = reader["ShortenedURL"]?.ToString() ?? string.Empty,
                            Tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader["Tags"]?.ToString() ?? "{}") ?? new Dictionary<string, string>()
                        };

                        items.Add(item);
                    }
                }
            }

            return items;
        }

        public async Task<List<HistoryItem>> GetHistoryItemsAsync(int offset, int limit)
        {
            return await Task.Run(() => GetHistoryItems(offset, limit));
        }

        protected override bool Append(string dbPath, IEnumerable<HistoryItem> historyItems)
        {
            using (SqliteTransaction transaction = connection.BeginTransaction())
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
                        item.Id = (long)cmd.ExecuteScalar();
                    }
                }

                transaction.Commit();
            }

            // Backup database after successful write
            Backup(FilePath);

            return true;
        }

        public void Edit(HistoryItem item)
        {
            using (SqliteTransaction transaction = connection.BeginTransaction())
            using (SqliteCommand cmd = connection.CreateCommand())
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

                transaction.Commit();
            }
        }

        public void Delete(params HistoryItem[] items)
        {
            if (items != null && items.Length > 0)
            {
                using (SqliteTransaction transaction = EnsureConnection().BeginTransaction())
                using (SqliteCommand cmd = EnsureConnection().CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM History WHERE Id = @Id;";
                    SqliteParameter idParam = cmd.CreateParameter();
                    idParam.ParameterName = "@Id";
                    cmd.Parameters.Add(idParam);

                    foreach (HistoryItem item in items)
                    {
                        idParam.Value = item.Id;
                        cmd.ExecuteNonQuery();
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
    }
}

