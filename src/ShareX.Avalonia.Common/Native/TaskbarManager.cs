using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XerahS.Common.Native
{
    public enum TaskbarProgressBarStatus
    {
        NoProgress = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        Paused = 0x8
    }

    public static class TaskbarManager
    {
        [ComImport, Guid("c43dc798-95d1-4bea-9030-bb99e2983a1a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList4
        {
            // ITaskbarList
            [PreserveSig]
            void HrInit();

            [PreserveSig]
            void AddTab(IntPtr hwnd);

            [PreserveSig]
            void DeleteTab(IntPtr hwnd);

            [PreserveSig]
            void ActivateTab(IntPtr hwnd);

            [PreserveSig]
            void SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            [PreserveSig]
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ITaskbarList3
            [PreserveSig]
            void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);

            [PreserveSig]
            void SetProgressState(IntPtr hwnd, TaskbarProgressBarStatus tbpFlags);
        }

        [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
        private class CTaskbarList
        {
        }

        private static readonly object syncLock = new object();

        private static ITaskbarList4 taskbarList;

        private static ITaskbarList4 TaskbarList
        {
            get
            {
                if (taskbarList == null)
                {
                    lock (syncLock)
                    {
                        if (taskbarList == null)
                        {
                            taskbarList = (ITaskbarList4)new CTaskbarList();
                            taskbarList.HrInit();
                        }
                    }
                }

                return taskbarList;
            }
        }

        private static IntPtr mainWindowHandle;

        private static IntPtr MainWindowHandle
        {
            get
            {
                if (mainWindowHandle == IntPtr.Zero)
                {
                    Process currentProcess = Process.GetCurrentProcess();

                    if (currentProcess == null || currentProcess.MainWindowHandle == IntPtr.Zero)
                    {
                        mainWindowHandle = IntPtr.Zero;
                    }
                    else
                    {
                        mainWindowHandle = currentProcess.MainWindowHandle;
                    }
                }

                return mainWindowHandle;
            }
        }

        public static bool Enabled { get; set; } = true;

        public static bool IsPlatformSupported
        {
            get
            {
                // Simple OS version check or RuntimeInformation.IsOSPlatform check
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version.Major >= 6 && Environment.OSVersion.Version.Minor >= 1;
            }
        }

        private static void SetProgressValue(IntPtr hwnd, int currentValue, int maximumValue = 100)
        {
            if (Enabled && IsPlatformSupported && hwnd != IntPtr.Zero)
            {
                currentValue = currentValue.Clamp(0, maximumValue);

                try
                {
                    TaskbarList.SetProgressValue(hwnd, Convert.ToUInt32(currentValue), Convert.ToUInt32(maximumValue));
                }
                catch (FileNotFoundException)
                {
                    Enabled = false;
                }
                catch (COMException)
                {
                    // Ignore COM exceptions
                }
            }
        }

        public static void SetProgressValue(int currentValue, int maximumValue = 100)
        {
            SetProgressValue(MainWindowHandle, currentValue, maximumValue);
        }

        // TODO: [Avalonia] Port SetProgressValue(Window window, ...) - Requires retrieving window handle from Avalonia Window

        /*
        public static void SetProgressValue(Form form, int currentValue, int maximumValue = 100)
        {
            form.InvokeSafe(() => SetProgressValue(form.Handle, currentValue, maximumValue));
        }
        */

        private static void SetProgressState(IntPtr hwnd, TaskbarProgressBarStatus state)
        {
            if (Enabled && IsPlatformSupported && hwnd != IntPtr.Zero)
            {
                try
                {
                    TaskbarList.SetProgressState(hwnd, state);
                }
                catch (FileNotFoundException)
                {
                    Enabled = false;
                }
                catch (COMException)
                {
                    // Ignore COM exceptions
                }
            }
        }

        public static void SetProgressState(TaskbarProgressBarStatus state)
        {
            SetProgressState(MainWindowHandle, state);
        }

        // TODO: [Avalonia] Port SetProgressState(Window window, ...)

        /*
        public static void SetProgressState(Form form, TaskbarProgressBarStatus state)
        {
            form.InvokeSafe(() => SetProgressState(form.Handle, state));
        }
        */
    }
}
