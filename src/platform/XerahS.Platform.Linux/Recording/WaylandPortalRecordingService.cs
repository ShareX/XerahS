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

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Media;
using XerahS.Platform.Linux.Capture;
using XerahS.Platform.Linux.Services;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Platform.Linux.Recording;

/// <summary>
/// Wayland screen recording via XDG ScreenCast portal with wf-recorder, FFmpeg, or GStreamer.
/// Falls back to FFmpegRecordingService if portal negotiation fails.
/// </summary>
public sealed class WaylandPortalRecordingService : IRecordingService
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    private FFmpegCLIManager? _ffmpeg;
    private Process? _gstreamerProcess;
    private int? _gstreamerPid;
    private Task? _ffmpegTask;
    private bool _usingFFmpegEncodingBridge;
    private RecordingOptions? _currentOptions;
    private RecordingStatus _status = RecordingStatus.Idle;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private bool _disposed;
    private bool _stopRequested;
    private Timer? _durationTimer;
    private string? _gstreamerOutputPath;

    private Connection? _connection;
    private IScreenCastPortal? _portal;
    private IPortalSession? _sessionProxy;
    private ObjectPath? _sessionHandle;
    private uint _pipewireNodeId;
    // Portal-scoped PipeWire connection (OpenPipeWireRemote). Window streams on
    // xdg-desktop-portal-hyprland only negotiate through it.
    private SafeFileHandle? _pipewireRemote;
    private static readonly object SpawnWithRemoteLock = new();
    private int _pipewireSourceWidth;
    private int _pipewireSourceHeight;

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    public RecordingRuntimeCapabilities GetCapabilities(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // wf-recorder can be safely restarted without re-opening the portal session flow.
        // Portal-backed GStreamer/FFmpeg recording requires a fresh portal session and should
        // not be exposed through the shared segmented pause path.
        return CanUseWfRecorder(options)
            ? RecordingRuntimeCapabilities.SegmentedRestart
            : new RecordingRuntimeCapabilities(
                RecordingPauseBehavior.Unsupported,
                RequiresPersistentSession: true);
    }

    public Task StartRecordingAsync(RecordingOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WaylandPortalRecordingService));
            if (_status != RecordingStatus.Idle)
            {
                throw new InvalidOperationException("Recording already in progress");
            }

            _currentOptions = options;
            UpdateStatus(RecordingStatus.Initializing);
        }

        try
        {
            EnsureWayland();

            // Prefer wf-recorder only on wlroots compositors and scenarios it supports well.
            if (CanUseWfRecorder(options))
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] Using wf-recorder (handles portal internally)");
                return StartWithWfRecorder(options);
            }

            // Fall back to portal + GStreamer/FFmpeg approach
            InitializePortalSession(options).GetAwaiter().GetResult();

            if (TryStartFFmpegEncodingBridge(options))
            {
                return Task.CompletedTask;
            }

            bool preferCpuGStreamer = ShouldPreferCpuGStreamerPath();
            if (preferCpuGStreamer)
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] GNOME Wayland detected; preferring CPU GStreamer pipeline to avoid black GL captures.");
            }

            var (executable, args, useGStreamer) = BuildRecordingCommand(
                options,
                _pipewireNodeId,
                _pipewireSourceWidth,
                _pipewireSourceHeight,
                preferCpuGStreamer,
                RemoteFd);
            DebugHelper.WriteLine($"[WaylandPortalRecording] Using {(useGStreamer ? "GStreamer" : "FFmpeg")}");
            DebugHelper.WriteLine($"[WaylandPortalRecording] Command: {executable} {args}");

            if (useGStreamer)
            {
                // Build CPU-only fallback pipeline in case the GL path fails.
                // Two known failure modes on GNOME/Wayland:
                //   (a) GL path with video/x-raw filter → not-negotiated when pipewiresrc only offers DMABuf
                //   (b) GL path without filter → glupload "unhandled format" on some systems
                // The fallback (no GL) uses videoconvert which handles both raw and DMABuf universally.
                string? fallbackGstArgs = null;
                bool primaryUsesGl = !preferCpuGStreamer && HasGStreamerElement("gldownload") && HasGStreamerElement("glupload");
                if (primaryUsesGl)
                {
                    // options.OutputPath was resolved (and possibly extension-adjusted) by BuildRecordingCommand above.
                    var (fallbackPipeline, _) = BuildGStreamerPipeline(options, _pipewireNodeId,
                        options.OutputPath ?? string.Empty, _pipewireSourceWidth, _pipewireSourceHeight, useGl: false, remoteFd: RemoteFd);
                    fallbackGstArgs = "-e " + fallbackPipeline;
                    DebugHelper.WriteLine("[WaylandPortalRecording] CPU fallback pipeline ready (will use if GL path fails)");
                }

                var capturedExecutable = executable;
                var capturedPrimaryArgs = args;
                var capturedFallbackArgs = fallbackGstArgs;
                _ffmpegTask = Task.Run(() => RunGStreamerWithFallback(capturedExecutable, capturedPrimaryArgs, capturedFallbackArgs));
            }
            else
            {
                // Use FFmpeg via FFmpegCLIManager
                if (string.IsNullOrEmpty(executable) || (!string.Equals(executable, "ffmpeg", StringComparison.Ordinal) && !File.Exists(executable)))
                {
                    throw new FileNotFoundException("FFmpeg not found for Wayland portal recording.", executable);
                }

                _ffmpeg = new FFmpegCLIManager(executable)
                {
                    ShowError = true,
                    TrackEncodeProgress = true
                };

                _ffmpegTask = Task.Run(() =>
                {
                    try
                    {
                        lock (_lock)
                        {
                            _stopwatch.Restart();
                            UpdateStatus(RecordingStatus.Recording);
                        }

                        bool success = _ffmpeg.Run(args);
                        if (!success && !_ffmpeg.StopRequested)
                        {
                            HandleFatalError(new Exception($"FFmpeg process failed.\nOutput: {_ffmpeg.Output}"), true);
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleFatalError(ex, true);
                    }
                });
            }

            return Task.CompletedTask;
        }
        catch (DBusException ex)
        {
            CleanupPortalSession();
            throw new PlatformNotSupportedException("Wayland ScreenCast portal unavailable.", ex);
        }
        catch (Exception ex)
        {
            CleanupPortalSession();
            HandleFatalError(ex, true);
            throw;
        }
    }

    /// <summary>
    /// Tries the primary GStreamer pipeline; if it fails and a fallback is available, retries with the fallback.
    /// Only calls HandleFatalError when all candidates are exhausted.
    /// </summary>
    private void RunGStreamerWithFallback(string executable, string primaryArgs, string? fallbackArgs)
    {
        // Stop may have been signalled before the background thread even starts GStreamer.
        if (_stopRequested) return;

        bool primaryFailed = RunGStreamerProcess(executable, primaryArgs, out string primaryOutput);

        if (!primaryFailed || _stopRequested)
            return;

        if (fallbackArgs != null)
        {
            DebugHelper.WriteLine("[WaylandPortalRecording] Primary (GL) pipeline failed; retrying with CPU fallback pipeline...");
            bool fallbackFailed = RunGStreamerProcess(executable, fallbackArgs, out string fallbackOutput);
            if (fallbackFailed && !_stopRequested)
            {
                HandleFatalError(new Exception(
                    $"GStreamer: both GL and CPU pipelines failed.\nGL output: {primaryOutput}\nCPU output: {fallbackOutput}"), true);
            }
        }
        else
        {
            HandleFatalError(new Exception($"GStreamer process failed.\nOutput: {primaryOutput}"), true);
        }
    }

    /// <summary>
    /// Records through GStreamer capture + FFmpeg encoding when GStreamer cannot encode the
    /// requested codec itself (missing x264enc/mp4mux/vp9enc…) or the user forces FFmpeg.
    /// Returns false to fall through to the native pipelines. Missing plugins are reported to the
    /// user with an install command for their distribution either way.
    /// </summary>
    private bool TryStartFFmpegEncodingBridge(RecordingOptions options)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        string ffmpegPath = ResolveConfiguredFFmpegPath(options);
        if (HasFFmpegPipewireSupport(ffmpegPath) || !HasGStreamerPipewireSupport())
        {
            return false; // ffmpeg reads PipeWire itself, or there is nothing to capture with
        }

        bool withAudio = settings.CaptureSystemAudio || settings.CaptureMicrophone;
        GStreamerPluginAdvice advice = GStreamerPluginAdvisor.Check(settings.Codec, withAudio, HasGStreamerElement);
        bool nativeOk = HasNativeGStreamerEncoding(settings.Codec);
        FFmpegFeatures features = FFmpegEncodingBridge.GetFeatures(ffmpegPath);
        FFmpegEncoderPlan? plan = FFmpegEncodingBridge.PlanEncoders(settings.Codec, settings.BitrateKbps, features);

        if ((nativeOk && !settings.ForceFFmpeg) || plan == null)
        {
            if (!nativeOk)
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] No usable FFmpeg encoder for the fallback; using GStreamer's last-resort encoder.");
                GStreamerPluginAdvisor.Notify(advice, usedFfmpegFallback: false, hasUsableFFmpegEncoder: plan != null);
            }

            return false;
        }

        string sourceElement = BuildPipeWireSource(_pipewireNodeId, RemoteFd);
        if (ProbeFrameSize(sourceElement) is not (int frameWidth, int frameHeight))
        {
            // Raw frames need an exact size; without one, use GStreamer's own encoders.
            DebugHelper.WriteLine("[WaylandPortalRecording] Could not determine the stream's frame size; not using the FFmpeg bridge.");
            // FFmpeg itself is fine here, so a download would not help.
            GStreamerPluginAdvisor.Notify(advice, usedFfmpegFallback: false, hasUsableFFmpegEncoder: true);
            return false;
        }

        WarnIfStreamShapeDiffers(frameWidth, frameHeight);

        string? crop = null;
        int width = frameWidth;
        int height = frameHeight;
        if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
        {
            int left = Math.Max(0, options.Region.X);
            int top = Math.Max(0, options.Region.Y);
            crop = $"videocrop left={left} top={top} right={Math.Max(0, frameWidth - left - options.Region.Width)} bottom={Math.Max(0, frameHeight - top - options.Region.Height)}";
            width = options.Region.Width;
            height = options.Region.Height;
        }

        string? pulseDevice = null;
        if (withAudio)
        {
            if (features.HasPulse)
            {
                pulseDevice = settings.CaptureSystemAudio
                    ? PulseAudioHelper.GetDefaultMonitorSource()
                    : string.IsNullOrEmpty(settings.MicrophoneDeviceId) ? "default" : settings.MicrophoneDeviceId;
            }
            else
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] This FFmpeg build has no PulseAudio input; recording video only.");
            }
        }

        string requested = options.OutputPath ?? GetDefaultOutputPath();
        string outputPath = Path.Combine(Path.GetDirectoryName(requested) ?? string.Empty, Path.GetFileNameWithoutExtension(requested) + plan.Extension);
        options.OutputPath = outputPath;

        string captureArgs = FFmpegEncodingBridge.BuildCaptureArgs(sourceElement, width, height, crop);
        string encodeArgs = FFmpegEncodingBridge.BuildEncodeArgs(width, height, settings.FPS, plan, pulseDevice, outputPath);
        DebugHelper.WriteLine($"[WaylandPortalRecording] Using GStreamer capture + FFmpeg encoding ({plan.VideoEncoder}{(pulseDevice != null ? " + " + plan.AudioEncoder : string.Empty)}), frame {FFmpegEncodingBridge.RawWidth(width)}x{FFmpegEncodingBridge.Even(height)} (source {frameWidth}x{frameHeight})");
        DebugHelper.WriteLine($"[WaylandPortalRecording] Capture: gst-launch-1.0 {captureArgs}");
        DebugHelper.WriteLine($"[WaylandPortalRecording] Encode: {ffmpegPath} {encodeArgs}");

        GStreamerPluginAdvisor.Notify(advice, usedFfmpegFallback: true, hasUsableFFmpegEncoder: true);
        StartFFmpegEncodingBridge(captureArgs, ffmpegPath, encodeArgs, outputPath, width, height);
        return true;
    }

    internal Task StartFFmpegEncodingBridge(string captureArgs, string ffmpegPath, string encodeArgs, string outputPath, int width, int height)
    {
        lock (_lock)
        {
            // The worker takes the same lock before publishing Recording, so stop always sees
            // both its task and its child process, even if the hotkey arrives during startup.
            _gstreamerOutputPath = outputPath;
            _usingFFmpegEncodingBridge = true;
            _ffmpegTask = Task.Run(() => RunFFmpegEncodingBridge(captureArgs, ffmpegPath, encodeArgs, outputPath, width, height));
            return _ffmpegTask;
        }
    }

    private static bool HasNativeGStreamerEncoding(VideoCodec codec)
    {
        bool mp4 = HasGStreamerElement("mp4mux");
        bool vp9 = HasGStreamerElement("vp9enc") && HasGStreamerElement("webmmux");
        bool h264 = (HasGStreamerElement("x264enc") || HasGStreamerElement("nvh264enc")) && mp4;
        return codec switch
        {
            VideoCodec.VP9 => vp9,
            VideoCodec.AV1 => (HasGStreamerElement("av1enc") && HasGStreamerElement("webmmux")) || vp9,
            VideoCodec.HEVC => ((HasGStreamerElement("x265enc") || HasGStreamerElement("nvh265enc")) && mp4) || vp9,
            _ => h264 || vp9,
        };
    }

    /// <summary>
    /// Logs when the negotiated stream does not have the shape the portal reported. Scaling changes
    /// the size but not the aspect ratio, so a different ratio means pipewiresrc may be reading
    /// another source (for example the webcam after a failed target match).
    /// </summary>
    private void WarnIfStreamShapeDiffers(int frameWidth, int frameHeight)
    {
        if (StreamShapeDiffers(_pipewireSourceWidth, _pipewireSourceHeight, frameWidth, frameHeight))
        {
            DebugHelper.WriteLine(
                $"[WaylandPortalRecording] Warning: stream frames are {frameWidth}x{frameHeight} but the portal reported " +
                $"{_pipewireSourceWidth}x{_pipewireSourceHeight}; the recording may not show the selected screen or window.");
        }
    }

    internal static bool StreamShapeDiffers(int portalWidth, int portalHeight, int frameWidth, int frameHeight)
    {
        if (portalWidth <= 0 || portalHeight <= 0 || frameWidth <= 0 || frameHeight <= 0)
        {
            return false;
        }

        double portalRatio = (double)portalWidth / portalHeight;
        double frameRatio = (double)frameWidth / frameHeight;
        return Math.Abs(portalRatio - frameRatio) / portalRatio > 0.05;
    }

    /// <summary>
    /// The real frame size of the portal stream. The portal's reported size is logical and can
    /// differ from the buffers (fractional scaling), so negotiate one frame and read its caps.
    /// </summary>
    private (int Width, int Height)? ProbeFrameSize(string sourceElement)
    {
        try
        {
            using Process? probe = StartWithInheritedRemote(new ProcessStartInfo("gst-launch-1.0", FFmpegEncodingBridge.BuildProbeArgs(sourceElement))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (probe != null)
            {
                Task<string> stdout = probe.StandardOutput.ReadToEndAsync();
                Task<string> stderr = probe.StandardError.ReadToEndAsync();
                bool completed = probe.WaitForExit(6000);
                if (!completed)
                {
                    try { probe.Kill(entireProcessTree: true); } catch { }
                }

                string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                if (completed && probe.ExitCode == 0 && FFmpegEncodingBridge.ParseProbedSize(output) is { Width: > 0, Height: > 0 } size)
                {
                    DebugHelper.WriteLine($"[WaylandPortalRecording] Stream frame size: {size.Width}x{size.Height}");
                    return size;
                }

                DebugHelper.WriteLine("[WaylandPortalRecording] Frame size probe output:\n" + output[^Math.Min(output.Length, 1500)..]);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[WaylandPortalRecording] Frame size probe failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>Pumps raw frames from GStreamer into FFmpeg until GStreamer reaches EOS (stop).</summary>
    private void RunFFmpegEncodingBridge(string captureArgs, string ffmpegPath, string encodeArgs, string outputPath, int width, int height)
    {
        Process? gst = null;
        Process? ffmpeg = null;
        try
        {
            lock (_lock)
            {
                if (_stopRequested) return;

                ffmpeg = Process.Start(new ProcessStartInfo(ffmpegPath, encodeArgs)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }) ?? throw new InvalidOperationException("Could not start FFmpeg.");

                gst = StartWithInheritedRemote(new ProcessStartInfo("gst-launch-1.0", captureArgs)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }) ?? throw new InvalidOperationException("Could not start GStreamer.");
                // Publish the process before StopRecordingAsync can take its snapshot. Otherwise
                // a quick stop can miss the child and leave the bridge waiting on a live pipe.
                _gstreamerProcess = gst;
                _gstreamerPid = gst.Id;
                _stopwatch.Restart();
                UpdateStatus(RecordingStatus.Recording);
            }

            Task<string> ffmpegErrors = ffmpeg.StandardError.ReadToEndAsync();
            _ = ffmpeg.StandardOutput.ReadToEndAsync();
            Task<string> gstErrors = gst.StandardError.ReadToEndAsync();

            bool encoderDied = false;
            long framesCopied = 0;
            try
            {
                framesCopied = FFmpegEncodingBridge.CopyRawFrames(gst.StandardOutput.BaseStream, ffmpeg.StandardInput.BaseStream, width, height);
            }
            catch (IOException)
            {
                encoderDied = true; // ffmpeg exited and closed its stdin
            }

            try { ffmpeg.StandardInput.Close(); } catch (IOException) { }

            if (encoderDied && !gst.HasExited)
            {
                try { gst.Kill(entireProcessTree: true); } catch { }
            }

            gst.WaitForExit();
            if (!ffmpeg.WaitForExit(60000))
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] FFmpeg did not finish in time; killing it.");
                try { ffmpeg.Kill(entireProcessTree: true); } catch { }
                ffmpeg.WaitForExit();
            }

            string gstOutput = gstErrors.GetAwaiter().GetResult().Trim();
            string ffmpegOutput = ffmpegErrors.GetAwaiter().GetResult().Trim();
            bool fileWritten = File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            DebugHelper.WriteLine($"[WaylandPortalRecording] Bridge finished: gst={gst.ExitCode} ffmpeg={ffmpeg.ExitCode} frames={framesCopied} output={(fileWritten ? new FileInfo(outputPath).Length + " bytes" : "missing")}");
            if (gst.ExitCode != 0 && !_stopRequested && gstOutput.Length > 0)
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] GStreamer capture stderr:\n" + gstOutput);
            }

            if (ffmpeg.ExitCode != 0 && ffmpegOutput.Length > 0)
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] FFmpeg stderr:\n" + ffmpegOutput);
            }

            if (!_stopRequested && gst.ExitCode != 0)
            {
                HandleFatalError(new Exception($"GStreamer capture failed.\n{gstOutput}"), true);
            }
            else if (ffmpeg.ExitCode != 0)
            {
                HandleFatalError(new Exception($"FFmpeg encoding failed.\n{ffmpegOutput}"), true);
            }
            else if (!fileWritten)
            {
                HandleFatalError(new Exception("Recording finished without a video file."), true);
            }
        }
        catch (Exception ex)
        {
            try { if (gst is { HasExited: false }) gst.Kill(entireProcessTree: true); } catch { }
            try { if (ffmpeg is { HasExited: false }) ffmpeg.Kill(entireProcessTree: true); } catch { }
            HandleFatalError(ex, true);
        }
        finally
        {
            gst?.Dispose();
            ffmpeg?.Dispose();
        }
    }

    /// <summary>
    /// Starts gst-launch with the portal remote fd inheritable, so "pipewiresrc fd=N" in the child
    /// refers to the same connection. .NET opens fds close-on-exec; the flag is cleared only for
    /// this spawn and restored right after.
    /// </summary>
    private Process? StartWithInheritedRemote(ProcessStartInfo startInfo)
    {
        int fd = RemoteFd;
        if (fd < 0)
        {
            return Process.Start(startInfo);
        }

        lock (SpawnWithRemoteLock)
        {
            int flags = NativeFcntl.fcntl(fd, NativeFcntl.F_GETFD, 0);
            if (flags < 0 || NativeFcntl.fcntl(fd, NativeFcntl.F_SETFD, flags & ~NativeFcntl.FD_CLOEXEC) != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not pass the selected screen's PipeWire connection to GStreamer.");
            }

            try
            {
                return Process.Start(startInfo);
            }
            finally
            {
                if (NativeFcntl.fcntl(fd, NativeFcntl.F_SETFD, flags) != 0)
                {
                    DebugHelper.WriteLine($"[WaylandPortalRecording] Could not restore close-on-exec on portal remote fd {fd}.");
                }
            }
        }
    }

    private static class NativeFcntl
    {
        public const int F_GETFD = 1;
        public const int F_SETFD = 2;
        public const int FD_CLOEXEC = 1;

        [DllImport("libc", SetLastError = true)]
        public static extern int fcntl(int fd, int cmd, int arg);
    }

    /// <summary>
    /// Runs a single GStreamer process and waits for it to exit.
    /// Returns true if the process failed and the caller should consider a retry.
    /// </summary>
    private bool RunGStreamerProcess(string executable, string args, out string stderrOutput)
    {
        stderrOutput = string.Empty;
        try
        {
            lock (_lock)
            {
                _stopwatch.Restart();
                UpdateStatus(RecordingStatus.Recording);
            }

            // Re-check after taking the lock: stop may have arrived between RunGStreamerWithFallback's
            // early check and this point.
            if (_stopRequested) { stderrOutput = string.Empty; return false; }

            // Extract the output file path from the GStreamer args so we can check for
            // partial recordings on failure. The path follows filesink location="..."
            _gstreamerOutputPath = ExtractGStreamerOutputPath(args);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = StartWithInheritedRemote(startInfo);
            if (process == null)
            {
                stderrOutput = "Failed to start GStreamer process";
                return true;
            }

            _gstreamerProcess = process;
            try { _gstreamerPid = process.Id; } catch { }

            // Drain both pipes concurrently: an unread stdout pipe can stall gst-launch.
            Task stdoutDrain = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
            stderrOutput = process.StandardError.ReadToEnd();
            process.WaitForExit();
            stdoutDrain.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                DebugHelper.WriteLine($"[WaylandPortalRecording] GStreamer stderr:\n{stderrOutput}");

                // If the user did not request the stop through XerahS, the portal session
                // may have been terminated externally (e.g. GNOME's screencast indicator).
                // When that happens GStreamer receives a broken PipeWire stream and exits
                // with a not-negotiated error, but it may have already flushed valid data
                // to the segment file. Treat this as a successful (partial) recording
                // rather than a fatal crash so the user gets their video.
                if (!_stopRequested && !string.IsNullOrEmpty(_gstreamerOutputPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(_gstreamerOutputPath);
                        if (fileInfo.Exists && fileInfo.Length > 0)
                        {
                            RecordingOutputValidator.EnsureUsable(fileInfo.FullName);
                            DebugHelper.WriteLine(
                                $"[WaylandPortalRecording] GStreamer exited with error but segment file exists " +
                                $"({fileInfo.Length} bytes). Treating as external stop (partial recording recovered).");
                            return false; // treat as success — don't retry, don't error
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteLine($"[WaylandPortalRecording] Error checking segment file: {ex.Message}");
                    }
                }

                return !_stopRequested; // failed → caller should retry (unless user stopped)
            }

            return false; // exited cleanly
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, true);
            stderrOutput = ex.Message;
            return false; // unexpected exception: don't retry
        }
    }

    /// <summary>
    /// Extracts the output file path from a GStreamer pipeline argument string.
    /// Looks for the filesink location="..." pattern.
    /// </summary>
    internal static string? ExtractGStreamerOutputPath(string args)
    {
        // Pattern: filesink location="/path/to/file.webm"
        const string marker = "filesink location=";
        int idx = args.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;

        int pathStart = idx + marker.Length;
        if (pathStart >= args.Length) return null;

        // Handle quoted path
        if (args[pathStart] == '"')
        {
            int closeQuote = args.IndexOf('"', pathStart + 1);
            return closeQuote > pathStart
                ? args.Substring(pathStart + 1, closeQuote - pathStart - 1)
                : null;
        }

        // Unquoted: take until next space
        int spaceIdx = args.IndexOf(' ', pathStart);
        return spaceIdx > pathStart
            ? args.Substring(pathStart, spaceIdx - pathStart)
            : args.Substring(pathStart);
    }

    /// <summary>
    /// Start recording using wf-recorder (handles portal integration internally).
    /// This is the preferred method on wlroots-based compositors (Hyprland, Sway).
    /// </summary>
    private Task StartWithWfRecorder(RecordingOptions options)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        string outputPath = options.OutputPath ?? GetDefaultOutputPath();

        var args = new List<string>();

        // Add geometry for region capture (wf-recorder uses "x,y WxH" format)
        if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
        {
            args.Add($"-g \"{options.Region.X},{options.Region.Y} {options.Region.Width}x{options.Region.Height}\"");
        }

        // Codec selection - wf-recorder prefers VAAPI for hardware encoding
        string codec = settings.Codec switch
        {
            VideoCodec.H264 => "libx264",  // Could use h264_vaapi if available
            VideoCodec.HEVC => "libx265",  // Could use hevc_vaapi if available
            VideoCodec.VP9 => "libvpx-vp9",
            VideoCodec.AV1 => "libaom-av1",
            _ => "libx264"
        };
        args.Add($"-c {codec}");

        // Encoder parameters
        args.Add($"-p crf=23");  // Quality setting
        args.Add($"-r {settings.FPS}");  // Frame rate

        // Audio capture
        if (settings.CaptureSystemAudio)
        {
            string monitorSource = PulseAudioHelper.GetDefaultMonitorSource();
            args.Add($"-a{monitorSource}");
            DebugHelper.WriteLine($"[WaylandPortalRecording] wf-recorder audio: system audio via {monitorSource}");
        }
        else if (settings.CaptureMicrophone)
        {
            string micDevice = !string.IsNullOrEmpty(settings.MicrophoneDeviceId)
                ? settings.MicrophoneDeviceId
                : "default";
            args.Add($"-a{micDevice}");
            DebugHelper.WriteLine($"[WaylandPortalRecording] wf-recorder audio: microphone via {micDevice}");
        }

        // Output file
        args.Add($"-f \"{outputPath}\"");

        string argsString = string.Join(" ", args);
        DebugHelper.WriteLine($"[WaylandPortalRecording] wf-recorder command: wf-recorder {argsString}");

        _ffmpegTask = Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    _stopwatch.Restart();
                    UpdateStatus(RecordingStatus.Recording);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "wf-recorder",
                    Arguments = argsString,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    HandleFatalError(new Exception("Failed to start wf-recorder process"), true);
                    return;
                }

                _gstreamerProcess = process;  // Reuse this field for the process reference
                try { _gstreamerPid = process.Id; } catch { }
                Task stdoutDrain = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
                string output = process.StandardError.ReadToEnd();
                process.WaitForExit();
                stdoutDrain.GetAwaiter().GetResult();

                if (process.ExitCode != 0 && !_stopRequested)
                {
                    HandleFatalError(new Exception($"wf-recorder failed.\nOutput: {output}"), true);
                }
            }
            catch (Exception ex)
            {
                HandleFatalError(ex, true);
            }
        });

        return Task.CompletedTask;
    }

    public async Task StopRecordingAsync()
    {
        FFmpegCLIManager? ffmpeg;
        Process? gstreamer;
        int? gstreamerPid;
        Task? ffmpegTask;
        bool usingFFmpegEncodingBridge;
        bool forcedTermination = false;

        lock (_lock)
        {
            // Allow stopping from Initializing too: hotkey can arrive before the background
            // thread advances status from Initializing to Recording.
            if (_status == RecordingStatus.Idle || _status == RecordingStatus.Finalizing)
            {
                return;
            }

            _stopRequested = true;
            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();

            ffmpeg = _ffmpeg;
            gstreamer = _gstreamerProcess;
            gstreamerPid = _gstreamerPid;
            ffmpegTask = _ffmpegTask;
            usingFFmpegEncodingBridge = _usingFFmpegEncodingBridge;
        }

        try
        {
            // Stop FFmpeg
            if (ffmpeg != null)
            {
                ffmpeg.StopRequested = true;
                ffmpeg.WriteInput("q");
            }

            // Stop GStreamer by sending EOS (End of Stream) via SIGINT
            // A completed bridge has disposed its child processes. Never signal a stale PID.
            if (gstreamerPid.HasValue && ffmpegTask is not { IsCompleted: true })
            {
                try
                {
                    DebugHelper.WriteLine($"[WaylandPortalRecording] Sending SIGINT to GStreamer (PID {gstreamerPid.Value}) for graceful shutdown...");
                    // Send SIGINT to GStreamer for graceful shutdown (triggers EOS with -e flag)
                    var killProcess = Process.Start("kill", $"-2 {gstreamerPid.Value}");
                    killProcess?.WaitForExit(1000);

                    // Wait for GStreamer to finish writing and exit
                    if (gstreamer != null && !gstreamer.WaitForExit(10000))
                    {
                        forcedTermination = true;
                        DebugHelper.WriteLine("[WaylandPortalRecording] GStreamer did not exit in time, force killing...");
                        try { Process.Start("kill", $"-9 {gstreamerPid.Value}")?.WaitForExit(1000); } catch { }
                    }
                    else if (gstreamer != null && ffmpegTask is not { IsCompleted: true })
                    {
                        DebugHelper.WriteLine($"[WaylandPortalRecording] GStreamer exited with code {gstreamer.ExitCode}");
                    }
                }
                catch (ObjectDisposedException) when (ffmpegTask is { IsCompleted: true })
                {
                    DebugHelper.WriteLine("[WaylandPortalRecording] Recording worker already finalized its processes.");
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"[WaylandPortalRecording] Error stopping GStreamer: {ex.Message}");
                    forcedTermination = true;
                    // If SIGINT fails, try force kill
                    try { Process.Start("kill", $"-9 {gstreamerPid.Value}")?.WaitForExit(1000); } catch { }
                }
            }
            else if (gstreamer != null && ffmpegTask is not { IsCompleted: true })
            {
                forcedTermination = true;
                // Fallback: no stored PID, try Kill() directly
                try { gstreamer.Kill(); } catch { }
            }

            if (ffmpegTask != null)
            {
                if (usingFFmpegEncodingBridge)
                {
                    // Do not return the file while the bridge is still encoding or writing its
                    // MP4 index. The bridge owns the encoder's shutdown timeout and cleanup.
                    await ffmpegTask.ConfigureAwait(false);
                }
                else
                {
                    try { await ffmpegTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
                    catch (TimeoutException) { ffmpeg?.Close(); throw; }
                }
            }
            if (forcedTermination)
                throw new TimeoutException("Recording backend did not finalize normally and was force-stopped. Partial output has been kept for recovery.");
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, false);
            throw;
        }
        finally
        {
            CleanupPortalSession();

            lock (_lock)
            {
                _ffmpeg = null;
                _ffmpegTask = null;
                _usingFFmpegEncodingBridge = false;
                _gstreamerProcess = null;
                _gstreamerPid = null;
                _currentOptions = null;
                _stopRequested = false;
                UpdateStatus(RecordingStatus.Idle);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            _durationTimer?.Dispose();
            _durationTimer = null;
        }

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

        CleanupPortalSession();
    }

    private static void EnsureWayland()
    {
        if (!LinuxScreenCaptureService.IsWayland)
        {
            throw new PlatformNotSupportedException("Wayland ScreenCast portal requires a Wayland session.");
        }
    }

    private async Task InitializePortalSession(RecordingOptions options)
    {
        _connection = new Connection(Address.Session);
        var connectionInfo = await _connection.ConnectAsync().ConfigureAwait(false);
        global::XerahS.Platform.Linux.Capture.PortalRequestExtensions.CacheLocalConnectionName(_connection, connectionInfo);
        _portal = _connection.CreateProxy<IScreenCastPortal>(PortalBusName, PortalObjectPath);

        var createOptions = new Dictionary<string, object>
        {
            ["session_handle_token"] = $"xerahs_sc_{Guid.NewGuid():N}"
        };

        var (createResponse, createResults) = await _connection
            .SendPortalRequestAsync(
                PortalBusName,
                createOptions,
                () => _portal.CreateSessionAsync(createOptions))
            .ConfigureAwait(false);
        if (createResponse != 0 ||
            !createResults.TryGetResult("session_handle", out string? sessionHandlePath) ||
            string.IsNullOrWhiteSpace(sessionHandlePath))
        {
            throw new PlatformNotSupportedException($"ScreenCast CreateSession failed ({createResponse}).");
        }

        _sessionHandle = new ObjectPath(sessionHandlePath);
        _sessionProxy = _connection.CreateProxy<IPortalSession>(PortalBusName, _sessionHandle.Value);

        var selectOptions = new Dictionary<string, object>
        {
            ["types"] = GetSourceTypes(options.Mode),
            ["multiple"] = false,
            ["cursor_mode"] = (uint)((options.Settings?.ShowCursor ?? true) ? 1 : 0),
            // persist_mode: 2 = persist the permission until explicitly revoked
            // This reduces portal dialogs for subsequent recordings
            ["persist_mode"] = (uint)2
        };

        var (selectResponse, _) = await _connection
            .SendPortalRequestAsync(
                PortalBusName,
                selectOptions,
                () => _portal.SelectSourcesAsync(_sessionHandle.Value, selectOptions))
            .ConfigureAwait(false);
        if (selectResponse != 0)
        {
            throw new PlatformNotSupportedException($"ScreenCast SelectSources failed ({selectResponse}).");
        }

        var startOptions = new Dictionary<string, object>();
        var (startResponse, startResults) = await _connection
            .SendPortalRequestAsync(
                PortalBusName,
                startOptions,
                () => _portal.StartAsync(_sessionHandle.Value, string.Empty, startOptions))
            .ConfigureAwait(false);
        if (startResponse != 0)
        {
            throw new PlatformNotSupportedException($"ScreenCast Start failed ({startResponse}).");
        }

        if (!TryGetPipeWireNodeId(startResults, out _pipewireNodeId))
        {
            throw new PlatformNotSupportedException("ScreenCast response did not include PipeWire stream node.");
        }

        try
        {
            _pipewireRemote?.Dispose();
            _pipewireRemote = await _portal.OpenPipeWireRemoteAsync(_sessionHandle.Value, new Dictionary<string, object>()).ConfigureAwait(false);
            DebugHelper.WriteLine($"[WaylandPortalRecording] Opened portal PipeWire remote (fd {RemoteFd})");
        }
        catch (Exception ex) when (ex is DBusException or InvalidOperationException or NotSupportedException)
        {
            _pipewireRemote = null;
            DebugHelper.WriteLine($"[WaylandPortalRecording] OpenPipeWireRemote failed; falling back to the default PipeWire connection: {ex.Message}");
        }

        TryGetPipeWireSourceSize(startResults, out _pipewireSourceWidth, out _pipewireSourceHeight);
        if (_pipewireSourceWidth > 0)
            DebugHelper.WriteLine($"[WaylandPortalRecording] PipeWire source size: {_pipewireSourceWidth}x{_pipewireSourceHeight}");
        else
            DebugHelper.WriteLine("[WaylandPortalRecording] PipeWire source size: not reported by portal");
    }

    private void CleanupPortalSession()
    {
        try
        {
            _sessionProxy?.CloseAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort cleanup
        }
        finally
        {
            _sessionProxy = null;
            _sessionHandle = null;
            _portal = null;
            _pipewireRemote?.Dispose();
            _pipewireRemote = null;
            _connection?.Dispose();
            _connection = null;
        }
    }

    private static uint GetSourceTypes(CaptureMode mode)
    {
        return mode == CaptureMode.Window ? 2u : 1u;
    }

    private static bool TryGetPipeWireNodeId(IDictionary<string, object> results, out uint nodeId)
    {
        nodeId = 0;
        if (!results.TryGetValue("streams", out var streamsRaw) || streamsRaw == null)
        {
            return false;
        }

        var streams = UnwrapVariant(streamsRaw) as Array;
        if (streams == null || streams.Length == 0)
        {
            return false;
        }

        foreach (var entry in streams)
        {
            if (entry == null) continue;
            var unwrapped = UnwrapVariant(entry);

            if (unwrapped is ValueTuple<uint, IDictionary<string, object>> tuple)
            {
                nodeId = tuple.Item1;
                return true;
            }

            if (unwrapped is object[] parts && parts.Length > 0)
            {
                var idCandidate = UnwrapVariant(parts[0]);
                if (idCandidate is uint id)
                {
                    nodeId = id;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the source stream size (width × height in screen pixels) from the portal Start response.
    /// The XDG ScreenCast portal includes a "size" property per stream: a{sv} with key "size" → (int32, int32).
    /// </summary>
    private static bool TryGetPipeWireSourceSize(IDictionary<string, object> results, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!results.TryGetValue("streams", out var streamsRaw) || streamsRaw == null)
            return false;

        var streams = UnwrapVariant(streamsRaw) as Array;
        if (streams == null || streams.Length == 0)
            return false;

        foreach (var entry in streams)
        {
            if (entry == null) continue;
            var unwrapped = UnwrapVariant(entry);

            IDictionary<string, object>? props = null;
            if (unwrapped is ValueTuple<uint, IDictionary<string, object>> tuple)
                props = tuple.Item2;
            else if (unwrapped is object[] parts && parts.Length > 1)
                props = UnwrapVariant(parts[1]) as IDictionary<string, object>;

            if (props == null || !props.TryGetValue("size", out var sizeRaw))
                continue;

            var size = UnwrapVariant(sizeRaw);
            if (size is ValueTuple<int, int> sizeTuple)
            {
                width = sizeTuple.Item1;
                height = sizeTuple.Item2;
                return width > 0 && height > 0;
            }
            if (size is object[] sizeArr && sizeArr.Length >= 2)
            {
                width = Convert.ToInt32(UnwrapVariant(sizeArr[0]));
                height = Convert.ToInt32(UnwrapVariant(sizeArr[1]));
                return width > 0 && height > 0;
            }
        }

        return false;
    }

    private static object UnwrapVariant(object value)
    {
        var current = value;
        while (current != null)
        {
            var type = current.GetType();
            var typeName = type.FullName;
            if (typeName != "Tmds.DBus.Protocol.Variant" &&
                typeName != "Tmds.DBus.Protocol.VariantValue" &&
                typeName != "Tmds.DBus.Variant")
            {
                break;
            }

            var valueProp = type.GetProperty("Value");
            var unwrapped = valueProp?.GetValue(current);
            if (unwrapped == null)
            {
                break;
            }

            current = unwrapped;
        }

        return current ?? value;
    }

    private static string ResolveConfiguredFFmpegPath(RecordingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FFmpegOverridePath))
        {
            string configuredPath = options.FFmpegOverridePath.Trim().Trim('"', '\'');
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        string detectedPath = PathsManager.GetFFmpegPath();
        if (!string.IsNullOrWhiteSpace(detectedPath))
        {
            return detectedPath;
        }

        return "ffmpeg";
    }

    private static bool HasFFmpegPipewireSupport(string ffmpegPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-devices",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            // ffmpeg prints the device list on stdout; drain both streams so neither pipe fills up.
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return (output + stderr.GetAwaiter().GetResult()).Split('\n').Any(line =>
            {
                string[] parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length >= 2 && parts[0].Contains('D') && parts[1].Equals("pipewire", StringComparison.OrdinalIgnoreCase);
            });
        }
        catch
        {
            return false;
        }
    }

    private static bool HasGStreamerPipewireSupport()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gst-inspect-1.0",
                Arguments = "pipewiresrc",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    internal static bool ShouldPreferCpuGStreamerPath(
        string? sessionType = null,
        string? currentDesktop = null,
        string? sessionDesktop = null,
        string? desktopSession = null)
    {
        sessionType ??= Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (!string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string desktopHints =
            $"{currentDesktop ?? Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")} " +
            $"{sessionDesktop ?? Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP")} " +
            $"{desktopSession ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION")}";

        return desktopHints.Contains("GNOME", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasWfRecorder()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "wf-recorder",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanUseWfRecorder(RecordingOptions options)
    {
        if (!HasWfRecorder())
        {
            return false;
        }

        // Keep window-mode behavior stable: current wf-recorder path doesn't implement window selection.
        if (options.Mode == CaptureMode.Window)
        {
            DebugHelper.WriteLine("[WaylandPortalRecording] Window mode requested; using portal path instead of wf-recorder.");
            return false;
        }

        if (!IsWlrootsCompositor())
        {
            DebugHelper.WriteLine("[WaylandPortalRecording] Non-wlroots compositor detected; preferring portal path over wf-recorder.");
            return false;
        }

        return true;
    }

    private static bool IsWlrootsCompositor()
    {
        // Strong signals for wlroots-based compositors.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE")) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SWAYSOCK")))
        {
            return true;
        }

        string currentDesktop = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty).ToUpperInvariant();
        string desktopSession = (Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? string.Empty).ToUpperInvariant();

        // Conservative allowlist for known wlroots families.
        return currentDesktop.Contains("HYPRLAND") ||
               currentDesktop.Contains("SWAY") ||
               currentDesktop.Contains("WLROOTS") ||
               desktopSession.Contains("HYPRLAND") ||
               desktopSession.Contains("SWAY");
    }

    private static (string executable, string arguments, bool useGStreamer) BuildRecordingCommand(
        RecordingOptions options,
        uint pipeWireNodeId,
        int sourceWidth = 0,
        int sourceHeight = 0,
        bool preferCpuGStreamer = false,
        int remoteFd = -1)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        string outputPath = options.OutputPath ?? GetDefaultOutputPath();
        string ffmpegPath = ResolveConfiguredFFmpegPath(options);

        // Check if FFmpeg has pipewire support
        if (HasFFmpegPipewireSupport(ffmpegPath))
        {
            return (ffmpegPath, BuildFFmpegArguments(options, pipeWireNodeId, outputPath), false);
        }

        // Fall back to GStreamer if available
        if (HasGStreamerPipewireSupport())
        {
            DebugHelper.WriteLine("[WaylandPortalRecording] FFmpeg lacks pipewire support, using GStreamer");
            // -e flag: send EOS on SIGINT for proper file finalization
            var (pipeline, actualOutputPath) = BuildGStreamerPipeline(
                options,
                pipeWireNodeId,
                outputPath,
                sourceWidth,
                sourceHeight,
                useGl: !preferCpuGStreamer,
                remoteFd: remoteFd);

            // Update options with the actual output path (may differ if muxer changed, e.g. .mp4 -> .mkv)
            if (!string.Equals(outputPath, actualOutputPath, StringComparison.Ordinal))
            {
                options.OutputPath = actualOutputPath;
                DebugHelper.WriteLine($"[WaylandPortalRecording] Output path changed to: {actualOutputPath}");
            }

            return ("gst-launch-1.0", "-e " + pipeline, true);
        }

        // Last resort: try FFmpeg anyway (will likely fail)
        DebugHelper.WriteLine("[WaylandPortalRecording] WARNING: Neither FFmpeg pipewire nor GStreamer available");
        return (ffmpegPath, BuildFFmpegArguments(options, pipeWireNodeId, outputPath), false);
    }

    private static string BuildFFmpegArguments(RecordingOptions options, uint pipeWireNodeId, string outputPath)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        bool hasAudio = settings.CaptureSystemAudio || settings.CaptureMicrophone;

        // Video input (input 0)
        var args = new List<string>
        {
            "-f pipewire",
            "-framerate " + settings.FPS.ToString(CultureInfo.InvariantCulture),
            $"-i {pipeWireNodeId}"
        };

        // Audio input (input 1) — must come right after video input, before codec/output args
        if (hasAudio)
        {
            args.Add("-f pulse");

            if (settings.CaptureSystemAudio)
            {
                string monitorSource = PulseAudioHelper.GetDefaultMonitorSource();
                args.Add($"-i {monitorSource}");
            }
            else
            {
                args.Add(!string.IsNullOrEmpty(settings.MicrophoneDeviceId)
                    ? $"-i {settings.MicrophoneDeviceId}"
                    : "-i default");
            }
        }

        // Stream mapping — required when there are multiple inputs
        if (hasAudio)
        {
            args.Add("-map 0:v");
            args.Add("-map 1:a");
        }

        if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
        {
            args.Add($"-vf \"crop={options.Region.Width}:{options.Region.Height}:{options.Region.X}:{options.Region.Y}\"");
        }

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

        if (hasAudio)
        {
            args.Add("-c:a aac");
            args.Add("-b:a 192k");
        }

        args.Add("-pix_fmt yuv420p");
        args.Add("-y");
        args.Add($"\"{outputPath}\"");

        return string.Join(" ", args);
    }

    /// <summary>
    /// The pipewiresrc element for a portal stream. With the portal's PipeWire remote fd the
    /// stream negotiates like any portal client (OBS, browsers); without it, pipewiresrc reaches
    /// the node over the default daemon connection, where Hyprland window streams fail with
    /// "no more input formats".
    /// The portal returns a node ID, so it goes in <c>path</c> (deprecated, but the property that
    /// takes a node ID). <c>target-object</c> matches an object name or serial instead; a node ID
    /// there matches nothing, and pipewiresrc then autoconnects to the default video source,
    /// which records the webcam instead of the screen.
    /// </summary>
    internal static string BuildPipeWireSource(uint nodeId, int remoteFd)
    {
        return remoteFd < 0
            ? $"pipewiresrc path={nodeId} do-timestamp=true"
            : $"pipewiresrc fd={remoteFd} path={nodeId} do-timestamp=true";
    }

    private int RemoteFd => _pipewireRemote is { IsInvalid: false, IsClosed: false } handle
        ? (int)handle.DangerousGetHandle()
        : -1;

    // Cached GStreamer element availability
    private static readonly ConcurrentDictionary<string, bool> _gstElementCache = new();

    private static bool HasGStreamerElement(string elementName)
    {
        return _gstElementCache.GetOrAdd(elementName, name =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "gst-inspect-1.0",
                    Arguments = name,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                process.WaitForExit(3000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    private static (string pipeline, string actualOutputPath) BuildGStreamerPipeline(RecordingOptions options, uint pipeWireNodeId, string outputPath, int sourceWidth = 0, int sourceHeight = 0, bool useGl = true, int remoteFd = -1)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        bool hasAudio = settings.CaptureSystemAudio || settings.CaptureMicrophone;

        // Build pipeline with queue for buffering large frames.
        var pipeline = new List<string>();
        pipeline.Add(BuildPipeWireSource(pipeWireNodeId, remoteFd));

        if (useGl && HasGStreamerElement("gldownload") && HasGStreamerElement("glupload"))
        {
            // GPU path: let glupload negotiate directly with pipewiresrc.
            // glupload is designed to handle DMA-BUF from PipeWire natively.
            // DO NOT insert a video/x-raw caps filter here: pipewiresrc on Wayland often
            // only offers DMA-BUF (video/x-raw(memory:DMABuf)), and the plain video/x-raw
            // filter rejects that, causing "streaming stopped, reason not-negotiated (-4)".
            // NOTE: on some systems glupload itself fails with "unhandled format" — if that
            // happens the caller retries with useGl=false (the CPU path below).
            pipeline.AddRange(new[] { "!", "glupload", "!", "glcolorconvert", "!", "gldownload" });
        }
        // CPU path: no GL, no caps filter.
        // videoconvert below handles all pipewiresrc output formats (raw memory and DMA-BUF).

        // queue to decouple and handle buffering
        pipeline.AddRange(new[] { "!", "queue max-size-buffers=3 leaky=downstream" });
        // videoconvert handles any format conversion needed
        pipeline.AddRange(new[] { "!", "videoconvert" });

        // Add crop filter for region capture.
        if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
        {
            // GStreamer videocrop removes pixels from each edge (left, top, right, bottom).
            // We MUST specify all four edges; omitting right/bottom leaves them at 0, meaning the
            // full screen width/height minus only the left/top crop is passed to videoscale, which
            // then squashes a wider-than-intended frame into the target dimensions.
            int cropLeft = Math.Max(0, options.Region.X);
            int cropTop = Math.Max(0, options.Region.Y);

            string cropElement;
            if (sourceWidth > 0 && sourceHeight > 0)
            {
                // Source size known from portal stream properties — compute exact right/bottom crops.
                int cropRight = Math.Max(0, sourceWidth - cropLeft - options.Region.Width);
                int cropBottom = Math.Max(0, sourceHeight - cropTop - options.Region.Height);
                cropElement = $"videocrop left={cropLeft} top={cropTop} right={cropRight} bottom={cropBottom}";
                DebugHelper.WriteLine($"[WaylandPortalRecording] videocrop: left={cropLeft} top={cropTop} right={cropRight} bottom={cropBottom} (source={sourceWidth}x{sourceHeight})");
            }
            else
            {
                // Source size unknown; crop left/top only. videoscale will still scale to the target
                // dimensions, but may squash if the source is wider than the region.
                cropElement = $"videocrop left={cropLeft} top={cropTop}";
                DebugHelper.WriteLine($"[WaylandPortalRecording] videocrop: left={cropLeft} top={cropTop} (source size unknown, right/bottom not cropped)");
            }

            pipeline.Add("!");
            pipeline.Add(cropElement);
            pipeline.Add("!");
            pipeline.Add("videoconvert");
            pipeline.Add("!");
            pipeline.Add("videoscale");
            pipeline.Add("!");
            pipeline.Add($"video/x-raw,width={options.Region.Width},height={options.Region.Height}");
            // Final videoconvert to ensure encoder-compatible format
            pipeline.Add("!");
            pipeline.Add("videoconvert");
        }

        // Get encoder and muxer based on requested codec and available elements
        var (encoderElement, muxerElement, fileExtension) = GetEncoderAndMuxer(settings.Codec, settings.BitrateKbps);

        pipeline.Add("!");
        pipeline.Add(encoderElement);

        // Adjust output path extension if needed
        string finalOutputPath = outputPath;
        if (!string.IsNullOrEmpty(fileExtension))
        {
            string dir = Path.GetDirectoryName(outputPath) ?? "";
            string baseName = Path.GetFileNameWithoutExtension(outputPath);
            finalOutputPath = Path.Combine(dir, baseName + fileExtension);
        }

        if (hasAudio)
        {
            // Audio branch: pulsesrc -> queue -> audioconvert -> encoder -> mux.
            string audioDevice;
            if (settings.CaptureSystemAudio)
            {
                audioDevice = PulseAudioHelper.GetDefaultMonitorSource();
            }
            else
            {
                audioDevice = !string.IsNullOrEmpty(settings.MicrophoneDeviceId)
                    ? settings.MicrophoneDeviceId
                    : "default";
            }

            var (audioEncoder, actualMuxer, actualExtension) = GetCompatibleAudioEncoder(muxerElement, fileExtension);

            // Update output path if the muxer/extension changed (e.g. mp4 -> mkv for Opus)
            if (actualExtension != fileExtension)
            {
                string dir = Path.GetDirectoryName(finalOutputPath) ?? "";
                string baseName = Path.GetFileNameWithoutExtension(finalOutputPath);
                finalOutputPath = Path.Combine(dir, baseName + actualExtension);
            }

            // Use a named muxer so both video and audio branches can connect
            pipeline.Add("!");
            pipeline.Add($"{ConfigureGStreamerMuxer(actualMuxer)} name=mux");
            pipeline.Add("!");
            pipeline.Add($"filesink location=\"{finalOutputPath}\"");

            pipeline.Add($"pulsesrc device=\"{audioDevice}\"");
            pipeline.Add("!");
            pipeline.Add("queue");
            pipeline.Add("!");
            pipeline.Add("audioconvert");
            pipeline.Add("!");
            pipeline.Add(audioEncoder);
            pipeline.Add("!");
            pipeline.Add("mux.");

            DebugHelper.WriteLine($"[WaylandPortalRecording] GStreamer audio: device={audioDevice}, encoder={audioEncoder}, muxer={actualMuxer}");
        }
        else
        {
            // Simple pipeline without named muxer
            pipeline.Add("!");
            pipeline.Add(ConfigureGStreamerMuxer(muxerElement));
            pipeline.Add("!");
            pipeline.Add($"filesink location=\"{finalOutputPath}\"");
        }

        DebugHelper.WriteLine($"[WaylandPortalRecording] GStreamer encoder: {encoderElement.Split(' ')[0]}, muxer: {muxerElement}, output: {finalOutputPath}");

        return (string.Join(" ", pipeline), finalOutputPath);
    }

    // Periodic fragments keep the MP4 initialization metadata on disk before EOS.
    // This limits damage from a compositor failure or a forced process termination.
    internal static string ConfigureGStreamerMuxer(string muxer) =>
        muxer == "mp4mux" ? "mp4mux fragment-duration=1000" : muxer;

    /// <summary>
    /// Returns a compatible (audioEncoder, muxer, extension) tuple.
    /// MP4 requires AAC; if only Opus is available, falls back to Matroska container.
    /// </summary>
    private static (string audioEncoder, string muxer, string extension) GetCompatibleAudioEncoder(string requestedMuxer, string requestedExtension)
    {
        bool isMp4 = requestedMuxer.Contains("mp4", StringComparison.OrdinalIgnoreCase);

        // AAC encoders work in both MP4 and MKV
        if (HasGStreamerElement("avenc_aac"))
            return ("avenc_aac", requestedMuxer, requestedExtension);
        if (HasGStreamerElement("voaacenc"))
            return ("voaacenc", requestedMuxer, requestedExtension);
        if (HasGStreamerElement("fdkaacenc"))
            return ("fdkaacenc", requestedMuxer, requestedExtension);

        // Opus is available but incompatible with MP4 — switch to MKV
        if (HasGStreamerElement("opusenc"))
        {
            if (isMp4)
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] No AAC encoder available; switching from mp4mux to matroskamux for Opus compatibility");
                return ("opusenc", "matroskamux", ".mkv");
            }

            return ("opusenc", requestedMuxer, requestedExtension);
        }

        // Vorbis works in WebM/MKV/OGG
        if (HasGStreamerElement("vorbisenc"))
        {
            if (isMp4)
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] No AAC/Opus encoder available; switching from mp4mux to matroskamux for Vorbis");
                return ("vorbisenc", "matroskamux", ".mkv");
            }

            return ("vorbisenc", requestedMuxer, requestedExtension);
        }

        // Last resort: raw audio in MKV (will work but produce large files)
        DebugHelper.WriteLine("[WaylandPortalRecording] No audio encoder found, using raw audio in MKV");
        return ("identity", "matroskamux", ".mkv");
    }

    private static (string encoder, string muxer, string extension) GetEncoderAndMuxer(VideoCodec codec, int bitrateKbps)
    {
        // Try to find an available encoder for the requested codec
        // Preference: Hardware (NVIDIA) > Software > Fallback to VP9

        switch (codec)
        {
            case VideoCodec.H264:
                // Try NVIDIA hardware encoder first
                if (HasGStreamerElement("nvh264enc"))
                {
                    DebugHelper.WriteLine("[WaylandPortalRecording] Using NVIDIA H.264 hardware encoder");
                    return ($"nvh264enc bitrate={bitrateKbps} preset=low-latency ! h264parse ! video/x-h264,profile=main", "mp4mux", ".mp4");
                }
                // Try software x264enc
                if (HasGStreamerElement("x264enc"))
                {
                    return ($"x264enc tune=zerolatency bitrate={bitrateKbps} speed-preset=ultrafast ! video/x-h264,profile=main", "mp4mux", ".mp4");
                }
                // Fallback to VP9
                DebugHelper.WriteLine("[WaylandPortalRecording] H.264 encoder not available, falling back to VP9");
                return GetVP9Encoder(bitrateKbps);

            case VideoCodec.HEVC:
                // Try NVIDIA hardware encoder first
                if (HasGStreamerElement("nvh265enc"))
                {
                    DebugHelper.WriteLine("[WaylandPortalRecording] Using NVIDIA H.265 hardware encoder");
                    return ($"nvh265enc bitrate={bitrateKbps} preset=low-latency ! h265parse", "mp4mux", ".mp4");
                }
                // Try software x265enc
                if (HasGStreamerElement("x265enc"))
                {
                    return ($"x265enc tune=zerolatency bitrate={bitrateKbps} speed-preset=ultrafast", "mp4mux", ".mp4");
                }
                // Fallback to VP9
                DebugHelper.WriteLine("[WaylandPortalRecording] H.265 encoder not available, falling back to VP9");
                return GetVP9Encoder(bitrateKbps);

            case VideoCodec.VP9:
                return GetVP9Encoder(bitrateKbps);

            case VideoCodec.AV1:
                if (HasGStreamerElement("av1enc"))
                {
                    return ($"av1enc target-bitrate={bitrateKbps * 1000}", "webmmux", ".webm");
                }
                // Fallback to VP9
                DebugHelper.WriteLine("[WaylandPortalRecording] AV1 encoder not available, falling back to VP9");
                return GetVP9Encoder(bitrateKbps);

            default:
                // Default: try H.264 path
                return GetEncoderAndMuxer(VideoCodec.H264, bitrateKbps);
        }
    }

    private static (string encoder, string muxer, string extension) GetVP9Encoder(int bitrateKbps)
    {
        if (HasGStreamerElement("vp9enc"))
        {
            // Keep VP9 options conservative for compatibility across plugin versions.
            // Some distributions reject deadline=realtime and fail pipeline creation.
            string encoderArgs = $"vp9enc target-bitrate={bitrateKbps * 1000} cpu-used=8";
            DebugHelper.WriteLine("[WaylandPortalRecording] VP9 compatibility profile selected (deadline omitted)");
            DebugHelper.WriteLine($"[WaylandPortalRecording] VP9 encoder args: {encoderArgs}");
            return (encoderArgs, "webmmux", ".webm");
        }

        // Last resort: Theora (almost always available but lower quality)
        if (HasGStreamerElement("theoraenc"))
        {
            DebugHelper.WriteLine("[WaylandPortalRecording] Using Theora encoder as last resort");
            return ($"theoraenc bitrate={bitrateKbps}", "oggmux", ".ogv");
        }

        throw new InvalidOperationException("No suitable video encoder found. Please install gst-plugins-good (for VP9) or gst-plugins-ugly (for H.264).");
    }

    private static string GetDefaultOutputPath()
    {
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

            // Start/stop duration timer based on recording status
            if (newStatus == RecordingStatus.Recording)
            {
                // Fire duration updates every 100ms for smooth timer display
                _durationTimer = new Timer(_ =>
                {
                    lock (_lock)
                    {
                        if (_status == RecordingStatus.Recording)
                        {
                            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecordingStatus.Recording, _stopwatch.Elapsed));
                        }
                    }
                }, null, 100, 100);
            }
            else
            {
                _durationTimer?.Dispose();
                _durationTimer = null;
            }

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
            try
            {
                _ffmpeg?.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }

            _ffmpeg = null;

            // Clean up the portal session immediately so the DBus Connection is not
            // left open until GC. An open Connection whose finalizer fires a close
            // message on a dead portal session produces an unobserved task exception
            // (org.freedesktop.DBus.Error.ServiceUnknown).
            CleanupPortalSession();
        }
    }
}

[DBusInterface("org.freedesktop.portal.ScreenCast")]
public interface IScreenCastPortal : IDBusObject
{
    Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
    Task<ObjectPath> SelectSourcesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
    Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);
    Task<SafeFileHandle> OpenPipeWireRemoteAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
}
