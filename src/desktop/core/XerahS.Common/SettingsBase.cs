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
using Newtonsoft.Json.Converters;
using XerahS.Common.Utilities;
using System.ComponentModel;
using System.IO.Compression;
using System.Text;

namespace XerahS.Common
{
    public abstract class SettingsBase<T> where T : SettingsBase<T>, new()
    {
        public delegate void SettingsSavedEventHandler(T settings, string filePath, bool result);
        public event SettingsSavedEventHandler? SettingsSaved;

        public delegate void SettingsSaveFailedEventHandler(Exception e);
        public event SettingsSaveFailedEventHandler? SettingsSaveFailed;

        /// <summary>
        /// Phase tag for <see cref="SettingsBackupFailed"/>: "create" when
        /// <c>CreateBackupZip</c> fails, "prune" when the outer
        /// <c>PruneOldBackups</c> scan fails, "pruneFolder" when an individual
        /// month folder cannot be deleted during pruning.
        /// </summary>
        public delegate void SettingsBackupFailedEventHandler(T settings, string phase, string? backupFolder, Exception e);
        public event SettingsBackupFailedEventHandler? SettingsBackupFailed;

        [Browsable(false), JsonIgnore]
        public string? FilePath { get; protected set; }

        [Browsable(false)]
        public string? ApplicationVersion { get; set; }

        [Browsable(false), JsonIgnore]
        public bool IsFirstTimeRun { get; private set; }

        [Browsable(false), JsonIgnore]
        public bool IsUpgrade { get; private set; }

        [Browsable(false), JsonIgnore]
        public string? BackupFolder { get; set; }

        [Browsable(false), JsonIgnore]
        public bool CreateBackup { get; set; }

        [Browsable(false), JsonIgnore]
        public bool CreateWeeklyBackup { get; set; }

        [Browsable(false), JsonIgnore]
        public int BackupRetentionDays { get; set; } = 90;

        [Browsable(false), JsonIgnore]
        public bool SupportDPAPIEncryption { get; set; }

        public bool IsUpgradeFrom(string version)
        {
            return IsUpgrade && CompareVersion(ApplicationVersion, version) <= 0;
        }

        private static int CompareVersion(string? currentVersion, string targetVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                return string.IsNullOrWhiteSpace(targetVersion) ? 0 : -1;
            }

            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                return 1;
            }

            string[] currentParts = currentVersion.Split('.');
            string[] targetParts = targetVersion.Split('.');
            int count = Math.Max(currentParts.Length, targetParts.Length);

            for (int i = 0; i < count; i++)
            {
                long currentPart = i < currentParts.Length ? ParseVersionPart(currentParts[i]) : 0;
                long targetPart = i < targetParts.Length ? ParseVersionPart(targetParts[i]) : 0;

                int result = currentPart.CompareTo(targetPart);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        private static long ParseVersionPart(string part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return 0;
            }

            int length = 0;
            while (length < part.Length && char.IsDigit(part[length]))
            {
                length++;
            }

            return length > 0 && long.TryParse(part.AsSpan(0, length), out long value) ? value : 0;
        }

        protected virtual void OnSettingsSaved(string filePath, bool result)
        {
            SettingsSaved?.Invoke((T)this, filePath, result);
        }

        protected virtual void OnSettingsSaveFailed(Exception e)
        {
            SettingsSaveFailed?.Invoke(e);
        }

        protected virtual void OnSettingsBackupFailed(string phase, Exception e)
        {
            SettingsBackupFailed?.Invoke((T)this, phase, BackupFolder, e);
        }

        public bool Save(string filePath)
        {
            FilePath = filePath;
            ApplicationVersion = SystemInfo.GetApplicationVersion();

            bool result = SaveInternal(FilePath);

            OnSettingsSaved(FilePath, result);

            return result;
        }

        public bool Save()
        {
            if (FilePath == null) return false;
            return Save(FilePath);
        }

        public Task<bool> SaveAsync(string filePath)
        {
            return Task.Run(() => Save(filePath));
        }

        public Task<bool> SaveAsync()
        {
            if (FilePath == null)
            {
                return Task.FromResult(false);
            }

            return SaveAsync(FilePath);
        }

        public MemoryStream SaveToMemoryStream(bool supportDPAPIEncryption = false)
        {
            ApplicationVersion = SystemInfo.GetApplicationVersion();

            MemoryStream ms = new MemoryStream();
            SaveToStream(ms, supportDPAPIEncryption, true);
            ms.Position = 0;
            return ms;
        }

