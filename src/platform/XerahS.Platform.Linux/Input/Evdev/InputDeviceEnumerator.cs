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
using System.Runtime.InteropServices;
using System.Text;
using XerahS.Common;

namespace XerahS.Platform.Linux.Input.Evdev;

/// <summary>
/// Enumerates and classifies <c>/dev/input/event*</c> devices using evdev ioctls.
/// Classification mirrors the CrossMacro approach: capability bitmaps decide whether
/// a device is a keyboard or pointer.
/// </summary>
internal static class InputDeviceEnumerator
{
    private const string InputDirectory = "/dev/input";

    public static bool InputDirectoryExists()
    {
        try
        {
            return Directory.Exists(InputDirectory);
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<InputDeviceInfo> Enumerate()
    {
        var devices = new List<InputDeviceInfo>();

        if (!InputDirectoryExists())
        {
            return devices;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(InputDirectory, "event*");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "InputDeviceEnumerator: Failed to list /dev/input");
            return devices;
        }

        foreach (var file in files)
        {
            try
            {
                var device = Inspect(file);
                if (device != null)
                {
                    devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"InputDeviceEnumerator: Failed to inspect {file}");
            }
        }

        return devices;
    }

    /// <summary>Returns only readable keyboard devices, suitable for hotkey listening.</summary>
    public static IReadOnlyList<InputDeviceInfo> GetReadableKeyboards()
    {
        var result = new List<InputDeviceInfo>();
        foreach (var device in Enumerate())
        {
            if (device.IsKeyboard && device.CanRead && !device.IsVirtual)
            {
                result.Add(device);
            }
        }

        return result;
    }

    private static InputDeviceInfo? Inspect(string devicePath)
    {
        int fd = EvdevNative.open(devicePath, EvdevNative.O_RDONLY | EvdevNative.O_NONBLOCK);
        if (fd < 0)
        {
            int errno = Marshal.GetLastWin32Error();
            // Still report keyboards we cannot open so diagnostics can explain permissions.
            return new InputDeviceInfo
            {
                Path = devicePath,
                Name = Path.GetFileName(devicePath),
                IsKeyboard = false,
                IsMouse = false,
                IsVirtual = false,
                CanRead = false,
                OpenErrno = errno
            };
        }

        try
        {
            string name = ReadDeviceName(fd);
            bool isKeyboard = CheckIsKeyboard(fd);
            bool isMouse = CheckIsMouse(fd);
            bool isVirtual = IsVirtualDevice(devicePath, name);

            return new InputDeviceInfo
            {
                Path = devicePath,
                Name = string.IsNullOrWhiteSpace(name) ? "Unknown Device" : name,
                IsKeyboard = isKeyboard,
                IsMouse = isMouse,
                IsVirtual = isVirtual,
                CanRead = true,
                OpenErrno = 0
            };
        }
        finally
        {
            EvdevNative.close(fd);
        }
    }

    private static string ReadDeviceName(int fd)
    {
        var nameBuf = new byte[256];
        int result = EvdevNative.ioctl(fd, EvdevNative.EVIOCGNAME_256, nameBuf);
        if (result < 0)
        {
            return string.Empty;
        }

        return Encoding.ASCII.GetString(nameBuf).TrimEnd('\0').Trim();
    }

    private static bool CheckIsKeyboard(int fd)
    {
        // Must support key events at all.
        if (!HasEventType(fd, InputEventCodes.EV_KEY))
        {
            return false;
        }

        // Require at least Esc or Enter plus part of the QWERTY row to avoid matching
        // power buttons and consumer-control pseudo keyboards.
        bool hasEscOrEnter = HasCapability(fd, InputEventCodes.EV_KEY, InputEventCodes.KEY_ESC) ||
                             HasCapability(fd, InputEventCodes.EV_KEY, InputEventCodes.KEY_ENTER);
        if (!hasEscOrEnter)
        {
            return false;
        }

        for (int keyCode = 30; keyCode <= 44; keyCode++)
        {
            if (HasCapability(fd, InputEventCodes.EV_KEY, keyCode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CheckIsMouse(int fd)
    {
        if (!HasEventType(fd, InputEventCodes.EV_REL) || !HasEventType(fd, InputEventCodes.EV_KEY))
        {
            return false;
        }

        if (!HasCapability(fd, InputEventCodes.EV_REL, InputEventCodes.REL_X) ||
            !HasCapability(fd, InputEventCodes.EV_REL, InputEventCodes.REL_Y))
        {
            return false;
        }

        return HasCapability(fd, InputEventCodes.EV_KEY, InputEventCodes.BTN_LEFT);
    }

    private static bool HasEventType(int fd, int eventType)
    {
        var mask = new byte[(InputEventCodes.EV_ABS / 8) + 4];
        int len = EvdevNative.ioctl(fd, EvdevNative.EVIOCGBIT(0, mask.Length), mask);
        if (len < 0)
        {
            return false;
        }

        int byteIndex = eventType / 8;
        int bitIndex = eventType % 8;
        return byteIndex < mask.Length && (mask[byteIndex] & (1 << bitIndex)) != 0;
    }

    private static bool HasCapability(int fd, int eventType, int code)
    {
        var mask = new byte[96];
        int len = EvdevNative.ioctl(fd, EvdevNative.EVIOCGBIT(eventType, mask.Length), mask);
        if (len < 0)
        {
            return false;
        }

        int byteIndex = code / 8;
        int bitIndex = code % 8;
        return byteIndex < mask.Length && (mask[byteIndex] & (1 << bitIndex)) != 0;
    }

    internal static bool IsVirtualDevice(string devicePath, string deviceName)
    {
        if (deviceName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("uinput", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("XerahS", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("ShareX", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            string eventName = Path.GetFileName(devicePath);
            string sysPath = $"/sys/class/input/{eventName}/device";
            if (Directory.Exists(sysPath))
            {
                string realPath = new DirectoryInfo(sysPath).FullName;
                if (realPath.Contains("/sys/devices/virtual/", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Best-effort sysfs probe.
        }

        return false;
    }
}
