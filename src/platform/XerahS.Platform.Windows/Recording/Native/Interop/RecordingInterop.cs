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

namespace XerahS.Platform.Windows.Recording.Native;

internal static unsafe partial class RecordingInterop
{
    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern HRESULT CoInitializeEx(void* reserved, COINIT flags);

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern void CoUninitialize();

    [DllImport("d3d11.dll", ExactSpelling = true)]
    public static extern HRESULT CreateDirect3D11DeviceFromDXGIDevice(nint device, nint* inspectable);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern HRESULT WindowsCreateString(char* value, uint length, nint* result);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern HRESULT WindowsDeleteString(nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern HRESULT RoGetActivationFactory(nint className, Guid* iid, void** factory);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(nint dc, RECT* clip, delegate* unmanaged[Stdcall]<nint, nint, RECT*, nint, BOOL> callback, nint state);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern nint MonitorFromWindow(nint window, MONITOR_FROM_FLAGS flags);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint window, RECT* rect);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(nint window);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    public static extern HRESULT DwmGetWindowAttribute(nint window, uint attribute, void* value, uint size);

    /// <summary>Visible window bounds in physical desktop pixels (DWM extended frame, excluding the drop shadow).</summary>
    public static bool TryGetWindowBounds(nint window, out System.Drawing.Rectangle bounds)
    {
        const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        RECT rect;
        if (DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT)).Failed && !GetWindowRect(window, &rect))
        {
            bounds = default;
            return false;
        }
        bounds = System.Drawing.Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    [DllImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

    [DllImport("avrt.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AvRevertMmThreadCharacteristics(nint task);

    [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", ExactSpelling = true, SetLastError = true)]
    public static extern nint CreateWaitableTimerEx(void* attributes, char* name, uint flags, uint access);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWaitableTimerEx(nint timer, long* dueTime, int period, void* completionRoutine, void* argument, void* wakeContext, uint tolerableDelay);
}
