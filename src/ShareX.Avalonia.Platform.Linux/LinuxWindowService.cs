using ShareX.Ava.Platform.Abstractions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ShareX.Ava.Platform.Linux
{
    public class LinuxWindowService : IWindowService, IDisposable
    {
        private readonly IntPtr _display;
        private readonly IntPtr _rootWindow;

        public LinuxWindowService()
        {
            try
            {
                _display = NativeMethods.XOpenDisplay(null);
                if (_display != IntPtr.Zero)
                {
                    _rootWindow = NativeMethods.XDefaultRootWindow(_display);
                }
            }
            catch (Exception)
            {
                // Fallback or log if X11 is not available (e.g. pure Wayland)
                _display = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_display != IntPtr.Zero)
            {
                NativeMethods.XCloseDisplay(_display);
            }
        }

        public IntPtr GetForegroundWindow()
        {
            if (_display == IntPtr.Zero) return IntPtr.Zero;
            NativeMethods.XGetInputFocus(_display, out IntPtr focus, out int revert_to);
             return focus;
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return false;
            NativeMethods.XSetInputFocus(_display, handle, 1 /* RevertToParent */, IntPtr.Zero /* CurrentTime */);
            NativeMethods.XRaiseWindow(_display, handle);
            return true;
        }

        public string GetWindowText(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return string.Empty;
            if (NativeMethods.XFetchName(_display, handle, out IntPtr namePtr) != 0 && namePtr != IntPtr.Zero)
            {
                try
                {
                    return Marshal.PtrToStringAnsi(namePtr) ?? string.Empty;
                }
                finally
                {
                    NativeMethods.XFree(namePtr);
                }
            }
            return string.Empty;
        }

        public string GetWindowClassName(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return string.Empty;
            if (NativeMethods.XGetClassHint(_display, handle, out XClassHint hint) != 0)
            {
                string resClass = Marshal.PtrToStringAnsi(hint.res_class) ?? string.Empty;
                if (hint.res_class != IntPtr.Zero) NativeMethods.XFree(hint.res_class);
                if (hint.res_name != IntPtr.Zero) NativeMethods.XFree(hint.res_name);
                return resClass;
            }
            return string.Empty;
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return Rectangle.Empty;
            var attrs = new XWindowAttributes();
            if (NativeMethods.XGetWindowAttributes(_display, handle, ref attrs) != 0)
            {
                return new Rectangle(attrs.x, attrs.y, attrs.width, attrs.height);
            }
            return Rectangle.Empty;
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
           return GetWindowBounds(handle);
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return false;
            var attrs = new XWindowAttributes();
            if (NativeMethods.XGetWindowAttributes(_display, handle, ref attrs) != 0)
            {
                return attrs.map_state == NativeMethods.IsViewable;
            }
            return false;
        }

        public bool IsWindowMaximized(IntPtr handle) 
        {
            // Not implemented in MVP
            return false; 
        }

        public bool IsWindowMinimized(IntPtr handle) 
        { 
             // Not implemented in MVP
             return false;
        }

        public bool ShowWindow(IntPtr handle, int cmdShow)
        {
            if (_display == IntPtr.Zero) return false;
             
             if (cmdShow == 0)
                NativeMethods.XIconifyWindow(_display, handle, 0);
             else
                NativeMethods.XRaiseWindow(_display, handle);
             
             return true;
        }

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
        {
            if (_display == IntPtr.Zero) return false;
            NativeMethods.XMoveResizeWindow(_display, handle, x, y, width, height);
            return true;
        }

        public WindowInfo[] GetAllWindows()
        {
            if (_display == IntPtr.Zero) return Array.Empty<WindowInfo>();
            
             var list = new List<WindowInfo>();
             if (NativeMethods.XQueryTree(_display, _rootWindow, out IntPtr root, out IntPtr parent, out IntPtr children, out uint nchildren) != 0)
             {
                 if (nchildren > 0 && children != IntPtr.Zero)
                 {
                     IntPtr[] windowHandles = new IntPtr[nchildren];
                     Marshal.Copy(children, windowHandles, 0, (int)nchildren);
                     NativeMethods.XFree(children); // Must free the list

                     // In X11, children are ordered bottom-to-top.
                     // Reverse to get Z-order top-to-bottom if needed, but GetAllWindows usually doesn't imply order.
                     
                     foreach(var handle in windowHandles)
                     {
                         var bounds = GetWindowBounds(handle);
                         // Basic filter: Check if visible? Or just return all?
                         // Windows implementation returns active only? No, it returns foreground.
                         // But intent is all.
                         
                         list.Add(new WindowInfo {
                             Handle = handle,
                             Title = GetWindowText(handle),
                             ClassName = GetWindowClassName(handle),
                             Bounds = bounds,
                             IsVisible = IsWindowVisible(handle)
                         });
                      }
                 }
             }
             return list.ToArray();
        }

        public uint GetWindowProcessId(IntPtr handle)
        {
             return 0;
        }
    }
}
