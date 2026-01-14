using System;
using System.IO;

namespace XerahS.Common
{
    public static class PathsManager
    {
        private static string _personalFolder = "";

        public static string PersonalFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_personalFolder))
                {
                    _personalFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                        AppResources.AppName);
                }
                return _personalFolder;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _personalFolder = value;
                }
            }
        }

        public static string ScreenshotsFolder => Path.Combine(PersonalFolder, AppResources.ScreenshotsFolderName);
        public static string ScreencastsFolder => Path.Combine(PersonalFolder, AppResources.ScreencastsFolderName);
        public static string FrameDumpsFolder => Path.Combine(ScreencastsFolder, "FrameDumps");
        
        public static string SettingsFolder => Path.Combine(PersonalFolder, AppResources.SettingsFolderName);
        public static string HistoryFolder => Path.Combine(PersonalFolder, AppResources.HistoryFolderName);
        public static string BackupFolder => Path.Combine(SettingsFolder, AppResources.BackupFolderName);
        public static string HistoryBackupFolder => Path.Combine(HistoryFolder, AppResources.BackupFolderName);
        public static string PluginsFolder
        {
            get
            {
#if DEBUG
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppResources.PluginsFolderName);
#else
                return Path.Combine(PersonalFolder, AppResources.PluginsFolderName);
#endif
            }
        }

        public static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(PersonalFolder))
                Directory.CreateDirectory(PersonalFolder);
            
            if (!Directory.Exists(ScreenshotsFolder))
                Directory.CreateDirectory(ScreenshotsFolder);
            
            if (!Directory.Exists(ScreencastsFolder))
                Directory.CreateDirectory(ScreencastsFolder);
            
            if (!Directory.Exists(FrameDumpsFolder))
                Directory.CreateDirectory(FrameDumpsFolder);
            
            if (!Directory.Exists(SettingsFolder))
                Directory.CreateDirectory(SettingsFolder);
            
            if (!Directory.Exists(HistoryFolder))
                Directory.CreateDirectory(HistoryFolder);
            
            if (!Directory.Exists(BackupFolder))
                Directory.CreateDirectory(BackupFolder);
            
            if (!Directory.Exists(PluginsFolder))
                Directory.CreateDirectory(PluginsFolder);
        }
    }
}
