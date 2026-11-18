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
using Newtonsoft.Json.Linq;
using XerahS.Common;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Conservatively cleans plugin folders by quarantining files that are not
/// referenced by plugin.json or the plugin's deps manifest.
/// </summary>
internal static class PluginFolderCleaner
{
    private const string ManifestFileName = "plugin.json";
    private const string QuarantineDirectoryName = "_quarantine";

    private static readonly Lock _scheduleLock = new();
    private static readonly HashSet<string> _pendingRoots = new(StringComparer.OrdinalIgnoreCase);
    private static Task? _backgroundTask;

    public static void ScheduleCleanup(IEnumerable<string> pluginDirectories)
    {
        if (pluginDirectories == null)
        {
            return;
        }

        lock (_scheduleLock)
        {
            foreach (var directory in pluginDirectories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                try
                {
                    var fullPath = Path.GetFullPath(directory);
                    if (Directory.Exists(fullPath))
                    {
                        _pendingRoots.Add(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"[PluginCleaner] Invalid path '{directory}': {ex.Message}");
                }
            }

            if (_pendingRoots.Count == 0)
            {
                return;
            }

            if (_backgroundTask is { IsCompleted: false })
            {
                return;
            }

            _backgroundTask = Task.Run(RunPendingCleanup);
        }
    }

    private static void RunPendingCleanup()
    {
        while (true)
        {
            List<string> rootsToProcess;

            lock (_scheduleLock)
            {
                if (_pendingRoots.Count == 0)
                {
                    _backgroundTask = null;
                    return;
                }

                rootsToProcess = _pendingRoots.ToList();
                _pendingRoots.Clear();
            }

            foreach (var pluginsRoot in rootsToProcess)
            {
                try
                {
                    CleanPluginsRoot(pluginsRoot);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"[PluginCleaner] Failed cleaning root '{pluginsRoot}': {ex.Message}");
                }
            }
        }
    }

    private static void CleanPluginsRoot(string pluginsRoot)
    {
        if (!Directory.Exists(pluginsRoot))
        {
            return;
        }

        var pluginDirectories = Directory.GetDirectories(pluginsRoot);
        foreach (var pluginDirectory in pluginDirectories)
        {
            try
            {
                var manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                CleanSinglePluginDirectory(pluginDirectory, manifestPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[PluginCleaner] Failed cleaning '{pluginDirectory}': {ex.Message}");
            }
        }
    }

    private static void CleanSinglePluginDirectory(string pluginDirectory, string manifestPath)
    {
        var manifest = LoadManifest(manifestPath);
        if (manifest == null)
        {
            return;
        }

        var keepFiles = BuildKeepFileSet(pluginDirectory, manifestPath, manifest);
        var quarantineRoot = Path.Combine(pluginDirectory, QuarantineDirectoryName);
        PruneEmptyQuarantineDirectories(quarantineRoot);

        var allFiles = Directory.GetFiles(pluginDirectory, "*", SearchOption.AllDirectories);

        var filesToQuarantine = allFiles
            .Select(Path.GetFullPath)
            .Where(file => !IsUnderDirectory(file, quarantineRoot))
            .Where(file => !keepFiles.Contains(file))
            .ToList();

        if (filesToQuarantine.Count == 0)
        {
            return;
        }

        var runQuarantineDirectory = Path.Combine(
            quarantineRoot,
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        // Bundled plugin folders can live on a read-only file system (e.g. /app inside a
        // Flatpak sandbox). Quarantining is impossible there, so skip quietly with one line
        // instead of logging a failure per file.
        try
        {
            Directory.CreateDirectory(runQuarantineDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DebugHelper.WriteLine($"[PluginCleaner] Skipping cleanup of '{pluginDirectory}' (not writable): {ex.Message}");
            return;
        }

        int quarantinedCount = 0;
        foreach (var file in filesToQuarantine)
        {
            try
            {
                var relativePath = Path.GetRelativePath(pluginDirectory, file);
                var destinationPath = Path.Combine(runQuarantineDirectory, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                var finalDestination = EnsureUniqueDestination(destinationPath);
                File.Move(file, finalDestination);
                quarantinedCount++;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[PluginCleaner] Could not quarantine '{file}': {ex.Message}");
            }
        }

        if (quarantinedCount > 0)
        {
            DebugHelper.WriteLine($"[PluginCleaner] Quarantined {quarantinedCount} file(s) in {pluginDirectory}");
        }
    }

    private static HashSet<string> BuildKeepFileSet(string pluginDirectory, string manifestPath, PluginManifest manifest)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(manifestPath)
        };

        var assemblyFileName = manifest.GetAssemblyFileName();
        var assemblyPath = Path.GetFullPath(Path.Combine(pluginDirectory, assemblyFileName));
        AddIfExistsInsidePlugin(pluginDirectory, assemblyPath, keep);

        var assemblyBaseName = Path.GetFileNameWithoutExtension(assemblyFileName);
        var depsPath = Path.GetFullPath(Path.Combine(pluginDirectory, $"{assemblyBaseName}.deps.json"));
        AddIfExistsInsidePlugin(pluginDirectory, depsPath, keep);

        var runtimeConfigPath = Path.GetFullPath(Path.Combine(pluginDirectory, $"{assemblyBaseName}.runtimeconfig.json"));
        AddIfExistsInsidePlugin(pluginDirectory, runtimeConfigPath, keep);

        var xmlDocPath = Path.GetFullPath(Path.Combine(pluginDirectory, $"{assemblyBaseName}.xml"));
        AddIfExistsInsidePlugin(pluginDirectory, xmlDocPath, keep);

        var pdbPath = Path.GetFullPath(Path.Combine(pluginDirectory, $"{assemblyBaseName}.pdb"));
        AddIfExistsInsidePlugin(pluginDirectory, pdbPath, keep);

        if (File.Exists(depsPath))
        {
            AddDepsReferencedFiles(pluginDirectory, depsPath, keep);
        }

        foreach (var dependency in manifest.Dependencies)
        {
            if (!IsSafePluginRelativeFilePath(dependency))
            {
                DebugHelper.WriteLine($"[PluginCleaner] Ignoring unsafe declared dependency path '{dependency}' in '{manifestPath}'.");
                continue;
            }

            var dependencyPath = Path.GetFullPath(Path.Combine(pluginDirectory, dependency));
            AddIfExistsInsidePlugin(pluginDirectory, dependencyPath, keep);
        }

        return keep;
    }

    private static void AddDepsReferencedFiles(string pluginDirectory, string depsPath, HashSet<string> keep)
    {
        try
        {
            var json = File.ReadAllText(depsPath);
            var depsRoot = JObject.Parse(json);

            if (depsRoot["targets"] is not JObject targets)
            {
                return;
            }

            foreach (var target in targets.Properties())
            {
                if (target.Value is not JObject libraries)
                {
                    continue;
                }

                foreach (var library in libraries.Properties())
                {
                    if (library.Value is not JObject libraryInfo)
                    {
                        continue;
                    }

                    AddAssetGroupFiles(pluginDirectory, libraryInfo["runtime"], keep);
                    AddAssetGroupFiles(pluginDirectory, libraryInfo["native"], keep);
                    AddAssetGroupFiles(pluginDirectory, libraryInfo["resources"], keep);
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PluginCleaner] Failed to parse deps '{depsPath}': {ex.Message}");
        }
    }

    private static void AddAssetGroupFiles(string pluginDirectory, JToken? assetGroup, HashSet<string> keep)
    {
        if (assetGroup is not JObject group)
        {
            return;
        }

        foreach (var asset in group.Properties())
        {
            if (string.IsNullOrWhiteSpace(asset.Name))
            {
                continue;
            }

            if (!IsSafePluginRelativeFilePath(asset.Name))
            {
                DebugHelper.WriteLine($"[PluginCleaner] Ignoring unsafe deps asset path '{asset.Name}' in '{pluginDirectory}'.");
                continue;
            }

            TryAddRelativeAssetPath(pluginDirectory, asset.Name, keep);
        }
    }

    private static void TryAddRelativeAssetPath(string pluginDirectory, string assetRelativePath, HashSet<string> keep)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(pluginDirectory, assetRelativePath));
            AddIfExistsInsidePlugin(pluginDirectory, fullPath, keep);
        }
        catch
        {
            // Ignore malformed paths in deps metadata
        }
    }

