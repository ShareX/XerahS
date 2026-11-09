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

using System.Runtime.InteropServices;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

/// <summary>
/// Restores system cursors left blank by XerahS 0.33.6 and earlier. Those versions replaced every system cursor
/// with a transparent one during capture, so a crash mid-capture left the user without a visible pointer
/// (XerahS issue #288). Capture no longer touches system cursors; only the recovery of that marker remains.
/// </summary>
internal static class SystemCursorGuard
{
    private const uint SPI_SETCURSORS = 0x0057;

    private static string MarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XerahS",
        "system-cursors-hidden.marker");

    /// <summary>Restores system cursors left hidden by a previous XerahS process that crashed mid-capture.</summary>
    public static void RecoverFromPreviousSession()
    {
        try
        {
            if (!File.Exists(MarkerPath))
            {
                return;
            }

            DebugHelper.WriteLine("System cursors were left hidden by a previous session; restoring them.");
            TryRestoreAndClearMarker(MarkerPath, ReloadSystemCursors);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to recover system cursors from a previous session");
        }
    }

    private static bool ReloadSystemCursors()
    {
        try
        {
            if (SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0)) return true;
            DebugHelper.WriteLine($"SystemCursorGuard: cursor restore failed (Win32 error {Marshal.GetLastWin32Error()}); keeping recovery marker.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"SystemCursorGuard: failed to restore cursors. {ex.Message}");
        }
        return false;
    }

    // A failed native restore must remain recoverable on the next launch.
    internal static bool TryRestoreAndClearMarker(string markerPath, Func<bool> reload)
    {
        try
        {
            if (!reload()) return false;
            File.Delete(markerPath);
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"SystemCursorGuard: keeping recovery marker. {ex.Message}");
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
}
