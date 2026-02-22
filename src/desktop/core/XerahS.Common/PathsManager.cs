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
using System.Runtime.InteropServices;

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
        public static string ToolsArchitectureFolder => Path.Combine(ToolsFolder, GetArchitectureFolderName());
        public static string PluginsFolder
        {
            get
            {
#if DEBUG
                if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
                    return Path.Combine(PersonalFolder, AppResources.PluginsFolderName);
                return Path.Combine(AppContext.BaseDirectory, AppResources.PluginsFolderName);
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

            if (!Directory.Exists(ToolsArchitectureFolder))
                Directory.CreateDirectory(ToolsArchitectureFolder);
        }

        public static System.Collections.Generic.IEnumerable<string> GetPluginDirectories()
        {
            var paths = new System.Collections.Generic.List<string>();

            // 1. App-bundled plugins (BaseDirectory/Plugins)
            // In Release, we also want to check this location adjacent to the executable
            string appPluginsPath = Path.Combine(AppContext.BaseDirectory, AppResources.PluginsFolderName);
            if (Directory.Exists(appPluginsPath))
            {
                paths.Add(appPluginsPath);
            }

            // 2. User-installed plugins (PluginsFolder -> PersonalFolder/Plugins)
            // This allows users to add plugins without modifying the app installation
            string userPluginsPath = PluginsFolder;
            
            // Only add if it exists and is different from the app plugins path
            if (Directory.Exists(userPluginsPath) && 
                !string.Equals(appPluginsPath, userPluginsPath, StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(userPluginsPath);
            }

            return paths;
        }

        public static string GetFFmpegPath()
        {
            // 1. Check Personal Tools Architecture Folder (Prioritized)
            string toolsFFmpeg = Path.Combine(ToolsArchitectureFolder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            DebugHelper.WriteLine($"[FFmpeg] Checking architecture tools path: {toolsFFmpeg}");
            if (File.Exists(toolsFFmpeg))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at: {toolsFFmpeg}");
                return toolsFFmpeg;
            }

            // Check without extension on macOS/Linux if strict naming is used
            if (!OperatingSystem.IsWindows())
            {
                string toolsFFmpegNoExt = Path.Combine(ToolsArchitectureFolder, "ffmpeg");
                if (toolsFFmpeg != toolsFFmpegNoExt)
                {
                    DebugHelper.WriteLine($"[FFmpeg] Checking architecture tools path: {toolsFFmpegNoExt}");
                    if (File.Exists(toolsFFmpegNoExt))
                    {
                        DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at: {toolsFFmpegNoExt}");
                        return toolsFFmpegNoExt;
                    }
                }
            }

            // 1b. Check legacy Personal Tools Folder
            string legacyToolsFFmpeg = Path.Combine(ToolsFolder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            DebugHelper.WriteLine($"[FFmpeg] Checking legacy tools path: {legacyToolsFFmpeg}");
            if (File.Exists(legacyToolsFFmpeg))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at: {legacyToolsFFmpeg}");
                return legacyToolsFFmpeg;
            }

            if (!OperatingSystem.IsWindows())
            {
                string legacyToolsFFmpegNoExt = Path.Combine(ToolsFolder, "ffmpeg");
                if (legacyToolsFFmpeg != legacyToolsFFmpegNoExt)
                {
                    DebugHelper.WriteLine($"[FFmpeg] Checking legacy tools path: {legacyToolsFFmpegNoExt}");
                    if (File.Exists(legacyToolsFFmpegNoExt))
                    {
                        DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at: {legacyToolsFFmpegNoExt}");
                        return legacyToolsFFmpegNoExt;
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

        private static string GetArchitectureFolderName()
        {
            if (OperatingSystem.IsWindows())
            {
                return RuntimeInformation.OSArchitecture switch
                {
                    Architecture.Arm64 => "win-arm64",
                    Architecture.X64 => "win-x64",
                    _ => "win-x86"
                };
            }

            if (OperatingSystem.IsMacOS())
            {
                return "macos64";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux64";
            }

            return "win-x64";
        }
    }
}
