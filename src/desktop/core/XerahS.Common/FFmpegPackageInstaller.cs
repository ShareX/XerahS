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

using System.Formats.Tar;
using System.IO.Compression;
using SharpCompress.Compressors.Xz;

namespace XerahS.Common;

/// <summary>Extracts BtbN packages into staging, then replaces the complete tool pair with rollback.</summary>
internal static class FFmpegPackageInstaller
{
    private static readonly SemaphoreSlim InstallLock = new(1, 1);

    internal static async Task<string> InstallAsync(string archivePath, string destinationFolder,
        FFmpegArchitecture architecture, CancellationToken token)
    {
        bool windows = architecture is FFmpegArchitecture.win64 or FFmpegArchitecture.winArm64;
        string executable = windows ? "ffmpeg.exe" : "ffmpeg";
        string probe = windows ? "ffprobe.exe" : "ffprobe";
        string[] members = [executable, probe, "LICENSE.txt"];
        string staging = Path.Combine(Path.GetTempPath(), $"xerahs-ffmpeg-extract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            string? packageRoot = null;
            async Task ExtractMember(string name, bool regularFile, Stream content)
            {
                string[] parts = name.Replace('\\', '/').Split('/');
                string? member = parts.Length == 3 && parts[1] == "bin" && (parts[2] == executable || parts[2] == probe)
                    ? parts[2] : parts.Length == 2 && parts[1] == "LICENSE.txt" ? parts[1] : null;
                if (member == null) return;
                if (!regularFile || parts[0] is "" or "." or ".." ||
                    (packageRoot != null && packageRoot != parts[0]) || !found.Add(member))
                    throw new InvalidDataException($"Invalid or duplicate FFmpeg archive member: {name}");
                packageRoot = parts[0];
                string path = Path.Combine(staging, member);
                await using (var output = File.Create(path))
                    await content.CopyToAsync(output, token).ConfigureAwait(false);
                if (new FileInfo(path).Length == 0)
                    throw new InvalidDataException($"Empty FFmpeg archive member: {name}");
                if (!OperatingSystem.IsWindows() && member != "LICENSE.txt")
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(archivePath);
                foreach (var entry in archive.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    using var content = entry.Open();
                    bool regular = (entry.ExternalAttributes >> 16 & 0xF000) is 0 or 0x8000;
                    await ExtractMember(entry.FullName, regular, content).ConfigureAwait(false);
                }
            }
            else if (archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
            {
                await using var input = File.OpenRead(archivePath);
                using var xz = new XZStream(input);
                using var tar = new TarReader(xz);
                while (await tar.GetNextEntryAsync(copyData: false, cancellationToken: token).ConfigureAwait(false) is { } entry)
                {
                    bool regular = entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile;
                    await ExtractMember(entry.Name, regular, entry.DataStream ?? Stream.Null).ConfigureAwait(false);
                }
            }
            else throw new InvalidDataException("Unsupported FFmpeg archive format.");

            if (members.Any(member => !found.Contains(member)))
                throw new InvalidDataException($"The FFmpeg archive must contain bin/{executable}, bin/{probe} and LICENSE.txt.");

            await ReplaceToolsAsync(staging, destinationFolder, members, token).ConfigureAwait(false);
            return Path.Combine(destinationFolder, executable);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static async Task ReplaceToolsAsync(string staging, string destination, string[] members, CancellationToken token)
    {
        await InstallLock.WaitAsync(token).ConfigureAwait(false);
        string transaction = Path.Combine(destination, $".ffmpeg-install-{Guid.NewGuid():N}");
        var replaced = new List<string>();
        var backedUp = new List<string>();
        bool preserveRecoveryFiles = false;
        try
        {
            Directory.CreateDirectory(transaction);
            foreach (string member in members)
            {
                token.ThrowIfCancellationRequested();
                // Stage on the destination volume so renames work across filesystems.
                File.Copy(Path.Combine(staging, member), Path.Combine(transaction, member));
            }
            foreach (string member in members)
            {
                token.ThrowIfCancellationRequested();
                string target = Path.Combine(destination, member);
                if (File.Exists(target))
                {
                    File.Move(target, Path.Combine(transaction, member + ".old"));
                    backedUp.Add(member);
                }
                File.Move(Path.Combine(transaction, member), target);
                replaced.Add(member);
            }
        }
        catch
        {
            try
            {
                foreach (string member in replaced) File.Delete(Path.Combine(destination, member));
                foreach (string member in backedUp) File.Move(Path.Combine(transaction, member + ".old"), Path.Combine(destination, member));
            }
            catch (Exception ex)
            {
                preserveRecoveryFiles = true;
                throw new IOException($"Could not restore FFmpeg tools. Recovery files remain in {transaction}.", ex);
            }
            throw;
        }
        finally
        {
            try { if (!preserveRecoveryFiles) TryDeleteDirectory(transaction); }
            finally { InstallLock.Release(); }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (Exception ex) { DebugHelper.WriteException(ex, "Could not remove the FFmpeg staging folder."); }
    }
}
