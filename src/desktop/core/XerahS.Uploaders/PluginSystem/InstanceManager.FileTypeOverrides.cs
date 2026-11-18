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

using XerahS.Common;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// File type overrides: an extension mapped to one instance, whatever category the upload is in.
/// </summary>
public partial class InstanceManager
{
    /// <summary>Write time of the settings file when the overrides were last read or written.</summary>
    private DateTime _overridesStampUtc;

    /// <summary>
    /// Re-reads the overrides from the settings file when its write time is newer than our last read or
    /// write, so the CLI and the app never work from, or write over, each other's overrides. The caller
    /// holds the cross-process config lock (so the read never races another process's save) and _lock.
    /// </summary>
    private void ReadFileTypeOverridesWhileLocked()
    {
        try
        {
            string path = ConfigFilePath;
            if (!File.Exists(path)) return;
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            if (stamp <= _overridesStampUtc) return;
            var token = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))["FileTypeOverrides"];
            _configuration.FileTypeOverrides = NormalizeFileTypeOverrides(token?.ToObject<List<FileTypeOverride>>());
            _overridesStampUtc = stamp;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            DebugHelper.WriteLine($"Could not re-read file type overrides: {ex.Message}");
        }
    }

    /// <summary>
    /// For readers: takes the config lock only when the file changed since our last read (the write-time
    /// check opens no handle). If the lock is busy, keeps the list we have. Call under _lock.
    /// </summary>
    private void SyncFileTypeOverridesIfChanged()
    {
        try
        {
            string path = ConfigFilePath;
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) <= _overridesStampUtc) return;
            using var configLock = AcquireConfigLock();
            ReadFileTypeOverridesWhileLocked();
        }
        catch (IOException ex)
        {
            DebugHelper.WriteLine($"Could not lock settings to re-read file type overrides: {ex.Message}");
        }
    }

    private void MarkOverridesSaved()
    {
        try
        {
            _overridesStampUtc = File.GetLastWriteTimeUtc(ConfigFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The overrides, sorted by extension. Copies: changing them does not change settings.</summary>
    public List<FileTypeOverride> GetFileTypeOverrides()
    {
        lock (_lock)
        {
            SyncFileTypeOverridesIfChanged();
            return _configuration.FileTypeOverrides
                .OrderBy(o => o.Extension, StringComparer.Ordinal)
                .Select(o => new FileTypeOverride { Extension = o.Extension, InstanceId = o.InstanceId })
                .ToList();
        }
    }

    /// <summary>
    /// Sends every file with <paramref name="extension"/> to <paramref name="instanceId"/>, replacing any
    /// override the extension already has. Throws when the extension is empty or the instance does not exist.
    /// </summary>
    public void SetFileTypeOverride(string extension, string instanceId)
    {
        var ext = NormalizeOverrideExtension(extension) ?? throw new ArgumentException("An extension is required.", nameof(extension));
        lock (_lock)
        {
            var instance = _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, instanceId))
                ?? throw new ArgumentException($"No uploader instance with ID {instanceId}.", nameof(instanceId));
            // Read, change and write under one cross-process lock, so no other process's edit is lost.
            using var configLock = AcquireConfigLock();
            ReadFileTypeOverridesWhileLocked();
            _configuration.FileTypeOverrides.RemoveAll(o => o.Extension == ext);
            _configuration.FileTypeOverrides.Add(new FileTypeOverride { Extension = ext, InstanceId = instance.InstanceId });
            SaveConfigurationWhileLocked(throwOnError: true, overridesChanged: true);
        }
    }

    /// <summary>Removes the override for <paramref name="extension"/>. Returns false when it had none.</summary>
    public bool RemoveFileTypeOverride(string extension)
    {
        var ext = NormalizeOverrideExtension(extension);
        if (ext == null) return false;
        lock (_lock)
        {
            using var configLock = AcquireConfigLock();
            ReadFileTypeOverridesWhileLocked();
            if (_configuration.FileTypeOverrides.RemoveAll(o => o.Extension == ext) == 0) return false;
            SaveConfigurationWhileLocked(throwOnError: true, overridesChanged: true);
            return true;
        }
    }

    /// <summary>
    /// The instance a file must go to because of a file type override, or null when its extension has
    /// none. The longest matching extension wins, so "archive.tar.gz" matches "tar.gz" before "gz".
    /// An override whose instance is unavailable is ignored, and the reason is logged.
    /// </summary>
    public UploaderInstance? GetFileTypeOverrideInstance(string? fileName) => FindFileTypeOverride(fileName)?.Instance;

    /// <summary>The override that applies to <paramref name="fileName"/>, with its instance, for explaining a route.</summary>
    public (FileTypeOverride Override, UploaderInstance Instance)? FindFileTypeOverride(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        string name = Path.GetFileName(fileName.Trim()).ToLowerInvariant();
        lock (_lock)
        {
            SyncFileTypeOverridesIfChanged();
            var match = _configuration.FileTypeOverrides
                .Where(o => name.Length > o.Extension.Length + 1 && name.EndsWith("." + o.Extension, StringComparison.Ordinal))
                .OrderByDescending(o => o.Extension.Length)
                .FirstOrDefault();
            if (match == null) return null;

            var instance = _configuration.Instances.FirstOrDefault(i => InstanceIdsEqual(i.InstanceId, match.InstanceId));
            if (instance == null || !instance.IsAvailable)
            {
                DebugHelper.WriteLine(
                    $"File type override for .{match.Extension} points to {(instance == null ? "a removed instance" : $"unavailable {instance.DisplayName}")} ({match.InstanceId}); ignoring it.");
                return null;
            }

            return (new FileTypeOverride { Extension = match.Extension, InstanceId = match.InstanceId }, instance);
        }
    }

    /// <summary>
    /// The extension a renamed upload must keep so it still matches its override: a multi-part override
    /// extension such as ".tar.gz" when one matches, else <see cref="Path.GetExtension(string?)"/>.
    /// </summary>
    public string GetExtensionToKeep(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
        string name = Path.GetFileName(fileName.Trim());
        lock (_lock)
        {
            SyncFileTypeOverridesIfChanged();
            // Compare on the name itself so the slice below uses the same lengths (lower-casing can change them).
            var multi = _configuration.FileTypeOverrides
                .Where(o => o.Extension.Contains('.') && name.Length > o.Extension.Length + 1 && name.EndsWith("." + o.Extension, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.Extension.Length)
                .FirstOrDefault();
            return multi != null ? name[^(multi.Extension.Length + 1)..] : Path.GetExtension(name);
        }
    }

    /// <summary>The override listed for <paramref name="fileName"/>, whether or not its instance is usable.</summary>
    public FileTypeOverride? GetListedFileTypeOverride(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        string name = Path.GetFileName(fileName.Trim()).ToLowerInvariant();
        lock (_lock)
        {
            SyncFileTypeOverridesIfChanged();
            return _configuration.FileTypeOverrides
                .Where(o => name.Length > o.Extension.Length + 1 && name.EndsWith("." + o.Extension, StringComparison.Ordinal))
                .OrderByDescending(o => o.Extension.Length)
                .Select(o => new FileTypeOverride { Extension = o.Extension, InstanceId = o.InstanceId })
                .FirstOrDefault();
        }
    }

    private void RemoveFileTypeOverridesFor(string instanceId)
    {
        int removed = _configuration.FileTypeOverrides.RemoveAll(o => InstanceIdsEqual(o.InstanceId, instanceId));
        if (removed > 0)
        {
            DebugHelper.WriteLine($"Removed {removed} file type override(s) for removed instance {instanceId}.");
        }
    }

    /// <summary>
    /// Lower case, no leading dot; keeps inner dots ("tar.gz"). Null for empty input or input with path
    /// separators or wildcards.
    /// </summary>
    public static string? NormalizeOverrideExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return null;
        string ext = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (ext.Length == 0 || ext.IndexOfAny(['/', '\\', '*', '?', ' ']) >= 0 || ext.EndsWith('.')) return null;
        return ext;
    }

    private static List<FileTypeOverride> NormalizeFileTypeOverrides(List<FileTypeOverride>? overrides)
    {
        var result = new List<FileTypeOverride>();
        foreach (var o in overrides ?? [])
        {
            var ext = NormalizeOverrideExtension(o?.Extension);
            if (ext == null || string.IsNullOrWhiteSpace(o!.InstanceId)) continue;
            // One override per extension; the last saved wins, as SetFileTypeOverride replaces.
            result.RemoveAll(r => r.Extension == ext);
            result.Add(new FileTypeOverride { Extension = ext, InstanceId = o.InstanceId });
        }

        return result;
    }
}
