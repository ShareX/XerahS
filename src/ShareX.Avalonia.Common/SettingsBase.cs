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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
        public bool SupportDPAPIEncryption { get; set; }

        public bool IsUpgradeFrom(string version)
        {
            // TODO: Implement Helpers.CompareVersion logic if needed
            return IsUpgrade && String.Compare(ApplicationVersion, version, StringComparison.Ordinal) <= 0;
        }

        protected virtual void OnSettingsSaved(string filePath, bool result)
        {
            SettingsSaved?.Invoke((T)this, filePath, result);
        }

        protected virtual void OnSettingsSaveFailed(Exception e)
        {
            SettingsSaveFailed?.Invoke(e);
        }

        public bool Save(string filePath)
        {
            FilePath = filePath;
            // ApplicationVersion = Helpers.GetApplicationVersion(); // TODO: Implement version retrieval

            bool result = SaveInternal(FilePath);

            OnSettingsSaved(FilePath, result);

            return result;
        }

        public bool Save()
        {
            if (FilePath == null) return false;
            return Save(FilePath);
        }

        public void SaveAsync(string filePath)
        {
            Task.Run(() => Save(filePath));
        }

        public void SaveAsync()
        {
            if (FilePath != null)
            {
                SaveAsync(FilePath);
            }
        }

        public MemoryStream SaveToMemoryStream(bool supportDPAPIEncryption = false)
        {
            // ApplicationVersion = Helpers.GetApplicationVersion();

            MemoryStream ms = new MemoryStream();
            SaveToStream(ms, supportDPAPIEncryption, true);
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
                            if (CreateBackup && !string.IsNullOrEmpty(BackupFolder))
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

                        // TODO: Weekly backup logic

                        isSuccess = true;
                    }
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

        private void CreateBackupZip(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(BackupFolder) || !File.Exists(filePath))
                {
                    return;
                }

                // Create yyyy-MM subfolder
                // Create yyyy-MM subfolder
                string monthFolder = Path.Combine(BackupFolder, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(monthFolder))
                {
                    Directory.CreateDirectory(monthFolder);
                }

                string zipFileName;

                if (CreateWeeklyBackup)
                {
                    // Create zip file with year and week number: yyyy-Www format
                    zipFileName = $"backup-{DateTime.Now.Year}-W{FileHelpers.WeekOfYear(DateTime.Now):00}.zip";
                }
                else
                {
                    // Create zip file with date stamp: yyyy-MM-dd format
                    zipFileName = $"backup-{DateTime.Now:yyyy-MM-dd}.zip";
                }

                string zipFilePath = Path.Combine(monthFolder, zipFileName);

                // If a backup for today already exists, delete it (we're updating with latest)
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                // Create zip file containing the JSON file
                using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    string fileName = Path.GetFileName(filePath);
                    using (var fileStream = File.OpenRead(filePath))
                    {
                        var entry = archive.CreateEntry(fileName);
                        using (var entryStream = entry.Open())
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Backup created: {zipFilePath}");
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create backup: {e}");
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

                serializer.Converters.Add(new StringEnumConverter());
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

            T setting = LoadInternal(filePath, fallbackFilePaths);

            if (setting != null)
            {
                setting.FilePath = filePath;
                setting.IsFirstTimeRun = string.IsNullOrEmpty(setting.ApplicationVersion);
                // setting.IsUpgrade = ... 
                setting.BackupFolder = backupFolder;
            }

            return setting;
        }

        private static T LoadInternal(string filePath, List<string>? fallbackFilePaths = null)
        {
            string typeName = typeof(T).Name;

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"{typeName} load started: {filePath}");

                try
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (fileStream.Length > 0)
                        {
                            T? settings;

                            using (StreamReader streamReader = new StreamReader(fileStream))
                            using (JsonTextReader jsonReader = new JsonTextReader(streamReader))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                // serializer.ContractResolver = ...
                                serializer.Converters.Add(new StringEnumConverter());
                                serializer.DateTimeZoneHandling = DateTimeZoneHandling.Local;
                                serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
                                serializer.Error += Serializer_Error;
                                settings = serializer.Deserialize<T>(jsonReader);
                            }

                            if (settings == null)
                            {
                                throw new Exception($"{typeName} object is null.");
                            }

                            System.Diagnostics.Debug.WriteLine($"{typeName} load finished: {filePath}");

                            return settings;
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"{typeName} load failed: {filePath}. Error: {e}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"{typeName} file does not exist: {filePath}");
            }

            if (fallbackFilePaths != null && fallbackFilePaths.Count > 0)
            {
                filePath = fallbackFilePaths[0];
                fallbackFilePaths.RemoveAt(0);
                return LoadInternal(filePath, fallbackFilePaths);
            }

            System.Diagnostics.Debug.WriteLine($"Loading new {typeName} instance.");

            return new T();
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
