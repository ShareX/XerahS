#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System.Runtime.InteropServices;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

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
/// Advanced colour state of one output. <see cref="Enabled"/> is what the HDR toggle controls:
/// when it is set Windows composites the desktop in scRGB and Desktop Duplication can hand back
/// fp16 frames. The encoding and bit depth describe the wire format.
/// </summary>
internal readonly record struct AdvancedColorState(
    bool Supported,
    bool Enabled,
    DisplayColorEncoding Encoding,
    int BitsPerChannel);

/// <summary>
/// Reads DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO for a GDI device name. DXGI keeps reporting a
/// PQ/BT.2020 colour space on an output where the HDR toggle was never applied, so the OS answer
/// is the one to trust.
/// </summary>
internal static class AdvancedColorInfo
{
    private const uint QueryDisplayConfigOnlyActivePaths = 2;
    private const uint GetSourceName = 1;
    private const uint GetAdvancedColorInfo = 9;
    private const int ModeInfoSize = 64;

    public static AdvancedColorState? GetState(string? deviceName)
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

            var paths = new DisplayConfigPathInfo[pathCount];
            IntPtr modes = Marshal.AllocHGlobal(checked((int)modeCount * ModeInfoSize));

            try
            {
                if (QueryDisplayConfig(QueryDisplayConfigOnlyActivePaths, ref pathCount, paths,
                    ref modeCount, modes, IntPtr.Zero) != 0)
                {
                    return null;
                }

                for (int i = 0; i < pathCount; i++)
                {
                    var sourceName = new DisplayConfigSourceDeviceName
                    {
                        Header = new DisplayConfigDeviceInfoHeader
                        {
                            Type = GetSourceName,
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

                    var info = new DisplayConfigAdvancedColorInfo
                    {
                        Header = new DisplayConfigDeviceInfoHeader
                        {
                            Type = GetAdvancedColorInfo,
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

                    DebugHelper.WriteLine(
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
            DebugHelper.WriteLine($"Failed to query the advanced colour state: {ex.Message}");
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
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigAdvancedColorInfo requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    /// <summary>
    /// DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_STRUCT. The two trailing fields are part of the native
    /// struct, so the header size must include them or the call fails with ERROR_INVALID_PARAMETER.
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
