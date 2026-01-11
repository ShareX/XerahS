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

        // Window map state
        internal const int IsUnviewable = 0;
        internal const int IsViewable = 1;
        internal const int IsViewableButNotMapped = 2; // Roughly speaking

        [DllImport(libX11)]
        internal static extern int XGetClassHint(IntPtr display, IntPtr w, out XClassHint class_hints_return);
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
}
