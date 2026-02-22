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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using XerahS.Common;

namespace XerahS.Platform.Linux.Capture;

internal static class PortalScreenshotFallback
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    public static async Task<SKBitmap?> TryFindScreenshotAsync(DateTime requestStartUtc, TimeSpan timeout, string logPrefix)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var candidate = FindNewestImageFile(requestStartUtc);
            if (candidate != null)
            {
                try
                {
                    using var stream = File.OpenRead(candidate.FullName);
                    var bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        DebugHelper.WriteLine($"{logPrefix}: Portal fallback found screenshot file: {candidate.FullName}");
                        TryDeleteIfTemporary(candidate.FullName);
                        return bitmap;
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, $"{logPrefix}: Portal fallback failed to decode candidate file.");
                }
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        DebugHelper.WriteLine($"{logPrefix}: Portal fallback did not find a screenshot file.");
        return null;
    }

    private static FileInfo? FindNewestImageFile(DateTime minUtc)
    {
        FileInfo? newest = null;
        foreach (var dir in GetCandidateDirectories())
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            try
            {
                foreach (var path in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var ext = Path.GetExtension(path);
                    if (!ImageExtensions.Contains(ext))
                    {
                        continue;
                    }

                    var info = new FileInfo(path);
                    var lastWriteUtc = info.LastWriteTimeUtc;
                    if (lastWriteUtc < minUtc)
                    {
                        continue;
                    }

                    if (newest == null || lastWriteUtc > newest.LastWriteTimeUtc)
                    {
                        newest = info;
                    }
                }
            }
            catch
            {
                // Best-effort fallback; ignore scan failures.
            }
        }

        return newest;
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDir))
        {
            dirs.Add(runtimeDir);
        }

        dirs.Add("/tmp");

        var picturesDir = ResolvePicturesDirectory();
        if (!string.IsNullOrWhiteSpace(picturesDir))
        {
            dirs.Add(picturesDir);
            dirs.Add(Path.Combine(picturesDir, "Screenshots"));
        }

        return dirs;
    }

    private static string ResolvePicturesDirectory()
    {
        var special = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (!string.IsNullOrWhiteSpace(special))
        {
            return special;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return string.Empty;
        }

        var configPath = Path.Combine(home, ".config", "user-dirs.dirs");
        if (!File.Exists(configPath))
        {
            return Path.Combine(home, "Pictures");
        }

        try
        {
            foreach (var line in File.ReadLines(configPath))
            {
                if (!line.StartsWith("XDG_PICTURES_DIR", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var raw = parts[1].Trim().Trim('"');
                var expanded = raw.Replace("$HOME", home, StringComparison.Ordinal);
                if (!string.IsNullOrWhiteSpace(expanded))
                {
                    return expanded;
                }
            }
        }
        catch
        {
        }

        return Path.Combine(home, "Pictures");
    }

    private static void TryDeleteIfTemporary(string path)
    {
        try
        {
            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? string.Empty;
            if (path.StartsWith("/tmp/", StringComparison.Ordinal) ||
                (!string.IsNullOrWhiteSpace(runtimeDir) && path.StartsWith(runtimeDir, StringComparison.Ordinal)))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
