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

using Newtonsoft.Json;
using XerahS.Common;
using System.IO.Compression;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Handles packaging and installation of .xsdp plugin files
/// </summary>
public static class PluginPackager
{
    private const string ManifestFileName = "plugin.json";
    private const long MaxPackageSize = 100_000_000; // 100MB

    /// <summary>
    /// Package a plugin directory into a .xsdp archive.
    /// </summary>
    /// <param name="pluginDirectory">Root directory of the plugin.</param>
    /// <param name="outputFilePath">Destination .xsdp file path.</param>
    /// <returns>Path to the created package.</returns>
    public static string Package(string pluginDirectory, string outputFilePath)
    {
        string manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"{ManifestFileName} not found in {pluginDirectory}");
        }

        _ = LoadAndValidateManifest(manifestPath);

        if (File.Exists(outputFilePath))
        {
            File.Delete(outputFilePath);
        }

        ZipFile.CreateFromDirectory(pluginDirectory, outputFilePath);
        DebugHelper.WriteLine($"Plugin packaged: {outputFilePath}");
        return outputFilePath;
    }

    /// <summary>
    /// Extracts a package into the Plugins directory and returns metadata.
    /// </summary>
    /// <param name="packageFilePath">Path to the .xsdp file.</param>
    /// <param name="pluginsDirectory">Root Plugins directory.</param>
    /// <returns>Metadata for the installed plugin.</returns>
    public static PluginMetadata? InstallPackage(string packageFilePath, string pluginsDirectory)
    {
        if (!File.Exists(packageFilePath))
        {
            throw new FileNotFoundException("Package file not found", packageFilePath);
        }

        var fileInfo = new FileInfo(packageFilePath);
        if (fileInfo.Length > MaxPackageSize)
        {
            throw new InvalidDataException($"Package exceeds maximum size of {MaxPackageSize / 1_000_000}MB");
        }

        DebugHelper.WriteLine($"Plugin install requested: {packageFilePath}");

        using var archive = ZipFile.OpenRead(packageFilePath);
        var manifestEntry = archive.GetEntry(ManifestFileName);
        if (manifestEntry == null)
        {
            throw new InvalidDataException($"Package does not contain {ManifestFileName}");
        }

        string manifestJson;
        using (var stream = manifestEntry.Open())
        using (var reader = new StreamReader(stream))
        {
            manifestJson = reader.ReadToEnd();
        }

        var manifest = LoadAndValidateManifestJson(manifestJson);

        string targetDir = Path.Combine(pluginsDirectory, manifest.PluginId);
        if (Directory.Exists(targetDir))
        {
            throw new InvalidOperationException(
                $"Plugin '{manifest.PluginId}' (v{manifest.Version}) is already installed. " +
                "Please uninstall it first or use a different plugin ID.");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"xsdp_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ExtractArchiveSafely(archive, tempDir);

            string assemblyFileName = manifest.GetAssemblyFileName();
            string assemblyPath = Path.Combine(tempDir, assemblyFileName);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Assembly not found: {assemblyFileName}");
            }

            Directory.Move(tempDir, targetDir);

            string finalAssemblyPath = Path.Combine(targetDir, assemblyFileName);
            var metadata = new PluginMetadata(manifest, targetDir, finalAssemblyPath);
            DebugHelper.WriteLine($"Plugin installed: {manifest.Name} v{manifest.Version} to {targetDir}");
            return metadata;
        }
        catch
        {
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignored
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Reads manifest data from a package without installing it.
    /// </summary>
    /// <param name="packageFilePath">Path to the .xsdp package.</param>
    /// <returns>Deserialized manifest or null.</returns>
    public static PluginManifest? PreviewPackage(string packageFilePath)
    {
        if (!File.Exists(packageFilePath))
        {
            return null;
        }

        var fileInfo = new FileInfo(packageFilePath);
        if (fileInfo.Length > MaxPackageSize)
        {
            throw new InvalidDataException($"Package exceeds maximum size of {MaxPackageSize / 1_000_000}MB");
        }

        using var archive = ZipFile.OpenRead(packageFilePath);
        var manifestEntry = archive.GetEntry(ManifestFileName);
        if (manifestEntry == null)
        {
            return null;
        }

        using var stream = manifestEntry.Open();
        using var reader = new StreamReader(stream);
        string json = reader.ReadToEnd();
        return LoadAndValidateManifestJson(json);
    }

    private static PluginManifest LoadAndValidateManifest(string manifestPath)
    {
        string json = File.ReadAllText(manifestPath);
        return LoadAndValidateManifestJson(json);
    }

    private static PluginManifest LoadAndValidateManifestJson(string json)
    {
        var manifest = JsonConvert.DeserializeObject<PluginManifest>(json);

        if (manifest == null)
        {
            throw new InvalidDataException("Failed to deserialize manifest");
        }

        if (!manifest.IsValid(out var error))
        {
            throw new InvalidDataException($"Invalid manifest: {error}");
        }

        return manifest;
    }

    private static void ExtractArchiveSafely(ZipArchive archive, string destinationDirectory)
    {
        string destinationRoot = Path.GetFullPath(destinationDirectory);
        if (!destinationRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            destinationRoot += Path.DirectorySeparatorChar;
        }

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
            {
                continue;
            }

            string targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Package contains an invalid entry path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            string? directoryPath = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            entry.ExtractToFile(targetPath, true);
        }
    }
}
