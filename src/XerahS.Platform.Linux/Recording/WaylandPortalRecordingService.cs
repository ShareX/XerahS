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
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Media;
using XerahS.Platform.Linux.Capture;
using XerahS.Platform.Linux.Services;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Platform.Linux.Recording;

/// <summary>
/// Wayland screen recording via XDG ScreenCast portal + FFmpeg pipewire input.
/// Falls back to FFmpegRecordingService if portal negotiation fails.
/// </summary>
public sealed class WaylandPortalRecordingService : IRecordingService
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    private FFmpegCLIManager? _ffmpeg;
    private Process? _gstreamerProcess;
    private Task? _ffmpegTask;
    private RecordingOptions? _currentOptions;
    private RecordingStatus _status = RecordingStatus.Idle;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private bool _disposed;
    private bool _stopRequested;
    private Timer? _durationTimer;

    private Connection? _connection;
    private IScreenCastPortal? _portal;
    private IPortalSession? _sessionProxy;
    private ObjectPath? _sessionHandle;
    private uint _pipewireNodeId;

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

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

            // Prefer wf-recorder if available - it handles portal integration internally
            if (HasWfRecorder())
            {
                DebugHelper.WriteLine("[WaylandPortalRecording] Using wf-recorder (handles portal internally)");
                return StartWithWfRecorder(options);
            }

            // Fall back to portal + GStreamer/FFmpeg approach
            InitializePortalSession(options).GetAwaiter().GetResult();

            var (executable, args, useGStreamer) = BuildRecordingCommand(options, _pipewireNodeId);
            DebugHelper.WriteLine($"[WaylandPortalRecording] Using {(useGStreamer ? "GStreamer" : "FFmpeg")}");
            DebugHelper.WriteLine($"[WaylandPortalRecording] Command: {executable} {args}");

            if (useGStreamer)
            {
                // Use GStreamer directly via Process
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
                            FileName = executable,
                            Arguments = args,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var process = Process.Start(startInfo);
                        if (process == null)
                        {
                            HandleFatalError(new Exception("Failed to start GStreamer process"), true);
                            return;
                        }

                        _gstreamerProcess = process;
                        string output = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        if (process.ExitCode != 0 && !_stopRequested)
                        {
                            HandleFatalError(new Exception($"GStreamer process failed.\nOutput: {output}"), true);
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleFatalError(ex, true);
                    }
                });
            }
            else
            {
                // Use FFmpeg via FFmpegCLIManager
                string ffmpegPath = PathsManager.GetFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
                {
                    throw new FileNotFoundException("FFmpeg not found for Wayland portal recording.", ffmpegPath);
                }

                _ffmpeg = new FFmpegCLIManager(ffmpegPath)
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

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    HandleFatalError(new Exception("Failed to start wf-recorder process"), true);
                    return;
                }

                _gstreamerProcess = process;  // Reuse this field for the process reference
                string output = process.StandardError.ReadToEnd();
                process.WaitForExit();

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
        Task? ffmpegTask;

        lock (_lock)
        {
            if (_status != RecordingStatus.Recording)
            {
                return;
            }

            _stopRequested = true;
            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();

            ffmpeg = _ffmpeg;
            gstreamer = _gstreamerProcess;
            ffmpegTask = _ffmpegTask;
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
            if (gstreamer != null && !gstreamer.HasExited)
            {
                try
                {
                    DebugHelper.WriteLine("[WaylandPortalRecording] Sending SIGINT to GStreamer for graceful shutdown...");
                    // Send SIGINT to GStreamer for graceful shutdown (triggers EOS with -e flag)
                    var killProcess = Process.Start("kill", $"-2 {gstreamer.Id}");
                    killProcess?.WaitForExit(1000);

                    // Wait for GStreamer to finish writing and exit
                    if (!gstreamer.WaitForExit(10000))
                    {
                        DebugHelper.WriteLine("[WaylandPortalRecording] GStreamer did not exit in time, force killing...");
                        try { gstreamer.Kill(); } catch { }
                    }
                    else
                    {
                        DebugHelper.WriteLine($"[WaylandPortalRecording] GStreamer exited with code {gstreamer.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"[WaylandPortalRecording] Error stopping GStreamer: {ex.Message}");
                    // If SIGINT fails, try force kill
                    try { gstreamer.Kill(); } catch { }
                }
            }

            if (ffmpegTask != null)
            {
                await Task.WhenAny(ffmpegTask, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, false);
        }
        finally
        {
            CleanupPortalSession();

            lock (_lock)
            {
                _ffmpeg = null;
                _gstreamerProcess = null;
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
        await _connection.ConnectAsync().ConfigureAwait(false);
        _portal = _connection.CreateProxy<IScreenCastPortal>(PortalBusName, PortalObjectPath);

        var createOptions = new Dictionary<string, object>
        {
            ["session_handle_token"] = $"xerahs_sc_{Guid.NewGuid():N}"
        };

        var createPath = await _portal.CreateSessionAsync(createOptions).ConfigureAwait(false);
        var createRequest = _connection.CreateProxy<IPortalRequest>(PortalBusName, createPath);
        var (createResponse, createResults) = await createRequest.WaitForResponseAsync().ConfigureAwait(false);
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

        var selectPath = await _portal.SelectSourcesAsync(_sessionHandle.Value, selectOptions).ConfigureAwait(false);
        var selectRequest = _connection.CreateProxy<IPortalRequest>(PortalBusName, selectPath);
        var (selectResponse, _) = await selectRequest.WaitForResponseAsync().ConfigureAwait(false);
        if (selectResponse != 0)
        {
            throw new PlatformNotSupportedException($"ScreenCast SelectSources failed ({selectResponse}).");
        }

        var startPath = await _portal.StartAsync(_sessionHandle.Value, string.Empty, new Dictionary<string, object>()).ConfigureAwait(false);
        var startRequest = _connection.CreateProxy<IPortalRequest>(PortalBusName, startPath);
        var (startResponse, startResults) = await startRequest.WaitForResponseAsync().ConfigureAwait(false);
        if (startResponse != 0)
        {
            throw new PlatformNotSupportedException($"ScreenCast Start failed ({startResponse}).");
        }

        if (!TryGetPipeWireNodeId(startResults, out _pipewireNodeId))
        {
            throw new PlatformNotSupportedException("ScreenCast response did not include PipeWire stream node.");
        }
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

    private static bool HasFFmpegPipewireSupport()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-devices",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            string output = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            return output.Contains("pipewire", StringComparison.OrdinalIgnoreCase);
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

    private static (string executable, string arguments, bool useGStreamer) BuildRecordingCommand(RecordingOptions options, uint pipeWireNodeId)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        string outputPath = options.OutputPath ?? GetDefaultOutputPath();

        // Check if FFmpeg has pipewire support
        if (HasFFmpegPipewireSupport())
        {
            return ("ffmpeg", BuildFFmpegArguments(options, pipeWireNodeId, outputPath), false);
        }

        // Fall back to GStreamer if available
        if (HasGStreamerPipewireSupport())
        {
            DebugHelper.WriteLine("[WaylandPortalRecording] FFmpeg lacks pipewire support, using GStreamer");
            // -e flag: send EOS on SIGINT for proper file finalization
            return ("gst-launch-1.0", "-e " + BuildGStreamerPipeline(options, pipeWireNodeId, outputPath), true);
        }

        // Last resort: try FFmpeg anyway (will likely fail)
        DebugHelper.WriteLine("[WaylandPortalRecording] WARNING: Neither FFmpeg pipewire nor GStreamer available");
        return ("ffmpeg", BuildFFmpegArguments(options, pipeWireNodeId, outputPath), false);
    }

    private static string BuildFFmpegArguments(RecordingOptions options, uint pipeWireNodeId, string outputPath)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();
        var args = new List<string>
        {
            "-f pipewire",
            "-framerate " + settings.FPS.ToString(CultureInfo.InvariantCulture),
            $"-i {pipeWireNodeId}"
        };

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

        if (settings.CaptureSystemAudio || settings.CaptureMicrophone)
        {
            if (settings.CaptureSystemAudio)
            {
                args.Add("-f pulse");
                args.Add("-i default");
                args.Add("-c:a aac");
                args.Add("-b:a 192k");
            }
            else if (settings.CaptureMicrophone)
            {
                args.Add("-f pulse");
                args.Add(!string.IsNullOrEmpty(settings.MicrophoneDeviceId)
                    ? $"-i {settings.MicrophoneDeviceId}"
                    : "-i default");
                args.Add("-c:a aac");
                args.Add("-b:a 192k");
            }
        }

        args.Add("-pix_fmt yuv420p");
        args.Add("-y");
        args.Add($"\"{outputPath}\"");

        return string.Join(" ", args);
    }

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

    private static string BuildGStreamerPipeline(RecordingOptions options, uint pipeWireNodeId, string outputPath)
    {
        var settings = options.Settings ?? new ScreenRecordingSettings();

        // Build pipeline with queue for buffering large frames
        var pipeline = new List<string>
        {
            // pipewiresrc - let it negotiate format naturally
            $"pipewiresrc path={pipeWireNodeId} do-timestamp=true",
            // queue to decouple and handle buffering
            "!", "queue max-size-buffers=3 leaky=downstream",
            // videoconvert handles any format conversion needed
            "!", "videoconvert"
        };

        // Add crop filter for region capture using FFmpeg-style crop filter
        if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
        {
            // Use videocrop with correct parameters (pixels to remove from each edge)
            // For a region at (X,Y) with size WxH on a larger source, we crop:
            // - left: X pixels, top: Y pixels
            // - right and bottom are set to 0, then we scale to exact size
            pipeline.Add("!");
            pipeline.Add($"videocrop left={options.Region.X} top={options.Region.Y}");
            pipeline.Add("!");
            pipeline.Add("videoconvert");
            pipeline.Add("!");
            pipeline.Add($"videoscale");
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

        // Muxer and output
        pipeline.Add("!");
        pipeline.Add(muxerElement);
        pipeline.Add("!");
        pipeline.Add($"filesink location=\"{finalOutputPath}\"");

        DebugHelper.WriteLine($"[WaylandPortalRecording] GStreamer encoder: {encoderElement.Split(' ')[0]}, muxer: {muxerElement}, output: {finalOutputPath}");

        return string.Join(" ", pipeline);
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
            // VP9 with webm container - deadline=realtime for low latency
            return ($"vp9enc target-bitrate={bitrateKbps * 1000} deadline=realtime cpu-used=8", "webmmux", ".webm");
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
        }
    }
}

[DBusInterface("org.freedesktop.portal.ScreenCast")]
public interface IScreenCastPortal : IDBusObject
{
    Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
    Task<ObjectPath> SelectSourcesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
    Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);
}
