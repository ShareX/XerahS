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

using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Helpers;

namespace XerahS.Platform.Linux.Capture.Cli;

/// <summary>
/// Executes CLI-based screen capture (gnome-screenshot, spectacle, scrot, import, xfce4-screenshooter)
/// for region, fullscreen, and active-window. Used as last-resort fallback when Portal/KDE/GNOME/X11 are not available.
/// </summary>
internal static class CliCaptureExecutor
{
    public static async Task<SKBitmap?> TryCaptureAsync(
        LinuxCaptureKind kind,
        string? desktop,
        IWindowService? windowService,
        bool isWayland)
    {
        switch (kind)
        {
            case LinuxCaptureKind.Region:
            {
                var r = await TryCaptureWithDesktopNativeToolAsync(desktop, isWayland).ConfigureAwait(false);
                if (r != null) return r;
                return await TryCaptureWithGenericToolsAsync(desktop, isWayland).ConfigureAwait(false);
            }
            case LinuxCaptureKind.FullScreen:
                return await TryCaptureFullScreenAsync().ConfigureAwait(false);
            case LinuxCaptureKind.ActiveWindow:
                return await TryCaptureActiveWindowAsync(windowService).ConfigureAwait(false);
            default:
                return null;
        }
    }

    private static Task<SKBitmap?> TryCaptureWithDesktopNativeToolAsync(string? desktop, bool isWayland)
    {
        return desktop switch
        {
            "GNOME" or "CINNAMON" or "MATE" => TryToolInteractiveAsync("gnome-screenshot", "-a -f", "gnome-screenshot"),
            "KDE" or "LXQT" => TryToolInteractiveAsync("spectacle", "-b -n -r -o", "spectacle"),
            "XFCE" => TryToolInteractiveAsync("xfce4-screenshooter", "-r -s", "xfce4-screenshooter"),
            _ => Task.FromResult<SKBitmap?>(null)
        };
    }

    private static async Task<SKBitmap?> TryCaptureWithGenericToolsAsync(string? alreadyTriedDesktop, bool isWayland)
    {
        if (alreadyTriedDesktop != "GNOME" && alreadyTriedDesktop != "CINNAMON" && alreadyTriedDesktop != "MATE")
        {
            var r = await TryToolInteractiveAsync("gnome-screenshot", "-a -f", "gnome-screenshot").ConfigureAwait(false);
            if (r != null) return r;
        }
        if (alreadyTriedDesktop != "KDE" && alreadyTriedDesktop != "LXQT")
        {
            var r = await TryToolInteractiveAsync("spectacle", "-b -n -r -o", "spectacle").ConfigureAwait(false);
            if (r != null) return r;
        }
        if (alreadyTriedDesktop != "XFCE")
        {
            var r = await TryToolInteractiveAsync("xfce4-screenshooter", "-r -s", "xfce4-screenshooter").ConfigureAwait(false);
            if (r != null) return r;
        }
        if (!isWayland)
        {
            var r = await TryToolInteractiveAsync("scrot", "-s", "scrot").ConfigureAwait(false);
            if (r != null) return r;
            r = await TryToolInteractiveAsync("import", "", "import").ConfigureAwait(false);
            if (r != null) return r;
        }
        return null;
    }

    private static async Task<SKBitmap?> TryCaptureFullScreenAsync()
    {
        var r = await LinuxCliToolRunner.RunAsync("gnome-screenshot", "-f", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
        if (r != null) return r;
        r = await LinuxCliToolRunner.RunAsync("spectacle", "-b -n -o", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
        if (r != null) return r;
        r = await LinuxCliToolRunner.RunAsync("scrot", "", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
        if (r != null) return r;
        return await LinuxCliToolRunner.RunAsync("import", "-window root", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
    }

    private static async Task<SKBitmap?> TryCaptureActiveWindowAsync(IWindowService? windowService)
    {
        if (windowService == null) return null;

        var handle = windowService.GetForegroundWindow();

        var r = await LinuxCliToolRunner.RunAsync("gnome-screenshot", "-w -b", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
        if (r != null) return r;
        r = await LinuxCliToolRunner.RunAsync("spectacle", "-a -b -n -o", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
        if (r != null) return r;
        r = await LinuxCliToolRunner.RunAsync("scrot", "-u -b", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
        if (r != null) return r;

        if (handle == IntPtr.Zero)
        {
            r = await LinuxCliToolRunner.RunAsync("gnome-screenshot", "-f", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
            if (r != null) return r;
            r = await LinuxCliToolRunner.RunAsync("spectacle", "-b -n -o", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
            if (r != null) return r;
            r = await LinuxCliToolRunner.RunAsync("scrot", "", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
            if (r != null) return r;
            return await LinuxCliToolRunner.RunAsync("import", "-window root", LinuxCliToolRunner.DefaultTimeoutMs).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<SKBitmap?> TryToolInteractiveAsync(string toolName, string argsPrefix, string logName)
    {
        var r = await LinuxCliToolRunner.RunAsync(toolName, argsPrefix, LinuxCliToolRunner.InteractiveTimeoutMs).ConfigureAwait(false);
        if (r != null)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Region captured with {logName}");
        }
        return r;
    }
}
