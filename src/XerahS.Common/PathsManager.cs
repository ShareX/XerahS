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
        public static string ToolsFolder => Path.Combine(PersonalFolder, "Tools");
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

            if (!Directory.Exists(ToolsFolder))
                Directory.CreateDirectory(ToolsFolder);
        }
    }
}
