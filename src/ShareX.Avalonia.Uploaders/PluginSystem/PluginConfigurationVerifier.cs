#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

namespace ShareX.Ava.Uploaders.PluginSystem;

/// <summary>
/// Plugin configuration verification status
/// </summary>
public enum PluginVerificationStatus
{
    /// <summary>
    /// Plugin is properly configured (4-6 files in folder)
    /// </summary>
    Valid,

    /// <summary>
    /// Plugin may have minor configuration issues (7-15 files)
    /// </summary>
    Warning,

    /// <summary>
    /// Plugin has critical configuration errors (16+ files, duplicate framework DLLs)
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

        // Find plugin folder
        var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", providerId);
        
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
            result.Message = $"⚠️ Config view may not load - {result.ProblematicFiles.Count} duplicate framework DLL(s) detected";
            result.Issues.Add($"Found {result.ProblematicFiles.Count} duplicate framework assemblies that should be excluded:");
            result.Issues.AddRange(result.ProblematicFiles);
            result.Issues.Add("");
            result.Issues.Add("Fix: Add <ExcludeAssets>runtime</ExcludeAssets> to NuGet package references in .csproj");
        }
        else if (result.FileCount >= 16)
        {
            result.Status = PluginVerificationStatus.Warning;
            result.Message = $"⚠️ Plugin folder has {result.FileCount} files (expected 4-6)";
            result.Issues.Add("Plugin folder contains more files than expected.");
            result.Issues.Add("This may indicate dependency configuration issues.");
        }
        else if (result.FileCount >= 7 && result.FileCount <= 15)
        {
            result.Status = PluginVerificationStatus.Warning;
            result.Message = $"Plugin has {result.FileCount} files (expected 4-6)";
            result.Issues.Add("Plugin folder has slightly more files than expected.");
            result.Issues.Add("Verify that only plugin-specific dependencies are included.");
        }
        else
        {
            result.Status = PluginVerificationStatus.Valid;
            result.Message = $"✓ Plugin properly configured ({result.FileCount} files)";
            result.Issues.Add("Plugin folder contains the expected number of files.");
            result.Issues.Add("No duplicate framework assemblies detected.");
        }

        return result;
    }
}
