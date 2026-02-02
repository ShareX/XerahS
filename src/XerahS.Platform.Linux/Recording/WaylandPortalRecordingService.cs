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
    private Task? _ffmpegTask;
    private RecordingOptions? _currentOptions;
    private RecordingStatus _status = RecordingStatus.Idle;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private bool _disposed;

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
            InitializePortalSession(options).GetAwaiter().GetResult();

            string ffmpegPath = PathsManager.GetFFmpegPath();
            if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                throw new FileNotFoundException("FFmpeg not found for Wayland portal recording.", ffmpegPath);
            }

            string args = BuildFFmpegArguments(options, _pipewireNodeId);
            DebugHelper.WriteLine($"[WaylandPortalRecording] FFmpeg args: {args}");

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

    public async Task StopRecordingAsync()
    {
        FFmpegCLIManager? ffmpeg;
        Task? ffmpegTask;

        lock (_lock)
        {
            if (_status != RecordingStatus.Recording)
            {
                return;
            }

            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();

            ffmpeg = _ffmpeg;
            ffmpegTask = _ffmpegTask;
        }

        try
        {
            if (ffmpeg != null)
            {
                ffmpeg.StopRequested = true;
                ffmpeg.WriteInput("q");
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
                _currentOptions = null;
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
            ["cursor_mode"] = (uint)((options.Settings?.ShowCursor ?? true) ? 1 : 0)
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

    private static string BuildFFmpegArguments(RecordingOptions options, uint pipeWireNodeId)
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

        string outputPath = options.OutputPath ?? GetDefaultOutputPath();
        args.Add($"\"{outputPath}\"");

        return string.Join(" ", args);
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
internal interface IScreenCastPortal : IDBusObject
{
    Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
    Task<ObjectPath> SelectSourcesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
    Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);
}