        private bool SaveInternal(string filePath)
        {
            string typeName = GetType().Name;
            System.Diagnostics.Debug.WriteLine($"{typeName} save started: {filePath}");

            bool isSuccess = false;

            try
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    lock (this)
                    {
                        string? directory = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        string tempFilePath = filePath + ".temp";

                        using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                        {
                            SaveToStream(fileStream, SupportDPAPIEncryption);
                        }

                        // Basic JSON verification could go here

                        if (File.Exists(filePath))
                        {
                            if ((CreateBackup || CreateWeeklyBackup) && !string.IsNullOrEmpty(BackupFolder))
                            {
                                CreateBackupZip(filePath);
                            }

                            // .NET Standard 2.0 / .NET Core doesn't verify File.Replace across checks, but standard File.Replace is available in newer .NET
                            // We'll use a manual move approach if needed or File.Replace
                            try
                            {
                                File.Move(tempFilePath, filePath, true);
                            }
                            catch (Exception)
                            {
                                if (File.Exists(tempFilePath))
                                {
                                    // Fallback
                                    File.Delete(filePath);
                                    File.Move(tempFilePath, filePath);
                                }
                            }
                        }
                        else
                        {
                            File.Move(tempFilePath, filePath);
                        }

                        isSuccess = true;
                    }
                }

                // Prune old backup folders outside the lock to avoid blocking
                // concurrent save callers on backup cleanup I/O.
                if (isSuccess)
                {
                    PruneOldBackups();
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                OnSettingsSaveFailed(e);
            }
            finally
            {
                string status = isSuccess ? "successful" : "failed";
                System.Diagnostics.Debug.WriteLine($"{typeName} save {status}: {filePath}");
            }

