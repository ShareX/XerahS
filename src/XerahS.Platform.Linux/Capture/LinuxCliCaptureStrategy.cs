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
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.Platform.Linux.Capture;

/// <summary>
/// Universal fallback capture strategy using CLI tools.
/// Tries: gnome-screenshot, spectacle, scrot, import (ImageMagick).
/// </summary>
internal sealed class LinuxCliCaptureStrategy : ICaptureStrategy
{
    private readonly string? _availableTool;

    public string Name => $"CLI ({_availableTool ?? "unknown"})";

    public LinuxCliCaptureStrategy()
    {
        _availableTool = DetectAvailableTool();
    }

    public static bool IsSupported()
    {
        return OperatingSystem.IsLinux();
    }

    public MonitorInfo[] GetMonitors()
    {
        // Try to use X11 for monitor enumeration if available
        if (X11GetImageStrategy.IsSupported())
        {
            var x11Strategy = new X11GetImageStrategy();
            return x11Strategy.GetMonitors();
        }

        // Fallback: assume single monitor
        return new[]
        {
            new MonitorInfo
            {
                Id = "0",
                Name = "Default Display",
                IsPrimary = true,
                Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
                WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
                // [2026-01-15] CLI tools return pre-scaled screenshots
                // Most CLI tools (gnome-screenshot, spectacle, scrot) respect
                // system DPI settings and return appropriately scaled images.
                // ScaleFactor = 1.0 indicates no additional scaling needed.
                ScaleFactor = 1.0,
                BitsPerPixel = 32
            }
        };
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        if (_availableTool == null)
            throw new InvalidOperationException("No screenshot tool available");

        var monitors = GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.Bounds.Intersect(physicalRegion) != null)
                   ?? monitors.First();

        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_capture_{Guid.NewGuid():N}.png");

        try
        {
            await CaptureWithTool(_availableTool, physicalRegion, tempFile);

            if (!File.Exists(tempFile))
                throw new InvalidOperationException($"{_availableTool} did not create output file");

            // Decode captured PNG
            var bitmap = SKBitmap.Decode(tempFile);
            if (bitmap == null)
                throw new InvalidOperationException("Failed to decode captured image");

            return new CapturedBitmap(bitmap, physicalRegion, monitor.ScaleFactor);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "Linux CLI Tools",
            Version = _availableTool ?? "none",
            SupportsHardwareAcceleration = false,
            SupportsCursorCapture = false,
            SupportsHDR = false,
            SupportsPerMonitorDpi = false,
            SupportsMonitorHotplug = false,
            MaxCaptureResolution = 16384,
            RequiresPermission = false
        };
    }

    private static string? DetectAvailableTool()
    {
        var tools = new[] { "gnome-screenshot", "spectacle", "scrot", "import" };

        foreach (var tool in tools)
        {
            if (IsCommandAvailable(tool))
                return tool;
        }

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };

            process.Start();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task CaptureWithTool(
        string tool,
        PhysicalRectangle region,
        string outputFile)
    {
        var arguments = tool switch
        {
            "gnome-screenshot" => $"-a -f \"{outputFile}\"",
            "spectacle" => $"-b -n -r -o \"{outputFile}\" -r {region.X},{region.Y},{region.Width},{region.Height}",
            "scrot" => $"-a {region.X},{region.Y},{region.Width},{region.Height} \"{outputFile}\"",
            "import" => $"-window root -crop {region.Width}x{region.Height}+{region.X}+{region.Y} \"{outputFile}\"",
            _ => throw new NotSupportedException($"Unknown tool: {tool}")
        };

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"{tool} failed: {error}");
        }
    }

    public void Dispose()
    {
        // No resources to clean up
    }
}
