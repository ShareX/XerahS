namespace XerahS.Common
{
    public static class DesktopIconManager
    {
        public static bool AreDesktopIconsVisible()
        {
            IntPtr hIcons = GetDesktopListViewHandle();

            return hIcons != IntPtr.Zero && NativeMethods.IsWindowVisible(hIcons);
        }

        public static bool SetDesktopIconsVisibility(bool show)
        {
            IntPtr hIcons = GetDesktopListViewHandle();

            if (hIcons != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hIcons, show ? (int)WindowShowStyle.Show : (int)WindowShowStyle.Hide);

                return true;
            }

            return false;
        }

        private static IntPtr GetDesktopListViewHandle()
        {
            IntPtr progman = NativeMethods.FindWindow("Progman", null);
            IntPtr desktopWnd = IntPtr.Zero;

            IntPtr defView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (defView == IntPtr.Zero)
            {
                IntPtr desktopHandle = IntPtr.Zero;

                while ((desktopHandle = NativeMethods.FindWindowEx(IntPtr.Zero, desktopHandle, "WorkerW", null)) != IntPtr.Zero)
                {
                    defView = NativeMethods.FindWindowEx(desktopHandle, IntPtr.Zero, "SHELLDLL_DefView", null);

                    if (defView != IntPtr.Zero)
                    {
                        break;
                    }
                }
            }

            if (defView != IntPtr.Zero)
            {
                IntPtr sysListView = NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
                return sysListView;
            }

            return IntPtr.Zero;
        }
    }
}
