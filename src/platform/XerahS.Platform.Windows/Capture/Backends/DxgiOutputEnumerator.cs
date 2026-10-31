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

using System.Drawing;
using System.Runtime.InteropServices;
using Vortice.DXGI;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture.Backends;

/// <summary>
/// One desktop output reported by DXGI, with the rotation XerahS must undo.
/// </summary>
internal sealed record DxgiOutputInfo(
    IDXGIOutput1 Output,
    IDXGIAdapter1 Adapter,
    Rectangle Bounds,
    ModeRotation Rotation,
    string DeviceName,
    ModeRotation DxgiRotation);

/// <summary>
/// Enumerates hardware DXGI outputs. The caller owns the returned COM objects and releases them with
/// <see cref="DxgiOutputEnumerationCleanupHelper.DisposeOutputsAndAdapters"/>.
/// </summary>
internal static class DxgiOutputEnumerator
{
    public static List<DxgiOutputInfo> Enumerate(IDXGIFactory1 factory)
    {
        var outputs = new List<DxgiOutputInfo>();

        for (uint adapterIndex = 0; factory.EnumAdapters1(adapterIndex, out var adapter).Success; adapterIndex++)
        {
            var desc = adapter.Description1;

            // Skip software adapters
            if ((desc.Flags & AdapterFlags.Software) != 0)
            {
                adapter.Dispose();
                continue;
            }

            int outputCountBefore = outputs.Count;

            for (uint outputIndex = 0; adapter.EnumOutputs(outputIndex, out var output).Success; outputIndex++)
            {
                var output1 = output.QueryInterface<IDXGIOutput1>();
                var outputDesc = output1.Description;

                var rect = outputDesc.DesktopCoordinates;
                var bounds = new Rectangle(
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top);

                var dxgiRotation = outputDesc.Rotation;
                var effectiveRotation = TryGetDisplaySettingsRotation(outputDesc.DeviceName, out var displayRotation)
                    ? displayRotation
                    : dxgiRotation;

                if (effectiveRotation != dxgiRotation)
                {
                    DebugHelper.WriteLine(
                        $"CaptureFullScreenDxgi: Rotation override for {outputDesc.DeviceName}. DisplaySettings={effectiveRotation}, DXGI={dxgiRotation}");
                }

                outputs.Add(new DxgiOutputInfo(output1, adapter, bounds, effectiveRotation, outputDesc.DeviceName, dxgiRotation));
                output.Dispose();
            }

            // Only dispose the adapter if no outputs were added from it
            if (outputs.Count == outputCountBefore)
            {
                adapter.Dispose();
            }
        }

        return outputs;
    }

    /// <summary>
    /// Union of all output bounds, in desktop coordinates.
    /// </summary>
    public static Rectangle GetDesktopBounds(IReadOnlyList<DxgiOutputInfo> outputs)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (DxgiOutputInfo output in outputs)
        {
            minX = Math.Min(minX, output.Bounds.Left);
            minY = Math.Min(minY, output.Bounds.Top);
            maxX = Math.Max(maxX, output.Bounds.Right);
            maxY = Math.Max(maxY, output.Bounds.Bottom);
        }

        return outputs.Count == 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX, maxY);
    }

    public static int ToClockwiseDegrees(ModeRotation rotation) => rotation switch
    {
        // ModeRotation.Rotate90 = display is physically rotated 90° CW → frame needs 90° CW correction.
        // ModeRotation.Rotate270 = display is physically rotated 270° CW → frame needs 270° CW correction.
        // Issue #148: DMDO_90/DMDO_270 were previously swapped in TryGetDisplaySettingsRotation,
        // causing the wrong ModeRotation to reach here and producing a black/inverted image on rotated monitors.
        ModeRotation.Rotate90 => 90,
        ModeRotation.Rotate180 => 180,
        ModeRotation.Rotate270 => 270,
        _ => 0 // Identity / Unspecified
    };

    private static bool TryGetDisplaySettingsRotation(string deviceName, out ModeRotation rotation)
    {
        rotation = ModeRotation.Unspecified;

        var devMode = new NativeMethods.DEVMODE
        {
            dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE))
        };

        if (!NativeMethods.EnumDisplaySettings(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
        {
            return false;
        }

        // Issue #148 regression guard: DEVMODE and DXGI both use CLOCKWISE degrees.
        // DMDO_90 (1) = 90° CW  → ModeRotation.Rotate90   (NOT Rotate270)
        // DMDO_270 (3) = 270° CW → ModeRotation.Rotate270  (NOT Rotate90)
        // Previously these were swapped, causing black screens on portrait/flipped monitors.
        // See also: BitmapRotationHelperTests for cross-platform regression coverage.
        rotation = devMode.dmDisplayOrientation switch
        {
            NativeMethods.DMDO_DEFAULT => ModeRotation.Identity,
            NativeMethods.DMDO_90 => ModeRotation.Rotate90,    // 90° CW
            NativeMethods.DMDO_180 => ModeRotation.Rotate180,  // 180°
            NativeMethods.DMDO_270 => ModeRotation.Rotate270,  // 270° CW
            _ => ModeRotation.Unspecified
        };

        DebugHelper.WriteLine(
            $"CaptureFullScreenDxgi: EnumDisplaySettings orientation for {deviceName} => dmDisplayOrientation={devMode.dmDisplayOrientation}, mappedRotation={rotation}");

        return true;
    }

    private static class NativeMethods
    {
        public const int ENUM_CURRENT_SETTINGS = -1;

        public const int DMDO_DEFAULT = 0;
        public const int DMDO_90 = 1;
        public const int DMDO_180 = 2;
        public const int DMDO_270 = 3;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);
    }
}
