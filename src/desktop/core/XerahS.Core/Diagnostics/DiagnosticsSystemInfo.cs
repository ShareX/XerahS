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

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Diagnostics;

/// <summary>
/// Collects the non-identifying system facts attached to a diagnostics report.
/// Nothing here reads user content, account details, serial numbers or hostnames.
/// </summary>
public static class DiagnosticsSystemInfo
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(5);

    public static OsInfo CollectOs()
    {
        string family = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "macos"
            : OperatingSystem.IsLinux() ? "linux"
            : "other";

        return new OsInfo
        {
            Family = family,
            Description = RuntimeInformation.OSDescription,
            Version = Environment.OSVersion.Version.ToString(),
            // Both: an x64 build on an ARM64 PC runs emulated and behaves differently.
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Elevated = IsElevated(),
            Sandbox = DetectSandbox(),
            SessionType = OperatingSystem.IsWindows() ? "windows"
                : OperatingSystem.IsMacOS() ? "macos"
                : Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            DesktopEnvironment = OperatingSystem.IsLinux() ? Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") : null,
            UiLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
        };
    }

    private static bool? IsElevated()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            return Environment.UserName == "root";
        }
        catch
        {
            return null;
        }
    }

    private static string? DetectSandbox()
    {
        if (!OperatingSystem.IsLinux()) return null;
        if (Environment.GetEnvironmentVariable("FLATPAK_ID") != null || File.Exists("/.flatpak-info")) return "flatpak";
        if (Environment.GetEnvironmentVariable("SNAP") != null) return "snap";
        if (File.Exists("/.dockerenv") || File.Exists("/run/.containerenv")) return "container";
        return "none";
    }

    public static HardwareInfo CollectHardware()
    {
        return new HardwareInfo(
            CpuModel(),
            Environment.ProcessorCount,
            RamGbBucket(),
            Gpus());
    }

    private static string? CpuModel()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return (key?.GetValue("ProcessorNameString") as string)?.Trim();
            }
            if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            {
                foreach (string line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("model name", StringComparison.Ordinal) || line.StartsWith("Model", StringComparison.Ordinal))
                    {
                        return line[(line.IndexOf(':') + 1)..].Trim();
                    }
                }
            }
            if (OperatingSystem.IsMacOS())
            {
                return RunProcess("sysctl", "-n machdep.cpu.brand_string")?.Trim();
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[Diagnostics] CPU model unavailable: {ex.Message}");
        }
        return null;
    }

    /// <summary>Installed memory rounded up to a power of two, so it does not fingerprint a machine.</summary>
    public static int? RamGbBucket(long? totalBytes = null)
    {
        long bytes = totalBytes ?? GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (bytes <= 0) return null;
        double gb = bytes / 1024d / 1024d / 1024d;
        int bucket = 1;
        while (bucket < gb * 0.95 && bucket < 65536) bucket *= 2;
        return bucket;
    }

    public static int? VramMbBucket(long bytes)
    {
        if (bytes <= 0) return null;
        long mb = bytes / 1024 / 1024;
        int bucket = 256;
        while (bucket < mb * 0.95 && bucket < 1_048_576) bucket *= 2;
        return bucket;
    }

    private static IReadOnlyList<GpuInfo> Gpus()
    {
        var gpus = new List<GpuInfo>();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Display adapter class. Same data as Device Manager, without WMI.
                using RegistryKey? adapters = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                foreach (string name in adapters?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    if (!Regex.IsMatch(name, @"^\d{4}$")) continue;
                    using RegistryKey? adapter = adapters!.OpenSubKey(name);
                    if (adapter?.GetValue("DriverDesc") is not string description) continue;
                    string? matching = adapter.GetValue("MatchingDeviceId") as string;
                    long vram = adapter.GetValue("HardwareInformation.qwMemorySize") switch
                    {
                        long l => l,
                        byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
                        byte[] b when b.Length >= 4 => BitConverter.ToUInt32(b, 0),
                        int i => (uint)i,
                        _ => 0,
                    };
                    gpus.Add(new GpuInfo
                    {
                        Name = description,
                        VendorId = Regex.Match(matching ?? "", @"VEN_([0-9A-Fa-f]{4})").Groups[1].Value is { Length: > 0 } v ? v.ToLowerInvariant() : null,
                        DeviceId = Regex.Match(matching ?? "", @"DEV_([0-9A-Fa-f]{4})").Groups[1].Value is { Length: > 0 } d ? d.ToLowerInvariant() : null,
                        DriverVersion = adapter.GetValue("DriverVersion") as string,
                        VramMbBucket = VramMbBucket(vram),
                        IsSoftwareRenderer = description.Contains("Basic Render", StringComparison.OrdinalIgnoreCase) ||
                                             description.Contains("Basic Display", StringComparison.OrdinalIgnoreCase),
                    });
                }
            }
            else if (OperatingSystem.IsLinux() && Directory.Exists("/sys/class/drm"))
            {
                foreach (string card in Directory.GetDirectories("/sys/class/drm", "card*"))
                {
                    if (!Regex.IsMatch(Path.GetFileName(card), @"^card\d+$")) continue;
                    string device = Path.Combine(card, "device");
                    string? vendor = ReadTrimmed(Path.Combine(device, "vendor"))?.Replace("0x", "", StringComparison.Ordinal);
                    string? id = ReadTrimmed(Path.Combine(device, "device"))?.Replace("0x", "", StringComparison.Ordinal);
                    string? driver = ReadTrimmed(Path.Combine(device, "uevent"))?
                        .Split('\n').FirstOrDefault(l => l.StartsWith("DRIVER=", StringComparison.Ordinal))?[7..];
                    string? driverVersion = driver != null ? ReadTrimmed($"/sys/module/{driver}/version") : null;
                    gpus.Add(new GpuInfo
                    {
                        Name = driver != null ? $"{VendorName(vendor)} ({driver})" : VendorName(vendor),
                        VendorId = vendor,
                        DeviceId = id,
                        DriverVersion = driverVersion,
                        IsSoftwareRenderer = driver is "simpledrm" or "vkms",
                    });
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[Diagnostics] GPU information unavailable: {ex.Message}");
        }
        return gpus.Take(16).ToList();
    }

    private static string VendorName(string? vendorId) => vendorId?.ToLowerInvariant() switch
    {
        "10de" => "NVIDIA",
        "1002" => "AMD",
        "8086" => "Intel",
        "106b" => "Apple",
        "5143" => "Qualcomm",
        "1af4" => "Virtio",
        "15ad" => "VMware",
        null => "Unknown GPU",
        _ => $"GPU {vendorId}",
    };

    private static string? ReadTrimmed(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Monitor layout: bounds, scale and primary flag. No model names or serials.</summary>
    public static IReadOnlyList<DisplayInfo> CollectDisplays()
    {
        IReadOnlyList<DisplayInfo> displays = CollectPlatformDisplays();
        // The Linux screen service reads xrandr, which is often absent on Wayland.
        if (displays.Count == 0 && OperatingSystem.IsLinux() &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE")))
        {
            displays = ParseHyprlandMonitors(RunProcess("hyprctl", "monitors -j"));
        }
        return displays;
    }

    /// <summary>
    /// Parses `hyprctl monitors -j`. The monitor description is not used: it carries the
    /// panel's serial number.
    /// </summary>
    public static IReadOnlyList<DisplayInfo> ParseHyprlandMonitors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<DisplayInfo>();
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return Array.Empty<DisplayInfo>();
            var displays = new List<DisplayInfo>();
            foreach (var monitor in document.RootElement.EnumerateArray())
            {
                static int Int(System.Text.Json.JsonElement element, string name) =>
                    element.TryGetProperty(name, out var value) && value.TryGetInt32(out int number) ? number : 0;
                static double? Number(System.Text.Json.JsonElement element, string name) =>
                    element.TryGetProperty(name, out var value) && value.TryGetDouble(out double number) ? number : null;

                if (monitor.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == System.Text.Json.JsonValueKind.True) continue;
                int width = Int(monitor, "width");
                int height = Int(monitor, "height");
                if (width <= 0 || height <= 0) continue;
                string? name = monitor.TryGetProperty("name", out var n) ? n.GetString() : null;
                displays.Add(new DisplayInfo
                {
                    Ordinal = displays.Count,
                    DeviceName = name is { Length: > 0 and <= 64 } ? name : null,
                    IsPrimary = displays.Count == 0,
                    X = Int(monitor, "x"),
                    Y = Int(monitor, "y"),
                    Width = width,
                    Height = height,
                    Scale = Number(monitor, "scale") is { } scale ? Math.Round(scale, 4) : null,
                    // Wayland transforms 0-3 rotate 0/90/180/270; 4-7 are the flipped variants.
                    Rotation = Int(monitor, "transform") % 4 * 90,
                    RefreshHz = Number(monitor, "refreshRate") is { } hz ? Math.Round(hz, 3) : null,
                });
                if (displays.Count >= 32) break;
            }
            return displays;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<DisplayInfo>();
        }
    }

    private static IReadOnlyList<DisplayInfo> CollectPlatformDisplays()
    {
        try
        {
            if (!PlatformServices.IsInitialized) return Array.Empty<DisplayInfo>();
            return PlatformServices.Screen.GetAllScreens()
                .OrderBy(s => s.DeviceName, StringComparer.Ordinal)
                .Take(32)
                .Select((screen, index) => new DisplayInfo
                {
                    Ordinal = index,
                    DeviceName = screen.DeviceName.Length is > 0 and <= 64 ? screen.DeviceName : null,
                    IsPrimary = screen.IsPrimary,
                    X = screen.Bounds.X,
                    Y = screen.Bounds.Y,
                    Width = Math.Max(1, screen.Bounds.Width),
                    Height = Math.Max(1, screen.Bounds.Height),
                    Scale = Math.Round(screen.ScaleFactor, 4),
                    BitsPerPixel = screen.BitsPerPixel > 0 ? screen.BitsPerPixel : null,
                })
                .ToList();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[Diagnostics] Display information unavailable: {ex.Message}");
            return Array.Empty<DisplayInfo>();
        }
    }

    private static readonly Regex HardwareEncoder = new(
        @"^\s*V\S*\s+(\S*(?:nvenc|qsv|amf|vaapi|videotoolbox|_mf|v4l2m2m|vulkan|d3d12va)\S*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>FFmpeg version, where it came from, and its hardware video encoders.</summary>
    public static FfmpegInfo CollectFfmpeg()
    {
        string path;
        try
        {
            path = PathsManager.GetFFmpegPath();
        }
        catch
        {
            return new FfmpegInfo(null, "none", Array.Empty<string>());
        }

        bool bundled = path.StartsWith(PathsManager.ToolsFolder, StringComparison.OrdinalIgnoreCase);
        string? versionOutput = RunProcess(path, "-hide_banner -version");
        if (versionOutput == null)
        {
            return new FfmpegInfo(null, "none", Array.Empty<string>());
        }

        string? version = Regex.Match(versionOutput, @"ffmpeg version (\S+)").Groups[1].Value is { Length: > 0 } v ? v : null;
        string? encoders = RunProcess(path, "-hide_banner -encoders");
        var hardware = encoders == null
            ? new List<string>()
            : HardwareEncoder.Matches(encoders).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).Take(64).ToList();
        return new FfmpegInfo(version, bundled ? "bundled" : "system", hardware);
    }

    private static string? RunProcess(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            if (!process.Start()) return null;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ProcessTimeout))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }
            return output.Result;
        }
        catch
        {
            return null;
        }
    }
}
