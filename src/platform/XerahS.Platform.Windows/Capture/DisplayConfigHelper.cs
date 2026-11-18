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

namespace XerahS.Platform.Windows.Capture;

internal static class DisplayConfigHelper
{
    private const uint QueryDisplayConfigOnlyActivePaths = 0x00000002;
    private const uint DisplayConfigGetSourceName = 1;
    private const uint DisplayConfigGetSdrWhiteLevel = 11;
    private const uint DisplayConfigGetAdvancedColorInfo = 9;
    private const int DisplayConfigModeInfoSize = 64;

    public static float GetSdrWhiteScale(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return 1f;
        }

        try
        {
            if (GetDisplayConfigBufferSizes(QueryDisplayConfigOnlyActivePaths, out uint pathCount, out uint modeCount) != 0)
            {
                return 1f;
            }

            DisplayConfigPathInfo[] paths = new DisplayConfigPathInfo[pathCount];
            IntPtr modes = Marshal.AllocHGlobal(checked((int)modeCount * DisplayConfigModeInfoSize));

            try
            {
                if (QueryDisplayConfig(QueryDisplayConfigOnlyActivePaths, ref pathCount, paths,
                    ref modeCount, modes, IntPtr.Zero) != 0)
                {
                    return 1f;
                }

                for (int i = 0; i < pathCount; i++)
                {
                    DisplayConfigSourceDeviceName sourceName = new()
                    {
                        Header = new DisplayConfigDeviceInfoHeader
                        {
                            Type = DisplayConfigGetSourceName,
                            Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                            AdapterId = paths[i].SourceInfo.AdapterId,
                            Id = paths[i].SourceInfo.Id
                        },
                        ViewGdiDeviceName = string.Empty
                    };

                    if (DisplayConfigGetDeviceInfo(ref sourceName) != 0 ||
                        !string.Equals(sourceName.ViewGdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DisplayConfigSdrWhiteLevel whiteLevel = new()
                    {
                        Header = new DisplayConfigDeviceInfoHeader
                        {
                            Type = DisplayConfigGetSdrWhiteLevel,
                            Size = (uint)Marshal.SizeOf<DisplayConfigSdrWhiteLevel>(),
                            AdapterId = paths[i].TargetInfo.AdapterId,
                            Id = paths[i].TargetInfo.Id
                        }
                    };

                    if (DisplayConfigGetDeviceInfo(ref whiteLevel) == 0 && whiteLevel.SdrWhiteLevel > 0)
                    {
                        return Math.Clamp(whiteLevel.SdrWhiteLevel / 1000f, 1f, 125f);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(modes);
            }
        }
        catch (Exception ex)
        {
            XerahS.Common.DebugHelper.WriteLine($"Failed to query the HDR display SDR white level: {ex.Message}");
        }

        return 1f;
    }

    /// <summary>DISPLAYCONFIG_COLOR_ENCODING: the signal encoding, not an SDR/HDR switch.</summary>
    internal enum DisplayColorEncoding
    {
        Rgb = 0,
        YCbCr444 = 1,
        YCbCr422 = 2,
        YCbCr420 = 3,
        Intensity = 4
    }

    /// <summary>
    /// Advanced colour on this output. <see cref="Enabled"/> is what the HDR toggle controls:
    /// when it is set Windows composites the desktop in scRGB and Desktop Duplication can hand
    /// back fp16 frames. The encoding and bit depth describe the wire format.
    /// </summary>
    internal readonly record struct AdvancedColorState(
        bool Supported,
        bool Enabled,
        DisplayColorEncoding Encoding,
        int BitsPerChannel);

    /// <summary>
    /// Returns the advanced-colour state of the output whose GDI name matches
    /// <paramref name="deviceName"/>, or null when the query is unavailable.
    /// </summary>
    public static AdvancedColorState? GetAdvancedColorState(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        try
        {
            if (GetDisplayConfigBufferSizes(QueryDisplayConfigOnlyActivePaths, out uint pathCount, out uint modeCount) != 0)
            {
                return null;
            }

            DisplayConfigPathInfo[] paths = new DisplayConfigPathInfo[pathCount];
            IntPtr modes = Marshal.AllocHGlobal(checked((int)modeCount * DisplayConfigModeInfoSize));

            try
            {
                if (QueryDisplayConfig(QueryDisplayConfigOnlyActivePaths, ref pathCount, paths,
                    ref modeCount, modes, IntPtr.Zero) != 0)
                {
                    return null;
                }

                for (int i = 0; i < pathCount; i++)
                {
                    DisplayConfigSourceDeviceName sourceName = new()
                    {
                        Header = new DisplayConfigDeviceInfoHeader
                        {
                            Type = DisplayConfigGetSourceName,
                            Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                            AdapterId = paths[i].SourceInfo.AdapterId,
                            Id = paths[i].SourceInfo.Id
                        },
                        ViewGdiDeviceName = string.Empty
                    };

                    if (DisplayConfigGetDeviceInfo(ref sourceName) != 0 ||
                        !string.Equals(sourceName.ViewGdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DisplayConfigAdvancedColorInfo info = new()
                    {
                        Header = new DisplayConfigDeviceInfoHeader
                        {
                            Type = DisplayConfigGetAdvancedColorInfo,
                            Size = (uint)Marshal.SizeOf<DisplayConfigAdvancedColorInfo>(),
                            AdapterId = paths[i].TargetInfo.AdapterId,
                            Id = paths[i].TargetInfo.Id
                        }
                    };

                    if (DisplayConfigGetDeviceInfo(ref info) != 0)
                    {
                        return null;
                    }

                    var encoding = info.ColorEncoding is <= 4
                        ? (DisplayColorEncoding)info.ColorEncoding
                        : DisplayColorEncoding.Rgb;
                    int bitsPerChannel = info.BitsPerColorChannel switch
                    {
                        1 => 10,
                        2 => 12,
                        3 => 16,
                        _ => 8
                    };

                    var state = new AdvancedColorState(
                        (info.Value & 0x1) != 0,
                        ((info.Value >> 1) & 0x1) != 0,
                        encoding,
                        bitsPerChannel);

                    XerahS.Common.DebugHelper.WriteLine(
                        $"[HDR] {deviceName}: advanced colour supported={state.Supported} enabled={state.Enabled} " +
                        $"encoding={state.Encoding} bitsPerChannel={state.BitsPerChannel}");
                    return state;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(modes);
            }
        }
        catch (Exception ex)
        {
            XerahS.Common.DebugHelper.WriteLine($"Failed to query the advanced colour state: {ex.Message}");
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint pathCount,
        [Out] DisplayConfigPathInfo[] paths, ref uint modeCount, IntPtr modes, IntPtr currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSdrWhiteLevel requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigAdvancedColorInfo requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigRational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DisplayConfigRational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo SourceInfo;
        public DisplayConfigPathTargetInfo TargetInfo;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigSdrWhiteLevel
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint SdrWhiteLevel;
    }

    /// <summary>
    /// DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_STRUCT. The two trailing fields are part of the
    /// native struct, so the header size must include them or the call fails with ERROR_INVALID_PARAMETER.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigAdvancedColorInfo
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColorChannel;
    }
}
