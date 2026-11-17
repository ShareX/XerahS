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
using XerahS.Common;
using XerahS.Common.Helpers;

namespace XerahS.Platform.Windows.Capture;

/// <summary>
/// Reads the SDR white level Windows composites SDR content at (Settings → Display → HDR → SDR
/// content brightness). DXGI synchronous frames are linear scRGB where 1.0 == 80 cd/m², so this
/// value is the multiplier the capture pipeline must divide by for <c>1.0 == SDR white</c> to hold.
/// Falls back to 1.0 (80 cd/m²) whenever the OS query is unavailable.
/// </summary>
internal static class DisplaySdrWhiteLevelHelper
{
    private const uint QDC_ONLY_ACTIVE_PATHS = 2;
    private const int ERROR_SUCCESS = 0;

    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11;

    /// <summary>
    /// Returns the SDR white level in scRGB units (1.0 == 80 cd/m²) for the output whose GDI name
    /// matches <paramref name="gdiDeviceName"/> (for example <c>\\.\DISPLAY1</c>). Returns 1.0 when
    /// the display is not in advanced-color mode or the query fails.
    /// </summary>
    public static float GetSdrWhiteLevelScRgb(string? gdiDeviceName)
    {
        if (string.IsNullOrEmpty(gdiDeviceName))
        {
            return 1f;
        }

        try
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != ERROR_SUCCESS)
            {
                return 1f;
            }

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != ERROR_SUCCESS)
            {
                return 1f;
            }

            for (int i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var sourceName = new DisplayConfigSourceDeviceName
                {
                    Header = new DisplayConfigDeviceInfoHeader
                    {
                        Type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                        Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                        AdapterId = path.SourceInfo.AdapterId,
                        Id = path.SourceInfo.Id
                    }
                };

                if (DisplayConfigGetDeviceInfo(ref sourceName) != ERROR_SUCCESS)
                {
                    continue;
                }

                if (!string.Equals(sourceName.ViewGdiDeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var whiteLevel = new DisplayConfigSdrWhiteLevel
                {
                    Header = new DisplayConfigDeviceInfoHeader
                    {
                        Type = DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL,
                        Size = (uint)Marshal.SizeOf<DisplayConfigSdrWhiteLevel>(),
                        AdapterId = path.TargetInfo.AdapterId,
                        Id = path.TargetInfo.Id
                    }
                };

                if (DisplayConfigGetDeviceInfo(ref whiteLevel) != ERROR_SUCCESS)
                {
                    return 1f;
                }

                float scRgb = HdrSdrWhiteNormalizer.FromDisplayConfigWhiteLevel(whiteLevel.SdrWhiteLevel);
                DebugHelper.WriteLine(
                    $"[HDR] SDR white level for {gdiDeviceName}: {whiteLevel.SdrWhiteLevel}/1000 " +
                    $"= {HdrSdrWhiteNormalizer.ScRgbToNits(scRgb):F0} nits (scRGB {scRgb:F3})");
                return scRgb;
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[HDR] SDR white level query failed: {ex.Message}");
        }

        return 1f;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathInfoArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName deviceName);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSdrWhiteLevel sdrWhiteLevel);

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
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public uint RefreshRate;
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

    /// <summary>
    /// The union member of DISPLAYCONFIG_MODE_INFO is 48 bytes; the capture path never reads it,
    /// but the array must have the exact native stride for QueryDisplayConfig.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigModeInfoUnion
    {
        public ulong Reserved0;
        public ulong Reserved1;
        public ulong Reserved2;
        public ulong Reserved3;
        public ulong Reserved4;
        public ulong Reserved5;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigModeInfo
    {
        public uint InfoType;
        public uint Id;
        public Luid AdapterId;
        public DisplayConfigModeInfoUnion Mode;
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
}
