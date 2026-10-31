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
using XerahS.Media;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.RegionCapture.ScreenRecording;

/// <summary>
/// FFmpeg-based screen recording service (fallback implementation)
/// Uses FFmpegCLIManager to orchestrate ffmpeg.exe process
/// Stage 4: Fallback for systems without Windows.Graphics.Capture support
/// </summary>
public class FFmpegRecordingService : IRecordingService
{
    private FFmpegCLIManager? _ffmpeg;
    private RecordingOptions? _currentOptions;
    private RecordingStatus _status = RecordingStatus.Idle;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private bool _disposed;
    private string? _ffmpegPath;
    private Task? _ffmpegTask;

    /// <summary>
    /// Gets or sets the path to ffmpeg.exe
    /// If null, will search in PATH and common locations
    /// </summary>
    public string? FFmpegPath
    {
        get => _ffmpegPath;
        set => _ffmpegPath = value;
    }

    /// <summary>
    /// FFmpeg options to use for encoding
    /// </summary>
    public FFmpegOptions? Options { get; set; }

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    public RecordingRuntimeCapabilities GetCapabilities(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return RecordingRuntimeCapabilities.SegmentedRestart;
    }

    public Task StartRecordingAsync(RecordingOptions options)
    {
        Console.WriteLine("[FFmpegRecordingService] StartRecordingAsync called");
        if (options == null) throw new ArgumentNullException(nameof(options));

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FFmpegRecordingService));
            if (_status != RecordingStatus.Idle)
            {
                throw new InvalidOperationException("Recording already in progress");
            }

            _currentOptions = options;
            UpdateStatus(RecordingStatus.Initializing);
        }

        try
        {
            // Locate ffmpeg.exe
            string ffmpegPath = FindFFmpegPath();
            Console.WriteLine($"[FFmpegRecordingService] FFmpeg Path resolved to: {ffmpegPath}");
            
            if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                string searched = $"Path not found. Checked: PATH, {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe")}, {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FFmpeg", "bin", "ffmpeg.exe")}";
                Console.WriteLine($"[FFmpegRecordingService] Critical Error: {searched}");
                DebugHelper.WriteLine($"[FFmpeg] {searched}");
                throw new FileNotFoundException($"ffmpeg.exe not found. {searched}");
            }

            // Build ffmpeg arguments
            string args = BuildFFmpegArguments(options);
            Console.WriteLine($"[FFmpegRecordingService] FFmpeg Arguments: {args}");

            // Create and start FFmpeg process
            _ffmpeg = new FFmpegCLIManager(ffmpegPath);
            _ffmpeg.ShowError = true;
            _ffmpeg.TrackEncodeProgress = true;

            Console.WriteLine("[FFmpegRecordingService] Starting FFmpeg process...");
            
            // Start FFmpeg in background
            _ffmpegTask = Task.Run(() =>
            {
                try
                {
                    lock (_lock)
                    {
                        _stopwatch.Restart();
                        UpdateStatus(RecordingStatus.Recording);
                    }
                    
                    Console.WriteLine("[FFmpegRecordingService] FFmpeg process running...");

                    bool success = _ffmpeg.Run(args);
                    
                    Console.WriteLine($"[FFmpegRecordingService] FFmpeg process finished. Success: {success}");

                    if (!success && !_ffmpeg.StopRequested)
                    {
                        Console.WriteLine($"[FFmpegRecordingService] FFmpeg process failed. Output: {_ffmpeg.Output}");
                        HandleFatalError(new Exception($"FFmpeg process failed.\nOutput: {_ffmpeg.Output}"), true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFmpegRecordingService] Exception in background task: {ex}");
                    HandleFatalError(ex, true);
                }
            });

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, true);
            throw;
        }
    }

    public async Task StopRecordingAsync()
    {
        Console.WriteLine("[FFmpegRecordingService] StopRecordingAsync called");
        FFmpegCLIManager? ffmpeg;
        Task? ffmpegTask;

        lock (_lock)
        {
            if (_status != RecordingStatus.Recording)
            {
                Console.WriteLine("[FFmpegRecordingService] StopRecordingAsync: Not recording (Status != Recording). Returning.");
                return; // Already stopped or never started
            }

            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();

            ffmpeg = _ffmpeg;
            ffmpegTask = _ffmpegTask;
        }

        try
        {
            Console.WriteLine("[FFmpegRecordingService] Sending 'q' to FFmpeg process...");
            // Send 'q' to FFmpeg to stop gracefully
            if (ffmpeg != null)
            {
                ffmpeg.StopRequested = true;
                ffmpeg.WriteInput("q");
            }

            bool exited = await WaitForFFmpegExitAsync(ffmpegTask, TimeSpan.FromSeconds(10));
            if (!exited && ffmpeg?.IsProcessRunning == true)
            {
                Console.WriteLine("[FFmpegRecordingService] FFmpeg process still running after timeout. Closing forcefully.");
                ffmpeg.Close();
                await WaitForFFmpegExitAsync(ffmpegTask, TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, false);
        }
        finally
        {
            lock (_lock)
            {
                _ffmpeg = null;
                _currentOptions = null;
                UpdateStatus(RecordingStatus.Idle);
            }
        }

        Console.WriteLine("[FFmpegRecordingService] StopRecordingAsync completed.");
    }

    private static async Task<bool> WaitForFFmpegExitAsync(Task? ffmpegTask, TimeSpan timeout)
    {
        if (ffmpegTask == null)
        {
            return true;
        }

        try
        {
            var completed = await Task.WhenAny(ffmpegTask, Task.Delay(timeout));
            return completed == ffmpegTask;
        }
        catch
        {
            return false;
        }
    }

    private string FindFFmpegPath()
    {
        // 1. Check if explicitly set
        if (!string.IsNullOrEmpty(_ffmpegPath))
        {
            DebugHelper.WriteLine($"[FFmpeg] Checking explicitly set path: {_ffmpegPath}");
            if (File.Exists(_ffmpegPath))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at explicitly set path: {_ffmpegPath}");
                return _ffmpegPath;
            }
            DebugHelper.WriteLine($"[FFmpeg] FFmpeg not found at explicitly set path.");
        }

        // 2. Check workflow recording options for a configured override path
        if (!string.IsNullOrEmpty(_currentOptions?.FFmpegOverridePath))
        {
            DebugHelper.WriteLine($"[FFmpeg] Checking RecordingOptions.FFmpegOverridePath: {_currentOptions.FFmpegOverridePath}");
            if (File.Exists(_currentOptions.FFmpegOverridePath))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at RecordingOptions.FFmpegOverridePath: {_currentOptions.FFmpegOverridePath}");
                return _currentOptions.FFmpegOverridePath;
            }
            DebugHelper.WriteLine("[FFmpeg] FFmpeg not found at RecordingOptions.FFmpegOverridePath.");
        }

        // 3. Check service-level options for a configured override path
        if (Options?.OverrideCLIPath == true && !string.IsNullOrEmpty(Options.CLIPath))
        {
            DebugHelper.WriteLine($"[FFmpeg] Checking Options.CLIPath: {Options.CLIPath}");
            if (File.Exists(Options.CLIPath))
            {
                DebugHelper.WriteLine($"[FFmpeg] Found FFmpeg at Options.CLIPath: {Options.CLIPath}");
                return Options.CLIPath;
            }
            DebugHelper.WriteLine($"[FFmpeg] FFmpeg not found at Options.CLIPath.");
        }

        // 4. Use Centralized PathsManager
        return PathsManager.GetFFmpegPath();
    }

    // MMDevice endpoint IDs are native WASAPI settings. DirectShow needs a friendly microphone name.
    // This pure builder is also used to verify that fallback never drops one of two requested sources.
    internal static IReadOnlyList<string> BuildWindowsAudioArguments(ScreenRecordingSettings settings, IRecordingDeviceService? devices)
    {
        List<string> args = [];
        if (settings.CaptureSystemAudio)
        {
            if (!string.IsNullOrWhiteSpace(settings.SystemAudioDeviceId) &&
                devices?.GetDefaultSystemAudioDevice()?.Id != settings.SystemAudioDeviceId)
                throw new NotSupportedException("FFmpeg's Stereo Mix fallback cannot select a WASAPI output endpoint. Use the default output or native recording.");
            args.Add("-thread_queue_size 512 -f dshow -i audio=\"Stereo Mix\"");
        }
        if (settings.CaptureMicrophone)
        {
            string? name = settings.MicrophoneDeviceId;
            if (devices != null)
            {
                var selected = string.IsNullOrWhiteSpace(name) ? devices.GetDefaultMicrophone()
                    : devices.GetMicrophones().FirstOrDefault(device => device.Id == name || device.Name == name);
                if (selected == null) throw new ArgumentException("The selected microphone is no longer available.");
                name = selected.Name;
            }
            name ??= "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\\wave_{00000000-0000-0000-0000-000000000000}";
            // Names are passed inside a quoted ProcessStartInfo argument, not through a shell.
            if (name.Contains('"')) throw new ArgumentException("FFmpeg cannot use this microphone name. Select native recording.");
            args.Add($"-thread_queue_size 512 -f dshow -i audio=\"{name}\"");
        }
        if (settings.CaptureSystemAudio && settings.CaptureMicrophone)
        {
            string systemGain = settings.SystemAudioGain.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string micGain = settings.MicrophoneGain.ToString(System.Globalization.CultureInfo.InvariantCulture);
            args.Add($"-filter_complex \"[1:a]volume={systemGain}[system];[2:a]volume={micGain}[mic];[system][mic]amix=inputs=2:duration=longest:normalize=0[mixed]\"");
            args.Add("-map 0:v -map \"[mixed]\"");
        }
        else if (settings.CaptureSystemAudio || settings.CaptureMicrophone)
            args.Add("-map 0:v -map 1:a");
        return args;
    }

    private string BuildFFmpegArguments(RecordingOptions options)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        bool hasAudio = settings.CaptureSystemAudio || settings.CaptureMicrophone;
        var args = new List<string>();

        // === Video input (input 0) ===
        if (OperatingSystem.IsWindows())
        {
            switch (options.Mode)
            {
                case CaptureMode.Screen:
                case CaptureMode.Window: // Fallback to region/screen for now
                case CaptureMode.Region:
                    // Use gdigrab for screen capture on Windows
                    args.Add("-f gdigrab");
                    args.Add("-framerate " + settings.FPS);
                    if (settings.ShowCursor)
                    {
                        args.Add("-draw_mouse 1");
                    }
                    else
                    {
                         args.Add("-draw_mouse 0");
                    }

                    var region = options.Region;
                    if (options.Mode == CaptureMode.Screen && !string.IsNullOrWhiteSpace(settings.DisplayId))
                        region = PlatformServices.Screen.GetAllScreens().FirstOrDefault(screen => screen.DeviceName == settings.DisplayId)?.Bounds
                            ?? throw new ArgumentException("The selected recording display is no longer available.");
                    if (options.Mode == CaptureMode.Window && options.TargetWindowHandle != IntPtr.Zero)
                        region = PlatformServices.Window.GetWindowBounds(options.TargetWindowHandle);
                    if (region.Width > 0 && region.Height > 0)
                    {
                        args.Add($"-offset_x {region.X}");
                        args.Add($"-offset_y {region.Y}");
                        args.Add($"-video_size {region.Width & ~1}x{region.Height & ~1}");
                    }

                    args.Add("-i desktop");
                    break;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
             // macOS uses avfoundation
             args.Add("-f avfoundation");
             args.Add("-framerate " + settings.FPS);

             if (settings.ShowCursor)
             {
                 args.Add("-capture_cursor 1");
             }
             else
             {
                 args.Add("-capture_cursor 0");
             }

             // Mouse clicks visualization optional
             // args.Add("-capture_mouse_clicks 1");

             // For now default to screen 1.
             // Ideally we would enumerate screens or look at options.Region to determine screen index.
             // But for MVP/Fix, "1" or "1:" is usually main screen.
             // Using "1" as video input.
             // Note: avfoundation input format is "video_device:audio_device" or just "video_device"
             args.Add("-i \"1\"");

             // Region capture in avfoundation is not directly supported via offset/size args like gdigrab
             // It usually requires a complex filter chain (crop).
             if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
             {
                 // Add crop filter: crop=w:h:x:y
                 args.Add($"-vf \"crop={options.Region.Width}:{options.Region.Height}:{options.Region.X}:{options.Region.Y}\"");
             }
        }
        else if (OperatingSystem.IsLinux())
        {
             // Basic Linux fallback (x11grab, X11 only).
             // Guard against invalid 0x0 region args for non-region capture modes.
             args.Add("-f x11grab");
             args.Add("-framerate " + settings.FPS);

             bool hasRegion = options.Region.Width > 0 && options.Region.Height > 0;
             bool useRegionInput = hasRegion && (options.Mode == CaptureMode.Region || options.Mode == CaptureMode.Window);

             if (useRegionInput)
             {
                 args.Add($"-video_size {options.Region.Width}x{options.Region.Height}");
                 args.Add($"-i :0.0+{options.Region.X},{options.Region.Y}");
             }
             else
             {
                 args.Add("-i :0.0");
             }

             args.Add(settings.ShowCursor ? "-draw_mouse 1" : "-draw_mouse 0");
        }

        // === Audio input (input 1) — must come right after video input, before codec/output args ===
        if (hasAudio)
        {
            if (OperatingSystem.IsWindows())
            {
                args.AddRange(BuildWindowsAudioArguments(settings, PlatformServices.RecordingDevices));
            }
            else if (OperatingSystem.IsLinux())
            {
                args.Add("-f pulse");
                if (settings.CaptureSystemAudio)
                {
                    // System audio requires a PulseAudio monitor source, not the default input
                    args.Add("-i default.monitor");
                    DebugHelper.WriteLine("FFmpeg: Capturing system audio via PulseAudio monitor (Linux)");
                }
                else if (settings.CaptureMicrophone)
                {
                    args.Add(!string.IsNullOrEmpty(settings.MicrophoneDeviceId)
                        ? $"-i {settings.MicrophoneDeviceId}"
                        : "-i default");
                    DebugHelper.WriteLine("FFmpeg: Capturing microphone via PulseAudio (Linux)");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                args.Add("-f avfoundation");
                args.Add("-i \":0\""); // Default audio device
                DebugHelper.WriteLine("FFmpeg: Capturing audio via avfoundation (macOS)");
            }
        }

        // === Stream mapping — required when there are multiple inputs ===
        if (hasAudio && !OperatingSystem.IsWindows())
        {
            args.Add("-map 0:v");
            args.Add("-map 1:a");
        }

        // === Video codec settings ===
        switch (settings.Codec)
        {
            case VideoCodec.H264:
                args.Add("-c:v libx264");
                args.Add("-preset ultrafast");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;

            case VideoCodec.HEVC:
                args.Add("-c:v libx265");
                args.Add("-preset ultrafast");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;

            case VideoCodec.VP9:
                args.Add("-c:v libvpx-vp9");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;

            case VideoCodec.AV1:
                args.Add("-c:v libaom-av1");
                args.Add($"-b:v {settings.BitrateKbps}k");
                break;
        }

        // === Audio codec settings ===
        if (hasAudio)
        {
            args.Add("-c:a aac");
            args.Add("-b:a 192k");
        }

        // Output options
        args.Add("-pix_fmt yuv420p");
        args.Add("-y"); // Overwrite output file

        // Output path
        string outputPath = ResolveOutputPath(options.OutputPath);
        args.Add($"\"{outputPath}\"");

        return string.Join(" ", args);
    }

    private string ResolveOutputPath(string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            string fullPath = Path.GetFullPath(outputPath);
            string? outputDirectory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            return fullPath;
        }

        return GetDefaultOutputPath();
    }

    private string GetDefaultOutputPath()
    {
        // Default pattern: ShareX/Screenshots/yyyy-MM/Date_Time.mp4
        // Use PathsManager.ScreenshotsFolder (or potentially a new ScreencastsFolder logic if preferred, keeping it simple for now)
        // Wait, FFmpegRecordingService is for recording, so it should probably use ScreencastsFolder.
        // Let's check what I planned. "Update GetDefaultOutputPath to use PathsManager.ScreenshotsFolder (or ScreencastsFolder to match logic)."
        // ScreencastsFolder makes more sense.
        
        string screencastsFolder = PathsManager.ScreencastsFolder;
        Directory.CreateDirectory(screencastsFolder);

        string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
        return Path.Combine(screencastsFolder, fileName);
    }

    private void UpdateStatus(RecordingStatus newStatus)
    {
        lock (_lock)
        {
            if (_status == newStatus) return;

            _status = newStatus;
            var duration = _stopwatch.Elapsed;

            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(newStatus, duration));
        }
    }

    private void HandleFatalError(Exception ex, bool isFatal)
    {
        lock (_lock)
        {
            if (_status != RecordingStatus.Error)
            {
                UpdateStatus(RecordingStatus.Error);
            }
        }

        ErrorOccurred?.Invoke(this, new RecordingErrorEventArgs(ex, isFatal));

        if (isFatal)
        {
            // Cleanup
            try
            {
                _ffmpeg?.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }

            _ffmpeg = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;

            try
            {
                if (_status == RecordingStatus.Recording)
                {
                    StopRecordingAsync().Wait();
                }
            }
            catch
            {
                // Best effort cleanup
            }

            _ffmpeg?.Close();
            _ffmpeg = null;
        }

        GC.SuppressFinalize(this);
    }
}