            return isSuccess;
        }

        private static readonly Lock BackupZipGate = new();
        private const int SettingsLoadMaxAttempts = 3;
        private const int BackupArchiveMaxExamine = 8;

        private void CreateBackupZip(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(BackupFolder) || !File.Exists(filePath))
                {
                    return;
                }

                // Create yyyy-MM subfolder
                string monthFolder = Path.Combine(BackupFolder, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(monthFolder))
                {
                    Directory.CreateDirectory(monthFolder);
                }

                // Get machine name for machine-specific backups
                string machineName = Environment.MachineName;

                // Get the directory containing the settings file
                string? settingsDirectory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(settingsDirectory))
                {
                    return;
                }

                List<string> zipFileNames = new List<string>();

                if (CreateBackup)
                {
                    // Create zip file with date stamp and machine name: yyyy-MM-dd-MACHINENAME format
                    zipFileNames.Add($"backup-{DateTime.Now:yyyy-MM-dd}-{machineName}.zip");
                }

                if (CreateWeeklyBackup)
                {
                    // Create zip file with year, week number, and machine name: yyyy-Www-MACHINENAME format
                    zipFileNames.Add($"backup-{DateTime.Now.Year}-W{FileHelpers.WeekOfYear(DateTime.Now):00}-{machineName}.zip");
                }

                lock (BackupZipGate)
                {
                    foreach (string zipFileName in zipFileNames.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        string zipFilePath = Path.Combine(monthFolder, zipFileName);
                        string tempZipPath = zipFilePath + ".tmp";

                        try
                        {
                            if (File.Exists(tempZipPath))
                            {
                                File.Delete(tempZipPath);
                            }

                            using (ZipArchive archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
                            {
                                foreach (string jsonFile in Directory.GetFiles(settingsDirectory, "*.json"))
                                {
                                    string entryName = Path.GetFileName(jsonFile);
                                    using FileStream fileStream = new FileStream(
                                        jsonFile,
                                        FileMode.Open,
                                        FileAccess.Read,
                                        FileShare.ReadWrite);
                                    ZipArchiveEntry entry = archive.CreateEntry(entryName);
                                    using Stream entryStream = entry.Open();
                                    fileStream.CopyTo(entryStream);
                                }
                            }

                            File.Move(tempZipPath, zipFilePath, overwrite: true);
                            System.Diagnostics.Debug.WriteLine($"Backup created: {zipFilePath} with all JSON files");
                        }
                        finally
                        {
                            if (File.Exists(tempZipPath))
                            {
                                try
                                {
                                    File.Delete(tempZipPath);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create backup: {e}");
                OnSettingsBackupFailed("create", e);
            }
        }

        /// <summary>
        /// Removes backup month folders older than <see cref="BackupRetentionDays"/> days.
        /// Does nothing when BackupFolder is not set or BackupRetentionDays is zero or negative.
        /// </summary>
        internal void PruneOldBackups()
        {
            if (string.IsNullOrEmpty(BackupFolder) || !Directory.Exists(BackupFolder) || BackupRetentionDays <= 0)
            {
                return;
            }

            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-BackupRetentionDays);

                foreach (string monthDir in Directory.GetDirectories(BackupFolder))
                {
                    string dirName = Path.GetFileName(monthDir);

                    // Month folders are named yyyy-MM; parse the first-of-month date.
                    if (DateTime.TryParseExact(dirName, "yyyy-MM", null,
                        System.Globalization.DateTimeStyles.AssumeLocal | System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                        out DateTime dirDate))
                    {
                        // Delete only when the entire month is older than the cutoff.
                        if (dirDate.AddMonths(1) < cutoff)
                        {
                            try
                            {
                                Directory.Delete(monthDir, recursive: true);
                                System.Diagnostics.Debug.WriteLine($"Pruned old backup folder: {monthDir}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to prune backup folder '{monthDir}': {ex.Message}");
                                OnSettingsBackupFailed("pruneFolder", ex);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to prune old backups: {e}");
                OnSettingsBackupFailed("prune", e);
            }
        }

        private void SaveToStream(Stream stream, bool supportDPAPIEncryption = false, bool leaveOpen = false)
        {
            using (StreamWriter streamWriter = new StreamWriter(stream, new UTF8Encoding(false, true), 1024, leaveOpen))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
            {
                JsonSerializer serializer = new JsonSerializer();

                // TODO: DPAPI resolver if needed
                // if (supportDPAPIEncryption) ...

                serializer.Converters.Add(new SafeStringEnumConverter());
                serializer.Converters.Add(new XerahS.Common.Converters.SkColorJsonConverter());
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(jsonWriter, this);
                jsonWriter.Flush();
            }
        }

        public static T Load(string filePath, string? backupFolder = null, bool fallbackSupport = true)
        {
            List<string> fallbackFilePaths = new List<string>();

            if (fallbackSupport && !string.IsNullOrEmpty(filePath))
            {
                string tempFilePath = filePath + ".temp";
                fallbackFilePaths.Add(tempFilePath);

                if (!string.IsNullOrEmpty(backupFolder) && Directory.Exists(backupFolder))
                {
                    string fileName = Path.GetFileName(filePath);
                    string backupFilePath = Path.Combine(backupFolder, fileName);
                    fallbackFilePaths.Add(backupFilePath);

                    // Weekly backups retrieval logic...
                }
            }

            T setting = LoadInternal(filePath, fallbackFilePaths, backupFolder, Path.GetFileName(filePath));

            if (setting != null)
            {
                string previousApplicationVersion = setting.ApplicationVersion ?? string.Empty;
                setting.FilePath = filePath;
                setting.IsFirstTimeRun = string.IsNullOrEmpty(previousApplicationVersion);
                setting.IsUpgrade = !setting.IsFirstTimeRun && CompareVersion(previousApplicationVersion, SystemInfo.GetApplicationVersion()) < 0;
                setting.BackupFolder = backupFolder;
            }

            return setting!;
        }

        /// <summary>
        /// Marks first-run state as completed by stamping <see cref="ApplicationVersion"/>
        /// and flipping <see cref="IsFirstTimeRun"/> to false.
        /// </summary>
        public void MarkFirstTimeRunCompleted(bool persist = true)
        {
            if (!IsFirstTimeRun)
                return;

            ApplicationVersion = SystemInfo.GetApplicationVersion();
            IsFirstTimeRun = false;

            if (persist)
                _ = Save();
        }

        private static T LoadInternal(
            string filePath,
            List<string>? fallbackFilePaths = null,
            string? backupFolder = null,
            string? originalFileName = null)
        {
            string typeName = typeof(T).Name;

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"{typeName} load started: {filePath}");
                DebugHelper.WriteLine($"[SettingsBase] {typeName} load started: {filePath}");

                // DEBUG: Log file details
                /*
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    DebugHelper.WriteLine($"[SettingsBase] File exists: {fileInfo.Exists}, Size: {fileInfo.Length} bytes, LastWrite: {fileInfo.LastWriteTime}");
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"[SettingsBase] Failed to get file info: {ex.Message}");
                }
                */

                Exception? lastError = null;

                for (int attempt = 1; attempt <= SettingsLoadMaxAttempts; attempt++)
                {
                    try
                    {
                        using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (fileStream.Length > 0)
                            {
                                using MemoryStream buffered = new MemoryStream();
                                fileStream.CopyTo(buffered);
                                buffered.Position = 0;

                                T settings = DeserializeFromStream(buffered, typeName);

                                System.Diagnostics.Debug.WriteLine($"{typeName} load finished: {filePath}");
                                return settings;
                            }

                            DebugHelper.WriteLine($"[SettingsBase] WARNING: File is empty (0 bytes): {filePath}");
                            lastError = null;
                            break;
                        }
                    }
                    catch (IOException ioEx) when (attempt < SettingsLoadMaxAttempts)
                    {
                        lastError = ioEx;
                        DebugHelper.WriteLine($"[SettingsBase] {typeName} load retry {attempt} after IO error: {ioEx.Message}");
                        Thread.Sleep(50 * attempt);
                    }
                    catch (Exception e)
                    {
                        lastError = e;
                        break;
                    }
                }

                if (lastError != null)
                {
                    System.Diagnostics.Debug.WriteLine($"{typeName} load failed: {filePath}. Error: {lastError}");
                    DebugHelper.WriteLine($"[SettingsBase] {typeName} load FAILED: {filePath}");
                    DebugHelper.WriteLine($"[SettingsBase] Exception type: {lastError.GetType().Name}");
                    DebugHelper.WriteLine($"[SettingsBase] Exception message: {lastError.Message}");
                    DebugHelper.WriteLine($"[SettingsBase] Stack trace: {lastError.StackTrace}");
                    if (lastError.InnerException != null)
                    {
                        DebugHelper.WriteLine($"[SettingsBase] Inner exception: {lastError.InnerException.Message}");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"{typeName} file does not exist: {filePath}");
                DebugHelper.WriteLine($"[SettingsBase] {typeName} file does not exist: {filePath}");
            }

            if (fallbackFilePaths != null && fallbackFilePaths.Count > 0)
            {
                filePath = fallbackFilePaths[0];
                fallbackFilePaths.RemoveAt(0);
                return LoadInternal(filePath, fallbackFilePaths, backupFolder, originalFileName);
            }

            T? backupSettings = LoadFromBackupArchive(backupFolder, originalFileName);
            if (backupSettings != null)
            {
                return backupSettings;
            }

            System.Diagnostics.Debug.WriteLine($"Loading new {typeName} instance.");

            return new T();
        }

        private static T DeserializeFromStream(Stream stream, string typeName)
        {
            using (StreamReader streamReader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
            {
                JsonSerializer serializer = new JsonSerializer();
                // serializer.ContractResolver = ...
                serializer.Converters.Add(new SafeStringEnumConverter());
                serializer.Converters.Add(new XerahS.Common.Converters.SkColorJsonConverter());
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.DateTimeZoneHandling = DateTimeZoneHandling.Local;
                serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
                serializer.Error += (sender, args) =>
                {
                    Exception error = args.ErrorContext.Error;
                    DebugHelper.WriteLine($"[SettingsBase] JSON Error: {error.Message} at path: {args.ErrorContext.Path}");

                    if (error is IOException or UnauthorizedAccessException)
                    {
                        return;
                    }

                    args.ErrorContext.Handled = true;
                };

                return serializer.Deserialize<T>(jsonReader) ?? throw new Exception($"{typeName} object is null.");
            }
        }

        private static T? LoadFromBackupArchive(string? backupFolder, string? fileName)
        {
            if (string.IsNullOrEmpty(backupFolder) || string.IsNullOrEmpty(fileName) || !Directory.Exists(backupFolder))
            {
                return null;
            }

            string typeName = typeof(T).Name;

            int examined = 0;
            foreach (string zipFilePath in Directory.EnumerateFiles(backupFolder, "*.zip", SearchOption.AllDirectories)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (examined >= BackupArchiveMaxExamine)
                {
                    DebugHelper.WriteLine($"[SettingsBase] Stopping {typeName} backup archive scan after {examined} archives.");
                    break;
                }

                examined++;

                try
                {
                    using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
                    ZipArchiveEntry? entry = archive.GetEntry(fileName);

                    if (entry == null || entry.Length == 0)
                    {
                        continue;
                    }

                    using Stream entryStream = entry.Open();
                    T settings = DeserializeFromStream(entryStream, typeName);
                    DebugHelper.WriteLine($"[SettingsBase] {typeName} loaded from backup archive: {zipFilePath}");
                    return settings;
                }
                catch (Exception e)
                {
                    DebugHelper.WriteLine($"[SettingsBase] Failed to load {typeName} backup archive '{zipFilePath}': {e.Message}");
                }
            }

            return null;
        }

        private static void Serializer_Error(object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            // Handle missing enum values
            if (e.ErrorContext.Error.Message.StartsWith("Error converting value"))
            {
                e.ErrorContext.Handled = true;
            }
        }
    }
}
