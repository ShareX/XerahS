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

namespace XerahS.Platform.Linux
{
    internal static class NativeMethods
    {
        private const string libX11 = "libX11.so.6";

        [DllImport(libX11)]
        internal static extern IntPtr XOpenDisplay(string? display);

        [DllImport(libX11)]
        internal static extern int XCloseDisplay(IntPtr display);

        [DllImport(libX11)]
        internal static extern int XGetInputFocus(IntPtr display, out IntPtr focus_return, out int revert_to_return);

        [DllImport(libX11)]
        internal static extern int XSetInputFocus(IntPtr display, IntPtr focus, int revert_to, IntPtr time);

        [DllImport(libX11)]
        internal static extern int XFetchName(IntPtr display, IntPtr w, out IntPtr window_name_return);

        [DllImport(libX11)]
        internal static extern int XFree(IntPtr data);

        [DllImport(libX11)]
        internal static extern int XGetWindowAttributes(IntPtr display, IntPtr w, ref XWindowAttributes window_attributes_return);

        [DllImport(libX11)]
        internal static extern int XQueryTree(IntPtr display, IntPtr w, out IntPtr root_return, out IntPtr parent_return, out IntPtr children_return, out uint nchildren_return);

        [DllImport(libX11)]
        internal static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(libX11)]
        internal static extern int XRaiseWindow(IntPtr display, IntPtr w);

        [DllImport(libX11)]
        internal static extern int XMoveResizeWindow(IntPtr display, IntPtr w, int x, int y, int width, int height);

        [DllImport(libX11)]
        internal static extern int XIconifyWindow(IntPtr display, IntPtr w, int screen_number);

        [DllImport(libX11)]
        internal static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, ulong plane_mask, int format);

        [DllImport(libX11)]
        internal static extern IntPtr XDestroyImage(IntPtr image);

        [DllImport(libX11)]
        internal static extern int XDisplayWidth(IntPtr display, int screen_number);

        [DllImport(libX11)]
        internal static extern int XDisplayHeight(IntPtr display, int screen_number);

        [DllImport(libX11)]
        internal static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window, bool owner_events, int pointer_mode, int keyboard_mode);

        [DllImport(libX11)]
        internal static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window);

        [DllImport(libX11)]
        internal static extern int XSelectInput(IntPtr display, IntPtr w, long event_mask);

        [DllImport(libX11)]
        internal static extern int XNextEvent(IntPtr display, out XEvent event_return);

        [DllImport(libX11)]
        internal static extern int XPending(IntPtr display);

        [DllImport(libX11)]
        internal static extern IntPtr XStringToKeysym(string keySym);

        [DllImport(libX11)]
        internal static extern int XKeysymToKeycode(IntPtr display, IntPtr keySym);

        [DllImport(libX11)]
        internal static extern int XFlush(IntPtr display);

        [DllImport(libX11)]
        internal static extern int XGetClassHint(IntPtr display, IntPtr w, out XClassHint class_hints_return);

        // Window map state
        internal const int IsUnviewable = 0;
        internal const int IsViewable = 1;
        internal const int IsViewableButNotMapped = 2; // Roughly speaking
        internal const int ZPixmap = 2;

        // Key events
        internal const int KeyPress = 2;
        internal const int KeyRelease = 3;

        internal const long KeyPressMask = 1L << 0;

        // Modifier masks (from X11/X.h)
        internal const uint ShiftMask = 1u << 0;
        internal const uint LockMask = 1u << 1;
        internal const uint ControlMask = 1u << 2;
        internal const uint Mod1Mask = 1u << 3;
        internal const uint Mod2Mask = 1u << 4;
        internal const uint Mod3Mask = 1u << 5;
        internal const uint Mod4Mask = 1u << 6;
        internal const uint Mod5Mask = 1u << 7;

        internal const int GrabModeAsync = 1;
        internal const int GrabModeSync = 0;
        internal const int GrabSuccess = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XClassHint
    {
        public IntPtr res_name;
        public IntPtr res_class;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XWindowAttributes
    {
        public int x, y;
        public int width, height;
        public int border_width;
        public int depth;
        public IntPtr visual;
        public IntPtr root;
        public int class_type;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public long backing_planes;
        public long backing_pixel;
        public bool save_under;
        public IntPtr colormap;
        public bool map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
    public bool override_redirect;
    public IntPtr screen;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XImage
{
    public int width;
    public int height;
    public int xoffset;
    public int format;
    public IntPtr data;
    public int byte_order;
    public int bitmap_unit;
    public int bitmap_bit_order;
    public int bitmap_pad;
    public int depth;
    public int bytes_per_line;
    public int bits_per_pixel;
    public ulong red_mask;
    public ulong green_mask;
    public ulong blue_mask;
    public IntPtr obdata;
    public IntPtr funcs;
}

    [StructLayout(LayoutKind.Sequential)]
    internal struct XKeyEvent
    {
        public int type;
        public uint serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public uint time;
        public int x;
        public int y;
        public int x_root;
        public int y_root;
        public uint state;
        public uint keycode;
        public bool same_screen;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct XEvent
    {
        [FieldOffset(0)]
        public int type;

        [FieldOffset(0)]
        public XKeyEvent key;
    }
}
