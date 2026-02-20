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

using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Capture.Gnome;

internal static class GnomeDbusScreenCapture
{
    private const string GnomeShellScreenshotBusName = "org.gnome.Shell.Screenshot";
    private static readonly ObjectPath GnomeShellScreenshotObjectPath = new("/org/gnome/Shell/Screenshot");

    public static async Task<SKBitmap?> CaptureFullScreenAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_gnome_{Guid.NewGuid():N}.png");
        var cleanupPaths = new HashSet<string>(StringComparer.Ordinal) { tempFile };

        try
        {
            using var connection = new Connection(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);
            var proxy = connection.CreateProxy<IGnomeShellScreenshot>(GnomeShellScreenshotBusName, GnomeShellScreenshotObjectPath);
            var (success, filenameUsed) = await proxy.ScreenshotAsync(false, false, tempFile).ConfigureAwait(false);
            if (!success) return null;
            return await TryLoadBitmapFromPathAsync(tempFile, filenameUsed, cleanupPaths).ConfigureAwait(false);
        }
        catch (DBusException ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell full-screen D-Bus capture failed: {ex.ErrorName} ({ex.ErrorMessage})");
            return null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell full-screen D-Bus capture failed: {ex.Message}");
            return null;
        }
        finally
        {
            CleanupFiles(cleanupPaths);
        }
    }

    public static async Task<SKBitmap?> CaptureWindowAsync(CaptureOptions? options)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_gnome_window_{Guid.NewGuid():N}.png");
        var cleanupPaths = new HashSet<string>(StringComparer.Ordinal) { tempFile };

        try
        {
            using var connection = new Connection(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);
            var proxy = connection.CreateProxy<IGnomeShellScreenshot>(GnomeShellScreenshotBusName, GnomeShellScreenshotObjectPath);
            var includeCursor = options?.ShowCursor == true;
            var (success, filenameUsed) = await proxy.ScreenshotWindowAsync(include_frame: true, include_cursor: includeCursor, flash: false, filename: tempFile).ConfigureAwait(false);
            if (!success) return null;
            return await TryLoadBitmapFromPathAsync(tempFile, filenameUsed, cleanupPaths).ConfigureAwait(false);
        }
        catch (DBusException ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell window D-Bus capture failed: {ex.ErrorName} ({ex.ErrorMessage})");
            return null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell window D-Bus capture failed: {ex.Message}");
            return null;
        }
        finally
        {
            CleanupFiles(cleanupPaths);
        }
    }

    public static async Task<SKBitmap?> CaptureRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_gnome_region_{Guid.NewGuid():N}.png");
        var cleanupPaths = new HashSet<string>(StringComparer.Ordinal) { tempFile };

        try
        {
            using var connection = new Connection(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);
            var proxy = connection.CreateProxy<IGnomeShellScreenshot>(GnomeShellScreenshotBusName, GnomeShellScreenshotObjectPath);

            int x, y, width, height;
            try
            {
                (x, y, width, height) = await proxy.SelectAreaAsync().ConfigureAwait(false);
            }
            catch (DBusException ex) when (IsLikelyUserCancelled(ex))
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: GNOME Shell SelectArea cancelled by user.");
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: GNOME Shell SelectArea returned invalid region.");
                return null;
            }

            var (success, filenameUsed) = await proxy.ScreenshotAreaAsync(x, y, width, height, flash: false, filename: tempFile).ConfigureAwait(false);
            if (!success) return null;
            return await TryLoadBitmapFromPathAsync(tempFile, filenameUsed, cleanupPaths).ConfigureAwait(false);
        }
        catch (DBusException ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell region D-Bus capture failed: {ex.ErrorName} ({ex.ErrorMessage})");
            return null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: GNOME Shell region D-Bus capture failed: {ex.Message}");
            return null;
        }
        finally
        {
            CleanupFiles(cleanupPaths);
        }
    }

    private static bool IsLikelyUserCancelled(DBusException ex)
    {
        return ex.ErrorName.Contains("Cancel", StringComparison.OrdinalIgnoreCase) ||
               ex.ErrorMessage.Contains("cancel", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<SKBitmap?> TryLoadBitmapFromPathAsync(string primaryPath, string? alternatePath, HashSet<string> cleanupPaths)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(primaryPath)) candidates.Add(primaryPath);
        if (!string.IsNullOrWhiteSpace(alternatePath)) candidates.Add(alternatePath);

        foreach (var candidate in candidates)
        {
            cleanupPaths.Add(candidate);
            if (!File.Exists(candidate)) continue;
            await using var stream = File.OpenRead(candidate);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null) return bitmap;
        }
        return null;
    }

    private static void CleanupFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    [DBusInterface("org.gnome.Shell.Screenshot")]
    internal interface IGnomeShellScreenshot : IDBusObject
    {
        Task<(bool success, string filename)> ScreenshotAsync(bool include_cursor, bool flash, string filename);
        Task<(bool success, string filename)> ScreenshotWindowAsync(bool include_frame, bool include_cursor, bool flash, string filename);
        Task<(bool success, string filename)> ScreenshotAreaAsync(int x, int y, int width, int height, bool flash, string filename);
        Task<(int x, int y, int width, int height)> SelectAreaAsync();
    }
}
