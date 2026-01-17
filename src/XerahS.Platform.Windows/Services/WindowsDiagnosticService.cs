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

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Services
{
    public class WindowsDiagnosticService : IDiagnosticService
    {
        private const string FolderName = "CaptureTroubleshooting";

        public string WriteRegionCaptureDiagnostics(string personalFolder)
        {
            if (!OperatingSystem.IsWindows())
            {
                return string.Empty;
            }

            var errors = new List<string>();
            var monitors = new List<MonitorInfo>();

            try
            {
                // Enumerate monitors
                EnumerateMonitors(monitors, errors);

                // Build log content
                var log = BuildLogContent(monitors, errors);

                // Build filename
                var fileName = BuildFileName(monitors);

                // Ensure folder exists
                var folder = Path.Combine(personalFolder, FolderName);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // Write file
                var filePath = Path.Combine(folder, fileName);
                File.WriteAllText(filePath, log, Encoding.UTF8);

                return filePath;
            }
            catch (Exception ex)
            {
                // Never crash Region Capture - silently fail
                try
                {
                    // Attempt to write an error log
                    var folder = Path.Combine(personalFolder, FolderName);
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    var errorPath = Path.Combine(folder, $"{SanitizeFileName(Environment.MachineName)}-error.log");
                    File.WriteAllText(errorPath, $"Diagnostic collection failed: {ex.Message}\n{ex.StackTrace}", Encoding.UTF8);
                    return errorPath;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private static string FormatResolutionList(IEnumerable<MonitorInfo> monitors)
        {
            return string.Join("+", monitors.Select(m => $"{m.ResolutionWidth}x{m.ResolutionHeight}"));
        }

        #region Monitor Information Model

        private sealed class MonitorInfo
        {
            public int Index { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public bool IsPrimary { get; set; }
            public int ResolutionWidth { get; set; }
            public int ResolutionHeight { get; set; }
            public int MonitorLeft { get; set; }
            public int MonitorTop { get; set; }
            public int MonitorRight { get; set; }
            public int MonitorBottom { get; set; }
            public int WorkLeft { get; set; }
            public int WorkTop { get; set; }
            public int WorkRight { get; set; }
            public int WorkBottom { get; set; }
            public uint EffectiveDpiX { get; set; } = 96;
            public uint EffectiveDpiY { get; set; } = 96;
            public uint RawDpiX { get; set; } = 96;
            public uint RawDpiY { get; set; } = 96;
            public double ScaleX => EffectiveDpiX / 96.0;
            public double ScaleY => EffectiveDpiY / 96.0;
            public bool HasNegativeCoords => MonitorLeft < 0 || MonitorTop < 0;

            public int MonitorWidth => MonitorRight - MonitorLeft;
            public int MonitorHeight => MonitorBottom - MonitorTop;
        }

        #endregion

        #region Monitor Enumeration

        private static void EnumerateMonitors(List<MonitorInfo> monitors, List<string> errors)
        {
            int monitorIndex = 0;

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
            {
                try
                {
                    var info = new MonitorInfo { Index = ++monitorIndex };

                    // Get monitor info
                    var monitorInfoEx = new NativeMethods.MONITORINFOEX();
                    monitorInfoEx.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));

                    if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfoEx))
                    {
                        info.DeviceName = monitorInfoEx.szDevice;
                        info.IsPrimary = (monitorInfoEx.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                        info.MonitorLeft = monitorInfoEx.rcMonitor.Left;
                        info.MonitorTop = monitorInfoEx.rcMonitor.Top;
                        info.MonitorRight = monitorInfoEx.rcMonitor.Right;
                        info.MonitorBottom = monitorInfoEx.rcMonitor.Bottom;
                        info.WorkLeft = monitorInfoEx.rcWork.Left;
                        info.WorkTop = monitorInfoEx.rcWork.Top;
                        info.WorkRight = monitorInfoEx.rcWork.Right;
                        info.WorkBottom = monitorInfoEx.rcWork.Bottom;

                        // Get resolution from display settings
                        var devMode = new NativeMethods.DEVMODE();
                        devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));

                        if (NativeMethods.EnumDisplaySettings(info.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                        {
                            info.ResolutionWidth = devMode.dmPelsWidth;
                            info.ResolutionHeight = devMode.dmPelsHeight;
                        }
                        else
                        {
                            // Fallback to monitor rect dimensions
                            info.ResolutionWidth = info.MonitorWidth;
                            info.ResolutionHeight = info.MonitorHeight;
                            errors.Add($"Monitor {monitorIndex}: EnumDisplaySettings failed (GetLastError: {Marshal.GetLastWin32Error()})");
                        }

                        // Get DPI
                        int hrEffective = NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
                            out uint effDpiX, out uint effDpiY);
                        if (hrEffective == 0)
                        {
                            info.EffectiveDpiX = effDpiX;
                            info.EffectiveDpiY = effDpiY;
                        }
                        else
                        {
                            errors.Add($"Monitor {monitorIndex}: GetDpiForMonitor (Effective) failed (HRESULT: 0x{hrEffective:X8})");
                        }

                        int hrRaw = NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MONITOR_DPI_TYPE.MDT_RAW_DPI,
                            out uint rawDpiX, out uint rawDpiY);
                        if (hrRaw == 0)
                        {
                            info.RawDpiX = rawDpiX;
                            info.RawDpiY = rawDpiY;
                        }
                        else
                        {
                            errors.Add($"Monitor {monitorIndex}: GetDpiForMonitor (Raw) failed (HRESULT: 0x{hrRaw:X8})");
                        }
                    }
                    else
                    {
                        errors.Add($"Monitor {monitorIndex}: GetMonitorInfo failed (GetLastError: {Marshal.GetLastWin32Error()})");
                    }

                    monitors.Add(info);
                }
                catch (Exception ex)
                {
                    errors.Add($"Monitor {monitorIndex}: Exception during enumeration - {ex.Message}");
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);
        }

        #endregion

        #region Log Building

        private static string BuildLogContent(List<MonitorInfo> monitors, List<string> errors)
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;

            // Header
            sb.AppendLine("================================================================================");
            sb.AppendLine("                    REGION CAPTURE DIAGNOSTICS");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            // System Info
            sb.AppendLine("[HEADER]");
            sb.AppendLine($"ComputerName: {Environment.MachineName}");
            sb.AppendLine($"TimestampLocal: {DateTime.Now:O}");
            sb.AppendLine($"TimestampUtc: {DateTime.UtcNow:O}");
            sb.AppendLine($"OSDescription: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"OSArchitecture: {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"Is64BitProcess: {Environment.Is64BitProcess}");
            sb.AppendLine($"Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"CurrentUser: {Environment.UserName}");
            sb.AppendLine($"CurrentCulture: {CultureInfo.CurrentCulture.Name}");
            sb.AppendLine();

            // Virtual Screen
            sb.AppendLine("[VIRTUAL SCREEN]");
            if (monitors.Count > 0)
            {
                int left = monitors.Min(m => m.MonitorLeft);
                int top = monitors.Min(m => m.MonitorTop);
                int right = monitors.Max(m => m.MonitorRight);
                int bottom = monitors.Max(m => m.MonitorBottom);
                bool hasNegative = monitors.Any(m => m.HasNegativeCoords);

                sb.AppendLine(string.Format(inv, "Bounds: Left={0}, Top={1}, Right={2}, Bottom={3}", left, top, right, bottom));
                sb.AppendLine(string.Format(inv, "Width: {0}", right - left));
                sb.AppendLine(string.Format(inv, "Height: {0}", bottom - top));
                sb.AppendLine($"AnyNegativeCoordinates: {hasNegative}");
            }
            else
            {
                sb.AppendLine("No monitors detected");
            }
            sb.AppendLine();

            // Monitors
            sb.AppendLine("[MONITORS]");
            sb.AppendLine("--------------------------------------------------------------------------------");
            foreach (var m in monitors)
            {
                sb.AppendLine($"Monitor {m.Index}: {m.DeviceName}");
                sb.AppendLine($"  IsPrimary: {m.IsPrimary}");
                sb.AppendLine(string.Format(inv, "  Resolution: {0}x{1}", m.ResolutionWidth, m.ResolutionHeight));
                sb.AppendLine(string.Format(inv, "  MonitorRect: Left={0}, Top={1}, Right={2}, Bottom={3}",
                    m.MonitorLeft, m.MonitorTop, m.MonitorRight, m.MonitorBottom));
                sb.AppendLine(string.Format(inv, "  WorkRect: Left={0}, Top={1}, Right={2}, Bottom={3}",
                    m.WorkLeft, m.WorkTop, m.WorkRight, m.WorkBottom));
                sb.AppendLine(string.Format(inv, "  EffectiveDpiX: {0}", m.EffectiveDpiX));
                sb.AppendLine(string.Format(inv, "  EffectiveDpiY: {0}", m.EffectiveDpiY));
                sb.AppendLine(string.Format(inv, "  RawDpiX: {0}", m.RawDpiX));
                sb.AppendLine(string.Format(inv, "  RawDpiY: {0}", m.RawDpiY));
                sb.AppendLine(string.Format(inv, "  ScaleX: {0:F2}", m.ScaleX));
                sb.AppendLine(string.Format(inv, "  ScaleY: {0:F2}", m.ScaleY));
                sb.AppendLine($"  HasNegativeCoords: {m.HasNegativeCoords}");
                sb.AppendLine();
            }

            // Relative Positions
            sb.AppendLine("[RELATIVE POSITIONS]");
            foreach (var a in monitors)
            {
                sb.AppendLine($"Monitor {a.Index}:");

                var leftOf = new List<int>();
                var rightOf = new List<int>();
                var above = new List<int>();
                var below = new List<int>();
                var overlaps = new List<int>();

                foreach (var b in monitors)
                {
                    if (a.Index == b.Index) continue;

                    // Check spatial relationships
                    if (a.MonitorRight <= b.MonitorLeft)
                    {
                        leftOf.Add(b.Index);
                    }
                    else if (a.MonitorLeft >= b.MonitorRight)
                    {
                        rightOf.Add(b.Index);
                    }
                    else if (a.MonitorBottom <= b.MonitorTop)
                    {
                        above.Add(b.Index);
                    }
                    else if (a.MonitorTop >= b.MonitorBottom)
                    {
                        below.Add(b.Index);
                    }
                    else
                    {
                        overlaps.Add(b.Index);
                    }
                }

                if (leftOf.Count > 0)
                    sb.AppendLine($"  LeftOf: {string.Join(", ", leftOf.Select(i => $"Monitor {i}"))}");
                if (rightOf.Count > 0)
                    sb.AppendLine($"  RightOf: {string.Join(", ", rightOf.Select(i => $"Monitor {i}"))}");
                if (above.Count > 0)
                    sb.AppendLine($"  Above: {string.Join(", ", above.Select(i => $"Monitor {i}"))}");
                if (below.Count > 0)
                    sb.AppendLine($"  Below: {string.Join(", ", below.Select(i => $"Monitor {i}"))}");
                if (overlaps.Count > 0)
                    sb.AppendLine($"  Overlaps: {string.Join(", ", overlaps.Select(i => $"Monitor {i}"))}");
            }
            sb.AppendLine();

            // Errors
            sb.AppendLine("[ERRORS]");
            if (errors.Count == 0)
            {
                sb.AppendLine("None");
            }
            else
            {
                foreach (var error in errors)
                {
                    sb.AppendLine(error);
                }
            }
            sb.AppendLine();

            sb.AppendLine("================================================================================");
            sb.AppendLine("EOF");
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }

        private static string BuildFileName(List<MonitorInfo> monitors)
        {
            var machineName = SanitizeFileName(Environment.MachineName);
            var resolutions = FormatResolutionList(monitors);
            return $"{machineName}-{resolutions}.log";
        }

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                sb.Append(invalidChars.Contains(c) ? '_' : c);
            }
            return sb.ToString();
        }

        #endregion

        #region Native Methods (Windows Only)

        private static class NativeMethods
        {
            public const int MONITORINFOF_PRIMARY = 0x00000001;
            public const int ENUM_CURRENT_SETTINGS = -1;

            public enum MONITOR_DPI_TYPE
            {
                MDT_EFFECTIVE_DPI = 0,
                MDT_ANGULAR_DPI = 1,
                MDT_RAW_DPI = 2
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct MONITORINFOEX
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string szDevice;
            }

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

            public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

            [DllImport("user32.dll")]
            public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

            [DllImport("shcore.dll")]
            public static extern int GetDpiForMonitor(IntPtr hMonitor, MONITOR_DPI_TYPE dpiType, out uint dpiX, out uint dpiY);
        }

        #endregion
    }
}
