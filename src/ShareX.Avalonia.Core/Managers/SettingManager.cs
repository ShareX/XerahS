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
    public static class SettingManager
    {
        public const string AppName = ShareXResources.AppName;

        #region Constants

        public const string ApplicationConfigFileName = "ApplicationConfig.json";
        public const string UploadersConfigFileNamePrefix = "UploadersConfig";
        public const string UploadersConfigFileNameExtension = "json";
        public const string UploadersConfigFileName = UploadersConfigFileNamePrefix + "." + UploadersConfigFileNameExtension;
        public const string WorkflowsConfigFileName = "WorkflowsConfig.json";
        public const string BackupFolderName = "Backup";
        public const string SettingsFolderName = "Settings";

        #endregion

        #region Static Properties

        /// <summary>
        /// Root folder for user settings. Defaults to Documents/XerahS.
        /// </summary>
        public static string PersonalFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_personalFolder))
                {
                    _personalFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);
                }
                return _personalFolder;
            }
            set => _personalFolder = value;
        }
        private static string _personalFolder = "";

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
        public static string SettingsFolder => Path.Combine(PersonalFolder, SettingsFolderName);

        /// <summary>
        /// History folder path
        /// </summary>
        public static string HistoryFolder => Path.Combine(PersonalFolder, ShareXResources.HistoryFolderName);

        /// <summary>
        /// Backup folder path
        /// </summary>
        public static string BackupFolder => Path.Combine(SettingsFolder, BackupFolderName);

        /// <summary>
        /// History backup folder path
        /// </summary>
        public static string HistoryBackupFolder => Path.Combine(HistoryFolder, BackupFolderName);

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

                return Path.Combine(workflowsConfigFolder, WorkflowsConfigFileName);
            }
        }

        /// <summary>
        /// Main application settings
        /// </summary>
        public static ApplicationConfig Settings { get; set; } = new ApplicationConfig();

        /// <summary>
        /// Uploaders configuration
        /// </summary>
        public static UploadersConfig UploadersConfig { get; set; } = new UploadersConfig();

        /// <summary>
        /// Workflows configuration
        /// </summary>
        public static WorkflowsConfig WorkflowsConfig { get; set; } = new WorkflowsConfig();

        /// <summary>
        /// Get the first workflow matching the specified HotkeyType.
        /// Returns null if no workflow exists for that type.
        /// Use this instead of GetOrCreateWorkflowTaskSettings when you need workflow-specific settings.
        /// </summary>
        public static WorkflowSettings? GetFirstWorkflow(HotkeyType hotkeyType)
        {
            return WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Job == hotkeyType);
        }

        /// <summary>
        /// Retrieve a workflow's task settings by hotkey type, creating a workflow entry if none exists.
        /// </summary>
        [Obsolete("Use GetFirstWorkflow() to get the full WorkflowSettings, or pass TaskSettings explicitly. " +
                  "Looking up by HotkeyType is ambiguous when multiple workflows share the same type.")]
        public static TaskSettings GetOrCreateWorkflowTaskSettings(HotkeyType hotkeyType)
        {
            WorkflowsConfig ??= new WorkflowsConfig();
            WorkflowsConfig.Hotkeys ??= new List<WorkflowSettings>();

            var workflow = WorkflowsConfig.Hotkeys.FirstOrDefault(w => w.Job == hotkeyType);
            if (workflow == null)
            {
                workflow = new WorkflowSettings(hotkeyType, new HotkeyInfo());
                WorkflowsConfig.Hotkeys.Add(workflow);
            }

            if (workflow.TaskSettings == null)
            {
                workflow.TaskSettings = new TaskSettings();
            }

            return workflow.TaskSettings;
        }

        /// <summary>
        /// Get a default TaskSettings instance.
        /// Use this for fallback/global settings instead of looking up by HotkeyType.
        /// </summary>
        public static TaskSettings DefaultTaskSettings { get; } = new TaskSettings();

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

        private static string GetUploadersConfigFileName(string destinationFolder)
        {
            if (string.IsNullOrEmpty(destinationFolder))
            {
                // Fallback if no specific folder is determined yet, but usually not called this way
                return UploadersConfigFileName;
            }

            if (Settings != null && Settings.UseMachineSpecificUploadersConfig)
            {
                string sanitizedMachineName = FileHelpers.SanitizeFileName(Environment.MachineName);

                if (!string.IsNullOrEmpty(sanitizedMachineName))
                {
                    string machineSpecificFileName = $"{UploadersConfigFileNamePrefix}-{sanitizedMachineName}.{UploadersConfigFileNameExtension}";
                    string machineSpecificPath = Path.Combine(destinationFolder, machineSpecificFileName);

                    // If machine specific file doesn't exist, we might want to initialize it from default
                    if (!File.Exists(machineSpecificPath))
                    {
                        string defaultFilePath = Path.Combine(destinationFolder, UploadersConfigFileName);

                        // If default exists, copy it to machine specific
                        if (File.Exists(defaultFilePath))
                        {
                            try
                            {
                                File.Copy(defaultFilePath, machineSpecificPath, false);
                            }
                            catch (IOException)
                            {
                                // Ignore
                            }
                        }
                    }

                    return machineSpecificFileName;
                }
            }

            return UploadersConfigFileName;
        }

        /// <summary>
        /// Reset all settings to defaults
        /// </summary>
        public static void ResetSettings()
        {
            // Delete files? Or just re-instantiate? Original returns to defaults.
            if (File.Exists(ApplicationConfigFilePath)) File.Delete(ApplicationConfigFilePath);
            Settings = new ApplicationConfig();

            if (File.Exists(UploadersConfigFilePath)) File.Delete(UploadersConfigFilePath);
            UploadersConfig = new UploadersConfig();

            if (File.Exists(WorkflowsConfigFilePath)) File.Delete(WorkflowsConfigFilePath);
            WorkflowsConfig = new WorkflowsConfig();
        }

        /// <summary>
        /// Ensure required directories exist
        /// </summary>
        public static void EnsureDirectoriesExist()
        {
            if (!string.IsNullOrEmpty(SettingsFolder))
            {
                FileHelpers.CreateDirectory(SettingsFolder);
            }

            if (!string.IsNullOrEmpty(BackupFolder))
            {
                FileHelpers.CreateDirectory(BackupFolder);
            }

            if (!string.IsNullOrEmpty(HistoryFolder))
            {
                FileHelpers.CreateDirectory(HistoryFolder);
            }

            if (!string.IsNullOrEmpty(HistoryBackupFolder))
            {
                FileHelpers.CreateDirectory(HistoryBackupFolder);
            }
        }

        /// <summary>
        /// Returns the history file path in the dedicated History folder.
        /// </summary>
        public static string GetHistoryFilePath()
        {
            var path = Path.Combine(HistoryFolder, ShareXResources.HistoryFileName);
            DebugHelper.WriteLine($"History file path: {path} (exists={File.Exists(path)})");
            return path;
        }

        #endregion
    }
}
