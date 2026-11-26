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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;

namespace XerahS.UI.Helpers;

/// <summary>
/// Silent Run on Linux X11/XWayland: keeps the hidden main window off screen.
/// Seen on Fedora with GNOME 51 (Mutter, XWayland): after a focused task-mode editor closed,
/// the hidden main window was mapped as an empty, transparent frame that stayed on screen,
/// while Avalonia still reported IsVisible == false (so its own Hide() is a no-op). The exact
/// trigger in the window manager is not confirmed. This guard withdraws the X window directly
/// (ICCCM 4.1.4) whenever Avalonia says the main window is hidden but X says it is viewable.
/// A window the user opened (tray, app menu, dialogs) is visible to Avalonia and left alone.
/// </summary>
public static class SilentRunMainWindowGuard
{
    private const int IsViewable = 2;
    private const int MapStateOffset = 92; // XWindowAttributes.map_state on LP64 (x86_64, arm64)
    private static readonly int[] CheckDelaysMs = [150, 600, 2000];

    public static void ScheduleCheck(string reason)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            foreach (int delay in CheckDelaysMs)
            {
                DispatcherTimer.RunOnce(() => Check(reason), TimeSpan.FromMilliseconds(delay));
            }
        });
    }

    private static void Check(string reason)
    {
        try
        {
            if (!SettingsManager.Settings.SilentRun ||
                Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } main } ||
                main.IsVisible)
            {
                return; // Shown on purpose (tray Open, app menu, dialogs): leave it alone.
            }

            if (main.TryGetPlatformHandle() is not { HandleDescriptor: "XID", Handle: var xid } || xid == IntPtr.Zero)
            {
                return;
            }

            if (WithdrawIfViewable(xid))
            {
                DebugHelper.WriteLine($"SilentRun guard ({reason}): hidden main window was mapped by the window manager; withdrew it.");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "SilentRun guard failed");
        }
    }

    private static bool WithdrawIfViewable(IntPtr window)
    {
        IntPtr display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr attributes = Marshal.AllocHGlobal(256);
            try
            {
                if (XGetWindowAttributes(display, window, attributes) == 0 ||
                    Marshal.ReadInt32(attributes, MapStateOffset) != IsViewable)
                {
                    return false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(attributes);
            }

            XUnmapWindow(display, window);

            // Synthetic UnmapNotify to the root so the window manager withdraws (unmanages) it.
            IntPtr root = XDefaultRootWindow(display);
            IntPtr ev = Marshal.AllocHGlobal(24 * IntPtr.Size);
            try
            {
                for (int i = 0; i < 24; i++)
                {
                    Marshal.WriteIntPtr(ev, i * IntPtr.Size, IntPtr.Zero);
                }

                Marshal.WriteInt32(ev, 0, 18);                      // type = UnmapNotify
                Marshal.WriteInt32(ev, 2 * IntPtr.Size, 1);         // send_event = True
                Marshal.WriteIntPtr(ev, 4 * IntPtr.Size, root);     // event
                Marshal.WriteIntPtr(ev, 5 * IntPtr.Size, window);   // window
                XSendEvent(display, root, 0, (IntPtr)((1L << 20) | (1L << 19)), ev);
            }
            finally
            {
                Marshal.FreeHGlobal(ev);
            }

            XSync(display, 0);
            return true;
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    private const string LibX11 = "libX11.so.6";

    [DllImport(LibX11)]
    private static extern IntPtr XOpenDisplay(IntPtr name);

    [DllImport(LibX11)]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, IntPtr attributes);

    [DllImport(LibX11)]
    private static extern int XUnmapWindow(IntPtr display, IntPtr window);

    [DllImport(LibX11)]
    private static extern int XSendEvent(IntPtr display, IntPtr window, int propagate, IntPtr eventMask, IntPtr ev);

    [DllImport(LibX11)]
    private static extern int XSync(IntPtr display, int discard);
}
