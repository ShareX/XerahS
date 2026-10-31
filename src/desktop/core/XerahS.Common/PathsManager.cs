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
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace XerahS.Common
{
    public static class PathsManager
    {
        private static string _personalFolder = "";
        private static bool _personalFolderOverrideSet;
        private const string PluginManifestFileName = "plugin.json";
        private const string PluginMigrationConflictFolderName = "_migration_conflicts";
        private static readonly string[] KnownPluginArchitectureFolders =
        [
            "win-arm64",
            "win-x64",
            "win-x86",
            "macos64",
            "linux64"
        ];

        public static string PersonalFolder
        {
            get
            {
                if (UseLinuxXdgLayout)
                {
                    return LinuxXdgDirectories.Detect().DataDirectory;
                }

                if (string.IsNullOrEmpty(_personalFolder))
                {
                    _personalFolder = Path.Combine(IsPortable ? AppContext.BaseDirectory : GetDocumentsFolder(), AppResources.AppName);
                }
                return _personalFolder;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _personalFolder = value;
                    _personalFolderOverrideSet = true;
                }
            }
        }

        public static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.txt"));

        private static bool UseLinuxXdgLayout => OperatingSystem.IsLinux() && !_personalFolderOverrideSet && !IsPortable;

        private static string GetDocumentsFolder()
        {
            // On macOS, launchd/agent hosts can override HOME to an agent profile
            // directory (for example ~/.hermes/profiles/<agent>/home). The CLR then
            // resolves SpecialFolder.MyDocuments inside that profile, causing CLI
            // plugin discovery to look in the wrong XerahS/Plugins folder. Resolve
            // the real login account home through libc before falling back to the
            // environment-sensitive .NET special folders.
            if (OperatingSystem.IsMacOS())
            {
                string? nativeHome = GetNativeUserHomeDirectory();
                if (!string.IsNullOrWhiteSpace(nativeHome))
                {
                    string nativeDocuments = Path.Combine(nativeHome, "Documents");
                    if (Directory.Exists(nativeDocuments))
                    {
                        return nativeDocuments;
                    }
                }

                string userNameDocuments = Path.Combine("/Users", Environment.UserName, "Documents");
                if (Directory.Exists(userNameDocuments))
                {
                    return userNameDocuments;
                }
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents) && Path.IsPathRooted(documents))
            {
                return documents;
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string userProfileDocuments = Path.Combine(userProfile ?? string.Empty, "Documents");
            if (!string.IsNullOrWhiteSpace(userProfile) && Path.IsPathRooted(userProfileDocuments))
            {
                return userProfileDocuments;
            }

            return string.IsNullOrWhiteSpace(documents) ? Environment.CurrentDirectory : documents;
        }

        private static string? GetNativeUserHomeDirectory()
        {
            try
            {
                IntPtr passwdPointer = getpwuid(getuid());
                if (passwdPointer == IntPtr.Zero)
                {
                    return null;
                }

                Passwd passwd = Marshal.PtrToStructure<Passwd>(passwdPointer);
                return Marshal.PtrToStringUTF8(passwd.HomeDirectory);
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException || ex is MarshalDirectiveException)
            {
                DebugHelper.WriteLine($"Unable to resolve native user home directory: {ex.Message}");
                return null;
            }
        }

        [DllImport("libc")]
        private static extern uint getuid();

        [DllImport("libc")]
        private static extern IntPtr getpwuid(uint uid);

        [StructLayout(LayoutKind.Sequential)]
        private struct Passwd
        {
            public IntPtr Name;
            public IntPtr Password;
            public uint Uid;
            public uint Gid;
            public long Change;
            public IntPtr Class;
            public IntPtr Gecos;
            public IntPtr HomeDirectory;
            public IntPtr Shell;
            public long Expire;
        }

        public static string ScreenshotsFolder => UseLinuxXdgLayout
            ? Path.Combine(LinuxXdgDirectories.Detect().DataDirectory, AppResources.ScreenshotsFolderName)
            : Path.Combine(PersonalFolder, AppResources.ScreenshotsFolderName);

        public static string ScreencastsFolder => UseLinuxXdgLayout
            ? Path.Combine(LinuxXdgDirectories.Detect().DataDirectory, AppResources.ScreencastsFolderName)
            : Path.Combine(PersonalFolder, AppResources.ScreencastsFolderName);

        public static string FrameDumpsFolder => Path.Combine(ScreencastsFolder, "FrameDumps");

        /// <summary>
        /// Environment variable that redirects all log files (main and error logs) to another folder.
        /// Test runs set it so they never write into the user's real error log.
        /// </summary>
        public const string LogsFolderOverrideVariable = "XERAHS_LOGS_DIR";

        /// <summary>Base folder for all log files (e.g. PersonalFolder/Logs).</summary>
        public static string LogsFolderBase
        {
            get
            {
                string? overridePath = Environment.GetEnvironmentVariable(LogsFolderOverrideVariable);
                if (!string.IsNullOrWhiteSpace(overridePath))
                {
                    return overridePath;
                }

                return UseLinuxXdgLayout
                    ? Path.Combine(LinuxXdgDirectories.Detect().StateDirectory, "Logs")
                    : Path.Combine(PersonalFolder, "Logs");
            }
        }

        /// <summary>Logs subfolder for the given month (e.g. Logs/yyyy-MM). Uses current date if null.</summary>
        public static string GetLogsFolderForMonth(DateTime? date = null) =>
            Path.Combine(LogsFolderBase, (date ?? DateTime.Now).ToString("yyyy-MM"));

        /// <summary>Filename prefix for the dedicated error log (full name: XerahS-errors-yyyyMMdd.log).</summary>
        public const string ErrorLogFileNamePrefix = "XerahS-errors";

        /// <summary>Full path to the error log file for today: Logs/yyyy-MM/XerahS-errors-yyyyMMdd.log.</summary>
        public static string GetErrorLogFilePath()
        {
            var date = DateTime.Now;
            return Path.Combine(GetLogsFolderForMonth(date), $"{ErrorLogFileNamePrefix}-{date:yyyyMMdd}.log");
        }

        /// <summary>Full path to the main log file for today: Logs/yyyy-MM/AppName-yyyyMMdd.log.</summary>
        public static string GetMainLogFilePath()
        {
            var date = DateTime.Now;
            return Path.Combine(GetLogsFolderForMonth(date), $"{AppResources.AppName}-{date:yyyyMMdd}.log");
        }

        public static string SettingsFolder => UseLinuxXdgLayout
            ? LinuxXdgDirectories.Detect().ConfigDirectory
            : Path.Combine(PersonalFolder, AppResources.SettingsFolderName);

        public static string HistoryFolder => UseLinuxXdgLayout
            ? Path.Combine(LinuxXdgDirectories.Detect().StateDirectory, AppResources.HistoryFolderName)
            : Path.Combine(PersonalFolder, AppResources.HistoryFolderName);

        public static string BackupFolder => Path.Combine(SettingsFolder, AppResources.BackupFolderName);
        public static string HistoryBackupFolder => Path.Combine(HistoryFolder, AppResources.BackupFolderName);
        /// <summary>Folder for troubleshooting / diagnostic logs (e.g. DPI, capture).</summary>
        public static string TroubleshootingFolder => UseLinuxXdgLayout
            ? Path.Combine(LinuxXdgDirectories.Detect().StateDirectory, "Troubleshooting")
            : Path.Combine(PersonalFolder, "Troubleshooting");

        /// <summary>Base folder for capture verification outputs (region/recording verify).</summary>
        public static string CaptureTroubleshootingFolder => UseLinuxXdgLayout
            ? Path.Combine(LinuxXdgDirectories.Detect().StateDirectory, "CaptureTroubleshooting")
            : Path.Combine(PersonalFolder, "CaptureTroubleshooting");

        public static string ToolsFolder => UseLinuxXdgLayout
            ? Path.Combine(LinuxXdgDirectories.Detect().DataDirectory, "Tools")
            : Path.Combine(PersonalFolder, "Tools");

        public static string ToolsArchitectureFolder => Path.Combine(ToolsFolder, GetToolsArchitectureFolderName());

        /// <summary>
        /// Stable local tools folder name (win-x64, linux-arm64, ...), independent of download asset naming.
        /// Plugins keep <see cref="GetArchitectureFolderName"/>, where Linux is still "linux64".
        /// </summary>
        public static string GetToolsArchitectureFolderName() => GetToolsArchitectureFolderName(RuntimeInformation.OSArchitecture);

        public static string GetToolsArchitectureFolderName(Architecture architecture)
        {
            if (OperatingSystem.IsLinux())
            {
                return architecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            }

            return GetArchitectureFolderName();
        }

        // Before 0.32.3 every Linux CPU shared Tools/linux64.
        private static string LegacyLinuxToolsFolder => Path.Combine(ToolsFolder, "linux64");
        public static string PluginsFolder => UseLinuxXdgLayout
            ? Path.Combine(LinuxXdgDirectories.Detect().DataDirectory, AppResources.PluginsFolderName)
            : Path.Combine(PersonalFolder, AppResources.PluginsFolderName);

        public static string PluginsArchitectureFolder => Path.Combine(PluginsFolder, GetArchitectureFolderName());
        public static string AppPluginsFolder => Path.Combine(AppContext.BaseDirectory, AppResources.PluginsFolderName);
        public static string CurrentArchitectureFolderName => GetArchitectureFolderName();

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

            if (!Directory.Exists(PluginsArchitectureFolder))
                Directory.CreateDirectory(PluginsArchitectureFolder);

            if (!Directory.Exists(ToolsFolder))
                Directory.CreateDirectory(ToolsFolder);

            if (!Directory.Exists(ToolsArchitectureFolder))
                Directory.CreateDirectory(ToolsArchitectureFolder);

            MigrateLegacyPluginDirectories();
        }

        public static System.Collections.Generic.IEnumerable<string> GetPluginDirectories()
        {
            EnsureDirectoriesExist();

            var paths = new System.Collections.Generic.List<string>();

            if (Directory.Exists(AppPluginsFolder))
            {
                paths.Add(AppPluginsFolder);
            }

            if (Directory.Exists(PluginsArchitectureFolder))
            {
                paths.Add(PluginsArchitectureFolder);
            }

            if (Directory.Exists(PluginsFolder))
            {
                paths.Add(PluginsFolder);
            }

            return paths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string GetUserPluginDirectory(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                return PluginsArchitectureFolder;
            }

            return Path.Combine(PluginsArchitectureFolder, pluginId);
        }

        public static string GetFFmpegPath()
        {
            return GetToolPath("FFmpeg", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        }

        public static string GetFFprobePath()
        {
            return GetToolPath("FFprobe", OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        }

        private static string GetToolPath(string toolName, string executableName)
        {
            // 1. Check Personal Tools Architecture Folder (Prioritized)
            string toolsExecutablePath = Path.Combine(ToolsArchitectureFolder, executableName);
            DebugHelper.WriteLine($"[{toolName}] Checking architecture tools path: {toolsExecutablePath}");
            if (File.Exists(toolsExecutablePath))
            {
                DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {toolsExecutablePath}");
                return toolsExecutablePath;
            }

            // Check without extension on macOS/Linux if strict naming is used
            if (!OperatingSystem.IsWindows())
            {
                string toolsExecutableNoExt = Path.Combine(ToolsArchitectureFolder, Path.GetFileNameWithoutExtension(executableName));
                if (toolsExecutablePath != toolsExecutableNoExt)
                {
                    DebugHelper.WriteLine($"[{toolName}] Checking architecture tools path: {toolsExecutableNoExt}");
                    if (File.Exists(toolsExecutableNoExt))
                    {
                        DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {toolsExecutableNoExt}");
                        return toolsExecutableNoExt;
                    }
                }
            }

            if (OperatingSystem.IsLinux())
            {
                string legacyLinuxExecutablePath = Path.Combine(LegacyLinuxToolsFolder, executableName);
                DebugHelper.WriteLine($"[{toolName}] Checking legacy architecture tools path: {legacyLinuxExecutablePath}");
                if (File.Exists(legacyLinuxExecutablePath))
                {
                    DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {legacyLinuxExecutablePath}");
                    return legacyLinuxExecutablePath;
                }
            }

            // 1b. Check legacy Personal Tools Folder
            string legacyToolsExecutablePath = Path.Combine(ToolsFolder, executableName);
            DebugHelper.WriteLine($"[{toolName}] Checking legacy tools path: {legacyToolsExecutablePath}");
            if (File.Exists(legacyToolsExecutablePath))
            {
                DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {legacyToolsExecutablePath}");
                return legacyToolsExecutablePath;
            }

            if (!OperatingSystem.IsWindows())
            {
                string legacyToolsExecutableNoExt = Path.Combine(ToolsFolder, Path.GetFileNameWithoutExtension(executableName));
                if (legacyToolsExecutablePath != legacyToolsExecutableNoExt)
                {
                    DebugHelper.WriteLine($"[{toolName}] Checking legacy tools path: {legacyToolsExecutableNoExt}");
                    if (File.Exists(legacyToolsExecutableNoExt))
                    {
                        DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {legacyToolsExecutableNoExt}");
                        return legacyToolsExecutableNoExt;
                    }
                }
            }

            // 2. Check Common System Locations
            string appToolsDir = GetAppToolsDirectory();
            string[] commonPaths = new[]
            {
                Path.Combine(appToolsDir, executableName),
                Path.Combine(AppContext.BaseDirectory, executableName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FFmpeg", "bin", executableName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FFmpeg", "bin", executableName),
                $"/opt/homebrew/bin/{Path.GetFileNameWithoutExtension(executableName)}",
                $"/usr/local/bin/{Path.GetFileNameWithoutExtension(executableName)}",
                $"/usr/bin/{Path.GetFileNameWithoutExtension(executableName)}"
            };

            foreach (var path in commonPaths)
            {
                DebugHelper.WriteLine($"[{toolName}] Checking common path: {path}");
                if (File.Exists(path))
                {
                    DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {path}");
                    return path;
                }
            }

            // 3. Check PATH Environment Variable
            DebugHelper.WriteLine($"[{toolName}] Searching PATH environment variable...");
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    var toolPath = Path.Combine(dir, executableName);
                    if (File.Exists(toolPath))
                    {
                        DebugHelper.WriteLine($"[{toolName}] Found {toolName} in PATH at: {toolPath}");
                        return toolPath;
                    }
                }
            }

            DebugHelper.WriteLine($"[{toolName}] {toolName} not found in any standard location.");
            return string.Empty;
        }

        /// <summary>App-bundled tools directory (BaseDirectory/Tools). Used for FFmpeg lookup and path consistency.</summary>
        private static string GetAppToolsDirectory() =>
            Path.Combine(AppContext.BaseDirectory, "Tools");

        private static void MigrateLegacyPluginDirectories()
        {
            if (!Directory.Exists(PluginsFolder))
            {
                return;
            }

            string[] legacyPluginDirectories;
            try
            {
                legacyPluginDirectories = Directory.GetDirectories(PluginsFolder);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                DebugHelper.WriteLine($"[Plugins] Skipping legacy plugin migration because '{PluginsFolder}' cannot be enumerated: {ex.Message}");
                return;
            }

            foreach (var legacyPluginDirectory in legacyPluginDirectories)
            {
                string directoryName = Path.GetFileName(legacyPluginDirectory);

                if (IsKnownPluginArchitectureFolder(directoryName))
                {
                    continue;
                }

                string manifestPath = Path.Combine(legacyPluginDirectory, PluginManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                string destinationArchitectureFolder = TryResolvePluginArchitectureFolder(legacyPluginDirectory);
                string destinationRoot = Path.Combine(PluginsFolder, destinationArchitectureFolder);
                Directory.CreateDirectory(destinationRoot);

                string destinationDirectory = Path.Combine(destinationRoot, directoryName);
                MoveLegacyPluginDirectory(legacyPluginDirectory, destinationDirectory);
            }
        }

        private static bool IsKnownPluginArchitectureFolder(string directoryName)
        {
            return KnownPluginArchitectureFolders.Contains(directoryName, StringComparer.OrdinalIgnoreCase);
        }

        private static string TryResolvePluginArchitectureFolder(string pluginDirectory)
        {
            try
            {
                string? assemblyPath = TryGetPluginAssemblyPath(pluginDirectory);
                if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                {
                    return GetArchitectureFolderName();
                }

                using FileStream stream = File.OpenRead(assemblyPath);
                using PEReader peReader = new(stream);
                PEHeaders peHeaders = peReader.PEHeaders;

                bool isAnyCpu = peHeaders.CorHeader != null &&
                    (peHeaders.CorHeader.Flags & CorFlags.ILOnly) != 0 &&
                    (peHeaders.CorHeader.Flags & CorFlags.Requires32Bit) == 0;

                return peHeaders.CoffHeader.Machine switch
                {
                    Machine.Arm64 => "win-arm64",
                    Machine.Amd64 => "win-x64",
                    Machine.I386 when isAnyCpu => GetArchitectureFolderName(),
                    Machine.I386 => "win-x86",
                    _ => GetArchitectureFolderName()
                };
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[Plugins] Failed to detect plugin architecture for '{pluginDirectory}': {ex.Message}");
                return GetArchitectureFolderName();
            }
        }

        private static string? TryGetPluginAssemblyPath(string pluginDirectory)
        {
            string manifestPath = Path.Combine(pluginDirectory, PluginManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                string manifestJson = File.ReadAllText(manifestPath);
                using JsonDocument document = JsonDocument.Parse(manifestJson);
                JsonElement root = document.RootElement;

                string? assemblyFileName = TryGetJsonStringProperty(root, "assemblyFileName");

                if (string.IsNullOrWhiteSpace(assemblyFileName))
                {
                    string? pluginId = TryGetJsonStringProperty(root, "pluginId");
                    if (!string.IsNullOrWhiteSpace(pluginId))
                    {
                        assemblyFileName = $"{pluginId}.dll";
                    }
                }

                if (string.IsNullOrWhiteSpace(assemblyFileName))
                {
                    assemblyFileName = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .FirstOrDefault();
                }

                return string.IsNullOrWhiteSpace(assemblyFileName)
                    ? null
                    : Path.Combine(pluginDirectory, assemblyFileName);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[Plugins] Failed to read plugin manifest '{manifestPath}': {ex.Message}");
                return null;
            }
        }

        private static string? TryGetJsonStringProperty(JsonElement element, string propertyName)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.ToString();
                }
            }

            return null;
        }

        private static void MoveLegacyPluginDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (string.Equals(sourceDirectory, destinationDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Directory.Exists(destinationDirectory))
            {
                string conflictDirectory = Path.Combine(
                    PluginsFolder,
                    PluginMigrationConflictFolderName,
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
                    Path.GetFileName(sourceDirectory));

                string? parentDirectory = Path.GetDirectoryName(conflictDirectory);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                Directory.Move(sourceDirectory, EnsureUniqueDirectoryDestination(conflictDirectory));
                DebugHelper.WriteLine($"[Plugins] Migration conflict for '{sourceDirectory}'. Moved legacy copy to '{conflictDirectory}'.");
                return;
            }

            Directory.Move(sourceDirectory, destinationDirectory);
            DebugHelper.WriteLine($"[Plugins] Migrated legacy plugin folder '{sourceDirectory}' -> '{destinationDirectory}'.");
        }

        private static string EnsureUniqueDirectoryDestination(string path)
        {
            if (!Directory.Exists(path))
            {
                return path;
            }

            string parentDirectory = Path.GetDirectoryName(path) ?? PluginsFolder;
            string baseName = Path.GetFileName(path);
            int suffix = 1;

            string candidate;
            do
            {
                candidate = Path.Combine(parentDirectory, $"{baseName}_{suffix}");
                suffix++;
            }
            while (Directory.Exists(candidate));

            return candidate;
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