    private static bool IsSafePluginRelativeFilePath(string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || Path.IsPathRooted(assetPath) || assetPath.Contains('\\') || assetPath.Contains(':'))
        {
            return false;
        }

        string[] segments = assetPath.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
        }

        return !string.IsNullOrWhiteSpace(Path.GetFileName(assetPath));
    }

    private static void AddIfExistsInsidePlugin(string pluginDirectory, string fullPath, HashSet<string> keep)
    {
        if (!IsUnderDirectory(fullPath, pluginDirectory))
        {
            return;
        }

        if (File.Exists(fullPath))
        {
            keep.Add(fullPath);
        }
    }

    private static bool IsUnderDirectory(string filePath, string directoryPath)
    {
        var fullFilePath = Path.GetFullPath(filePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDirectoryPath = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var prefix = fullDirectoryPath + Path.DirectorySeparatorChar;
        return fullFilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullFilePath, fullDirectoryPath, StringComparison.OrdinalIgnoreCase);
    }

    private static PluginManifest? LoadManifest(string manifestPath)
    {
        try
        {
            var json = File.ReadAllText(manifestPath);
            return JsonConvert.DeserializeObject<PluginManifest>(json);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PluginCleaner] Failed to read manifest '{manifestPath}': {ex.Message}");
            return null;
        }
    }

    private static void PruneEmptyQuarantineDirectories(string quarantineRoot)
    {
        if (!Directory.Exists(quarantineRoot))
        {
            return;
        }

        try
        {
            foreach (var directory in Directory.GetDirectories(quarantineRoot, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length))
            {
                TryDeleteDirectoryIfEmpty(directory);
            }

            TryDeleteDirectoryIfEmpty(quarantineRoot);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PluginCleaner] Failed pruning quarantine '{quarantineRoot}': {ex.Message}");
        }
    }

    private static void TryDeleteDirectoryIfEmpty(string directory)
    {
        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PluginCleaner] Could not delete empty quarantine directory '{directory}': {ex.Message}");
        }
    }

    private static string EnsureUniqueDestination(string destinationPath)
    {
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);
        var counter = 1;

        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_{counter}{extension}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }
}
