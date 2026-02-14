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

#nullable disable
using XerahS.Common;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Plugin configuration verification status
/// </summary>
public enum PluginVerificationStatus
{
    /// <summary>
    /// Plugin is properly configured (3 files in folder)
    /// </summary>
    Valid,

    /// <summary>
    /// Plugin may have minor configuration issues (any file count outside 3)
    /// </summary>
    Warning,

    /// <summary>
    /// Plugin has critical configuration errors (duplicate framework DLLs)
    /// </summary>
    Error
}

/// <summary>
/// Result of plugin configuration verification
/// </summary>
public class PluginVerificationResult
{
    public PluginVerificationStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new();
    public int FileCount { get; set; }
    public List<string> ProblematicFiles { get; set; } = new();
}

/// <summary>
/// Verifies plugin folder configuration to detect duplicate framework DLLs
/// </summary>
public static class PluginConfigurationVerifier
{
    private static readonly string[] ProblematicDlls = new[]
    {
        "Avalonia.Base.dll",
        "Avalonia.Controls.dll",
        "Avalonia.Themes.Fluent.dll",
        "Avalonia.Markup.Xaml.dll",
        "Avalonia.Markup.dll",
        "CommunityToolkit.Mvvm.dll",
        "Newtonsoft.Json.dll"
    };

    /// <summary>
    /// Verifies a plugin's folder configuration
    /// </summary>
    /// <param name="providerId">The plugin provider ID</param>
    /// <returns>Verification result</returns>
    public static PluginVerificationResult VerifyPluginConfiguration(string providerId)
    {
        var result = new PluginVerificationResult();

        // Custom uploaders are single .sxcu files, not plugin folders - skip verification
        if (providerId.StartsWith("custom_", StringComparison.OrdinalIgnoreCase))
        {
            result.Status = PluginVerificationStatus.Valid;
            result.Message = "Custom uploader (.sxcu file)";
            result.Issues.Add("Custom uploaders are single JSON files and do not require folder verification.");
            return result;
        }

        // Find plugin folder
        var pluginsPath = Path.Combine(PathsManager.PluginsFolder, providerId);

        if (!Directory.Exists(pluginsPath))
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = "Plugin folder not found";
            result.Issues.Add($"Plugin folder does not exist: {pluginsPath}");
            return result;
        }

        // Count files (excluding subdirectories like runtimes/)
        var files = Directory.GetFiles(pluginsPath, "*.*", SearchOption.TopDirectoryOnly);
        result.FileCount = files.Length;

        // Check for problematic DLLs
        foreach (var dll in ProblematicDlls)
        {
            if (files.Any(f => Path.GetFileName(f).Equals(dll, StringComparison.OrdinalIgnoreCase)))
            {
                result.ProblematicFiles.Add(dll);
            }
        }

        // Determine status based on file count and problematic files
        if (result.ProblematicFiles.Count > 0)
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = $"\u26A0\uFE0F Config view may not load - {result.ProblematicFiles.Count} duplicate framework DLL(s) detected";
            result.Issues.Add($"Found {result.ProblematicFiles.Count} duplicate framework assemblies in the plugin folder:");
            result.Issues.AddRange(result.ProblematicFiles);
            result.Issues.Add("");
            result.Issues.Add("Fix: Delete these duplicate DLLs from the plugin folder, then restart the app.");
        }
        else if (result.FileCount == 3)
        {
            result.Status = PluginVerificationStatus.Valid;
            result.Message = $"\u2713 Plugin properly configured ({result.FileCount} files)";
            result.Issues.Add("Plugin folder contains the expected number of files.");
            result.Issues.Add("No duplicate framework assemblies detected.");
        }
        else
        {
            result.Status = PluginVerificationStatus.Warning;
            result.Message = $"\u26A0\uFE0F Plugin folder has {result.FileCount} files (expected 3)";
            result.Issues.Add("Plugin folder contains an unexpected number of files.");
            result.Issues.Add("Verify that only plugin-specific dependencies are included.");
        }

        return result;
    }

    /// <summary>
    /// Cleans duplicate framework DLLs from a plugin folder
    /// </summary>
    /// <param name="providerId">The plugin provider ID</param>
    /// <returns>Number of files deleted</returns>
    public static int CleanDuplicateFrameworkDlls(string providerId)
    {
        var pluginsPath = Path.Combine(PathsManager.PluginsFolder, providerId);

        if (!Directory.Exists(pluginsPath))
        {
            return 0;
        }

        int deletedCount = 0;
        var files = Directory.GetFiles(pluginsPath, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var dll in ProblematicDlls)
        {
            var filePath = Path.Combine(pluginsPath, dll);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                    Common.DebugHelper.WriteLine($"[PluginVerifier] Deleted duplicate DLL: {dll}");
                }
                catch (Exception ex)
                {
                    Common.DebugHelper.WriteException(ex, $"Failed to delete {dll}");
                }
            }
        }

        // Also clean up other Avalonia-related DLLs that might be duplicates
        var avaloniaPatterns = new[] { "Avalonia.*.dll", "MicroCom.*.dll" };
        foreach (var pattern in avaloniaPatterns)
        {
            var matchingFiles = Directory.GetFiles(pluginsPath, pattern, SearchOption.TopDirectoryOnly);
            foreach (var file in matchingFiles)
            {
                var fileName = Path.GetFileName(file);
                // Skip the main Avalonia.dll if it's in the list already
                if (ProblematicDlls.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    continue; // Already handled above
                }

                // Delete other Avalonia assemblies
                try
                {
                    File.Delete(file);
                    deletedCount++;
                    Common.DebugHelper.WriteLine($"[PluginVerifier] Deleted Avalonia-related DLL: {fileName}");
                }
                catch (Exception ex)
                {
                    Common.DebugHelper.WriteException(ex, $"Failed to delete {fileName}");
                }
            }
        }

        return deletedCount;
    }
}

