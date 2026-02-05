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

using System.Linq;
using XerahS.Common;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.Uploaders;

// ReSharper disable MemberCanBePrivate.Global

namespace XerahS.Core
{
    /// <summary>
    /// Manages loading and saving of all application settings.
    /// Provides centralized access to all configuration objects.
    /// </summary>
    public static class SettingsManager
    {
        public static readonly string AppName = AppResources.AppName;

        #region Constants

        public const string ApplicationConfigFileName = "ApplicationConfig.json";
        public const string UploadersConfigFileNamePrefix = "UploadersConfig";
        public const string UploadersConfigFileNameExtension = "json";
        public const string UploadersConfigFileName = UploadersConfigFileNamePrefix + "." + UploadersConfigFileNameExtension;
        public const string WorkflowsConfigFileNamePrefix = "WorkflowsConfig";
        public const string WorkflowsConfigFileNameExtension = "json";
        public const string WorkflowsConfigFileName = WorkflowsConfigFileNamePrefix + "." + WorkflowsConfigFileNameExtension;
        public const string SecretsStoreFileName = "SecretsStore.json";

        #endregion

        #region Static Properties

        /// <summary>
        /// Root folder for user settings. Delegates to PathsManager.
        /// </summary>
        public static string PersonalFolder
        {
            get => XerahS.Common.PathsManager.PersonalFolder;
            set => XerahS.Common.PathsManager.PersonalFolder = value;
        }

        /// <summary>
        /// Event raised when settings are saved
        /// </summary>
        public static event EventHandler? SettingsChanged;

        /// <summary>
        /// Raises the SettingsChanged event
        /// </summary>
        public static void RaiseSettingsChanged()
        {
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Folder containing settings files
        /// </summary>
        public static string SettingsFolder => XerahS.Common.PathsManager.SettingsFolder;

        /// <summary>
        /// History folder path
        /// </summary>
        public static string HistoryFolder => XerahS.Common.PathsManager.HistoryFolder;

        /// <summary>
        /// Screenshots folder path
        /// </summary>
        public static string ScreenshotsFolder => XerahS.Common.PathsManager.ScreenshotsFolder;

        /// <summary>
        /// Screencasts folder path
        /// </summary>
        public static string ScreencastsFolder => XerahS.Common.PathsManager.ScreencastsFolder;

        /// <summary>
        /// Frame dumps folder path for screen recording debug
        /// </summary>
        public static string FrameDumpsFolder => XerahS.Common.PathsManager.FrameDumpsFolder;

        /// <summary>
        /// Backup folder path
        /// </summary>
        public static string BackupFolder => XerahS.Common.PathsManager.BackupFolder;

        /// <summary>
        /// History backup folder path
        /// </summary>
        public static string HistoryBackupFolder => XerahS.Common.PathsManager.HistoryBackupFolder;

        /// <summary>
        /// Application config file path
        /// </summary>
        public static string ApplicationConfigFilePath => Path.Combine(SettingsFolder, ApplicationConfigFileName);

        /// <summary>
        /// Uploaders config file path
        /// </summary>
        public static string UploadersConfigFilePath
        {
            get
            {
                string uploadersConfigFolder = SettingsFolder;

                if (Settings != null && !string.IsNullOrEmpty(Settings.CustomUploadersConfigPath))
                {
                    uploadersConfigFolder = FileHelpers.ExpandFolderVariables(Settings.CustomUploadersConfigPath);
                }

                string uploadersConfigFileName = GetUploadersConfigFileName(uploadersConfigFolder);

                return Path.Combine(uploadersConfigFolder, uploadersConfigFileName);
            }
        }

        /// <summary>
        /// Workflows config file path
        /// </summary>
        public static string WorkflowsConfigFilePath
        {
            get
            {
                string workflowsConfigFolder = SettingsFolder;

                if (Settings != null && !string.IsNullOrEmpty(Settings.CustomWorkflowsConfigPath))
                {
                    workflowsConfigFolder = FileHelpers.ExpandFolderVariables(Settings.CustomWorkflowsConfigPath);
                }

                string workflowsConfigFileName = GetWorkflowsConfigFileName(workflowsConfigFolder);

                return Path.Combine(workflowsConfigFolder, workflowsConfigFileName);
            }
        }

        /// <summary>
        /// Secrets store file path
        /// </summary>
        public static string SecretsStoreFilePath => Path.Combine(SettingsFolder, SecretsStoreFileName);

        /// <summary>
        /// Main application settings
        /// </summary>
        public static ApplicationConfig Settings { get; private set; } = new ApplicationConfig();

        /// <summary>
        /// Uploaders configuration
        /// </summary>
        public static UploadersConfig UploadersConfig { get; set; } = new UploadersConfig();

        /// <summary>
        /// Workflows configuration
        /// </summary>
        public static WorkflowsConfig WorkflowsConfig { get; set; } = new WorkflowsConfig();

        /// <summary>
        /// Get the first workflow matching the specified WorkflowType.
        /// Returns null if no workflow exists for that type.
        /// Use this instead of GetOrCreateWorkflowTaskSettings when you need workflow-specific settings.
        /// </summary>
        public static WorkflowSettings? GetFirstWorkflow(WorkflowType workflowType)
        {
            return WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Job == workflowType);
        }

