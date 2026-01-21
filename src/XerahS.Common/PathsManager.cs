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
        public static string GetFFmpegPath()
        {
            // 1. Check Personal Tools Folder (Prioritized)
            string toolsFFmpeg = Path.Combine(ToolsFolder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            DebugHelper.WriteLine($"[FFmpeg] Checking common path: {toolsFFmpeg}");
            if (File.Exists(toolsFFmpeg))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at: {toolsFFmpeg}");
                return toolsFFmpeg;
            }

            // Check without extension on macOS/Linux if strict naming is used
            if (!OperatingSystem.IsWindows())
            {
                string toolsFFmpegNoExt = Path.Combine(ToolsFolder, "ffmpeg");
                if (toolsFFmpeg != toolsFFmpegNoExt)
                {
                    DebugHelper.WriteLine($"[FFmpeg] Checking common path: {toolsFFmpegNoExt}");
                    if (File.Exists(toolsFFmpegNoExt))
                    {
                        DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at: {toolsFFmpegNoExt}");
                        return toolsFFmpegNoExt;
                    }
                }
            }

            // 2. Check Common System Locations
            string[] commonPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FFmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FFmpeg", "bin", "ffmpeg.exe"),
                "/opt/homebrew/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/usr/bin/ffmpeg"
            };

            foreach (var path in commonPaths)
            {
                DebugHelper.WriteLine($"[FFmpeg] Checking common path: {path}");
                if (File.Exists(path))
                {
                    DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at: {path}");
                    return path;
                }
            }

            // 3. Check PATH Environment Variable
            DebugHelper.WriteLine("[FFmpeg] Searching PATH environment variable...");
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    var ffmpegExecutable = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
                    var ffmpegPath = Path.Combine(dir, ffmpegExecutable);
                    if (File.Exists(ffmpegPath))
                    {
                        DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg in PATH at: {ffmpegPath}");
                        return ffmpegPath;
                    }
                }
            }

            DebugHelper.WriteLine("[FFmpeg] FFmpeg not found in any standard location.");
            return string.Empty;
        }
    }
}