        /// <summary>
        /// Get the first workflow matching the specified WorkflowType, or create a default workflow if none exists.
        /// Use this method when you need guaranteed non-null workflow for a hotkey type.
        /// </summary>
        public static WorkflowSettings GetFirstWorkflowOrDefault(WorkflowType workflowType)
        {
            return GetFirstWorkflow(workflowType) ?? new WorkflowSettings(workflowType, new HotkeyInfo());
        }

        public static WorkflowSettings? GetWorkflowById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Id == id);
        }

        public static TaskSettings GetWorkflowTaskSettings(string workflowId)
        {
            var workflow = GetWorkflowById(workflowId);
            return workflow?.TaskSettings ?? DefaultTaskSettings;
        }

        /// <summary>
        /// Get a default TaskSettings instance.
        /// Use this for fallback/global settings instead of looking up by WorkflowType.
        /// </summary>
        public static TaskSettings DefaultTaskSettings { get; private set; } = new TaskSettings { Job = WorkflowType.None };

        /// <summary>
        /// Recent task manager
        /// </summary>
        public static RecentTaskManager RecentTaskManager { get; } = new RecentTaskManager();

        #endregion

        #region Load Methods

        public static void LoadInitialSettings()
        {
            EnsureDirectoriesExist();
            LoadApplicationConfig();
            LoadUploadersConfig();
            LoadWorkflowsConfig();
            InitializeRecentTasks();
            XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
        }

        /// <summary>
        /// Load application config from file using SettingsBase mechanism
        /// </summary>
        public static void LoadApplicationConfig(bool fallbackSupport = true)
        {
            var path = ApplicationConfigFilePath;
            DebugHelper.WriteLine($"ApplicationConfig load started: {path}");
            Settings = ApplicationConfig.Load(path, BackupFolder, fallbackSupport) ?? new ApplicationConfig();
            Settings.CreateBackup = true;
            Settings.CreateWeeklyBackup = true;
            DebugHelper.WriteLine($"ApplicationConfig load finished: {path}");
        }

        /// <summary>
        /// Load uploaders config from file using SettingsBase mechanism
        /// </summary>
        public static void LoadUploadersConfig(bool fallbackSupport = true)
        {
            var path = UploadersConfigFilePath;
            DebugHelper.WriteLine($"UploadersConfig load started: {path}");
            UploadersConfig = UploadersConfig.Load(path, BackupFolder, fallbackSupport) ?? new UploadersConfig();
            UploadersConfig.CreateBackup = true;
            UploadersConfig.CreateWeeklyBackup = true;
            UploadersConfig.SupportDPAPIEncryption = true;
            DebugHelper.WriteLine($"UploadersConfig load finished: {path}");
        }

        /// <summary>
        /// Load workflows config from file using SettingsBase mechanism
        /// </summary>
        public static void LoadWorkflowsConfig(bool fallbackSupport = true)
        {
            var path = WorkflowsConfigFilePath;
            DebugHelper.WriteLine($"WorkflowsConfig load started: {path}");
            WorkflowsConfig = WorkflowsConfig.Load(path, BackupFolder, fallbackSupport) ?? new WorkflowsConfig();
            WorkflowsConfig.CreateBackup = true;
            WorkflowsConfig.CreateWeeklyBackup = true;

            // Ensure all workflows have valid IDs
            WorkflowsConfig.EnsureWorkflowIds();
            SyncDefaultTaskSettings();

            DebugHelper.WriteLine($"WorkflowsConfig load finished: {path}");
        }

        private static void InitializeRecentTasks()
        {
            if (Settings.RecentTasks != null)
            {
                RecentTaskManager.Initialize(Settings.RecentTasks, Settings.RecentTasksMaxCount);
            }
        }

        #endregion

        #region Save Methods

        /// <summary>
        /// Save all settings to disk
        /// </summary>
        public static void SaveAllSettings()
        {
            SaveApplicationConfig();
            SaveUploadersConfig();
            SaveWorkflowsConfig();
        }

        public static void SaveAllSettingsAsync()
        {
            SaveApplicationConfigAsync();
            SaveUploadersConfigAsync();
            SaveWorkflowsConfigAsync();
        }

        /// <summary>
        /// Save application config to file
        /// </summary>
        public static void SaveApplicationConfig()
        {
            UpdateRecentTasks();
            Settings?.Save(ApplicationConfigFilePath);
            RaiseSettingsChanged();
        }

        public static void SaveApplicationConfigAsync()
        {
            UpdateRecentTasks();
            Settings?.SaveAsync(ApplicationConfigFilePath);
        }

        /// <summary>
        /// Save uploaders config to file
        /// </summary>
        public static void SaveUploadersConfig()
        {
            UploadersConfig?.Save(UploadersConfigFilePath);
        }

        public static void SaveUploadersConfigAsync()
        {
            UploadersConfig?.SaveAsync(UploadersConfigFilePath);
        }

        /// <summary>
        /// Save workflows config to file
        /// </summary>
        public static void SaveWorkflowsConfig()
        {
            WorkflowsConfig?.Save(WorkflowsConfigFilePath);
            RaiseSettingsChanged();
        }

        public static void SaveWorkflowsConfigAsync()
        {
            WorkflowsConfig?.SaveAsync(WorkflowsConfigFilePath);
            RaiseSettingsChanged();
        }

        private static void UpdateRecentTasks()
        {
            if (Settings != null)
            {
                if (Settings.RecentTasksSave)
                {
                    Settings.RecentTasks = RecentTaskManager.ToArray();
                }
                else
                {
                    Settings.RecentTasks = null;
                }
            }
        }

        #endregion


        #region Helper Methods

        /// <summary>
        /// Gets machine-specific config filename, initializing from default if needed.
        /// </summary>
        /// <param name="destinationFolder">Folder where config files are stored</param>
        /// <param name="configPrefix">Config filename prefix (e.g., "UploadersConfig")</param>
        /// <param name="configExtension">Config filename extension (e.g., "json")</param>
        /// <param name="defaultFileName">Default filename without machine-specific suffix</param>
        /// <param name="useMachineSpecific">Whether to use machine-specific config</param>
        /// <returns>The appropriate config filename</returns>
        private static string GetMachineSpecificConfigFileName(
            string destinationFolder,
            string configPrefix,
            string configExtension,
            string defaultFileName,
            bool useMachineSpecific)
        {
            if (string.IsNullOrEmpty(destinationFolder))
            {
                return defaultFileName;
            }

            if (!useMachineSpecific)
            {
                return defaultFileName;
            }

            string sanitizedMachineName = FileHelpers.SanitizeFileName(Environment.MachineName);
            if (string.IsNullOrEmpty(sanitizedMachineName))
            {
                return defaultFileName;
            }

            string machineSpecificFileName = $"{configPrefix}-{sanitizedMachineName}.{configExtension}";
            string machineSpecificPath = Path.Combine(destinationFolder, machineSpecificFileName);

            // If machine specific file doesn't exist, initialize from default
            if (!File.Exists(machineSpecificPath))
            {
                string defaultFilePath = Path.Combine(destinationFolder, defaultFileName);

                if (File.Exists(defaultFilePath))
                {
                    try
                    {
                        File.Copy(defaultFilePath, machineSpecificPath, overwrite: false);
                    }
                    catch (IOException) when (File.Exists(machineSpecificPath))
                    {
                        // File was created by another process/thread - safe to ignore
                    }
                    catch (IOException ex)
                    {
                        DebugHelper.WriteException(ex, $"Failed to initialize machine-specific config: {machineSpecificPath}");
                    }
                }
            }

            return machineSpecificFileName;
        }

        private static string GetUploadersConfigFileName(string destinationFolder)
        {
            return GetMachineSpecificConfigFileName(
                destinationFolder,
                UploadersConfigFileNamePrefix,
                UploadersConfigFileNameExtension,
                UploadersConfigFileName,
                Settings?.UseMachineSpecificUploadersConfig ?? false);
        }

        private static string GetWorkflowsConfigFileName(string destinationFolder)
        {
            return GetMachineSpecificConfigFileName(
                destinationFolder,
                WorkflowsConfigFileNamePrefix,
                WorkflowsConfigFileNameExtension,
                WorkflowsConfigFileName,
                Settings?.UseMachineSpecificWorkflowsConfig ?? false);
        }

        private static void SyncDefaultTaskSettings()
        {
            if (WorkflowsConfig.DefaultTaskSettings == null)
            {
                WorkflowsConfig.DefaultTaskSettings = new TaskSettings { Job = WorkflowType.None };
            }

            if (WorkflowsConfig.DefaultTaskSettings.Job != WorkflowType.None)
            {
                WorkflowsConfig.DefaultTaskSettings.Job = WorkflowType.None;
            }

            DefaultTaskSettings = WorkflowsConfig.DefaultTaskSettings;
        }

        /// <summary>
        /// Reset all settings to defaults. Creates a backup before deleting.
        /// </summary>
        /// <returns>True if reset succeeded, false if an error occurred</returns>
        public static bool ResetSettings()
        {
            try
            {
                // Create timestamped backup folder
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var backupFolder = Path.Combine(BackupFolder, $"Reset_{timestamp}");
                Directory.CreateDirectory(backupFolder);

                // Backup and delete ApplicationConfig
                if (File.Exists(ApplicationConfigFilePath))
                {
                    File.Copy(ApplicationConfigFilePath,
                        Path.Combine(backupFolder, ApplicationConfigFileName), overwrite: true);
                    File.Delete(ApplicationConfigFilePath);
                }
                Settings = new ApplicationConfig();

                // Backup and delete UploadersConfig
                if (File.Exists(UploadersConfigFilePath))
                {
                    File.Copy(UploadersConfigFilePath,
                        Path.Combine(backupFolder, UploadersConfigFileName), overwrite: true);
                    File.Delete(UploadersConfigFilePath);
                }
                UploadersConfig = new UploadersConfig();

                // Backup and delete WorkflowsConfig
                if (File.Exists(WorkflowsConfigFilePath))
                {
                    File.Copy(WorkflowsConfigFilePath,
                        Path.Combine(backupFolder, WorkflowsConfigFileName), overwrite: true);
                    File.Delete(WorkflowsConfigFilePath);
                }
                WorkflowsConfig = new WorkflowsConfig();
                SyncDefaultTaskSettings();

                DebugHelper.WriteLine($"Settings reset successfully. Backup created: {backupFolder}");
                return true;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to reset settings");
                return false;
            }
        }



        public static void LoadAllSettings()
        {
            LoadApplicationConfig();
            LoadUploadersConfig();
            LoadWorkflowsConfig();


            // Initialize PathsManager
            EnsureDirectoriesExist();
        }

        public static void EnsureDirectoriesExist()
        {
            // Delegate all directory creation to PathsManager
            XerahS.Common.PathsManager.EnsureDirectoriesExist();
        }


        /// <summary>
        /// Returns the history file path in the dedicated History folder.
        /// </summary>
        public static string GetHistoryFilePath()
        {
            var path = Path.Combine(HistoryFolder, AppResources.HistoryFileName);
            DebugHelper.WriteLine($"History file path: {path} (exists={File.Exists(path)})");
            return path;
        }

        #endregion
    }
}
