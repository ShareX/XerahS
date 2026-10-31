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
using Vortice.MediaFoundation;

namespace XerahS.Platform.Windows.Recording.Native;

/// <summary>A single native recording session. Prepare during a countdown to remove setup from StartAsync.</summary>
public sealed class NativeScreenRecorder : IDisposable, IAsyncDisposable
{
    private readonly NativeRecordingOptions options;
    private readonly RecordingClock clock = new();
    private readonly object sync = new();
    private readonly ManualResetEvent stop = new(false);
    private readonly ManualResetEvent begin = new(false);
    private readonly AutoResetEvent changed = new(false);
    private readonly TaskCompletionSource prepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<NativeRecordingResult> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? worker;
    private bool startRequested, disposed, resumeRequested;
    private Rectangle? resumeRegion;
    private int recording;

    public Task<NativeRecordingResult> Completion => completed.Task;
    public VideoEncoderInfo? Encoder { get; private set; }
    /// <summary>Nonfatal diagnostics; recording continues.</summary>
    public event Action<string>? Diagnostic;
    public event Action<Exception>? AudioCaptureFailed;
    public bool IsRecording => Volatile.Read(ref recording) != 0;
    public bool IsPaused => clock.IsPaused;
    public TimeSpan Elapsed => TimeSpan.FromTicks(clock.Elapsed);

    public NativeScreenRecorder(NativeRecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.options = options with { OutputPath = Path.GetFullPath(options.OutputPath) };
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (worker == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                worker = new Thread(Run) { IsBackground = true, Name = "XerahS native recording" };
                worker.Start();
            }
        }
        // Cancellation stops this session, including its capture threads and partially created output.
        using CancellationTokenRegistration registration = cancellationToken.Register(RequestStop);
        await prepared.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await PrepareAsync(cancellationToken).ConfigureAwait(false);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (startRequested) throw new InvalidOperationException("A recorder instance can only be started once.");
            startRequested = true;
            begin.Set();
        }
        using CancellationTokenRegistration registration = cancellationToken.Register(RequestStop);
        await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Pause()
    {
        lock (sync)
        {
            if (disposed) return;
            resumeRequested = false;
            resumeRegion = null;
            clock.Pause();
            changed.Set();
        }
    }

    public void Resume() => RequestResume(null);

    /// <summary>Resume desktop capture at a new position, keeping the recording's output dimensions.</summary>
    public void Resume(Rectangle region)
    {
        if (options.WindowHandle != 0) throw new InvalidOperationException("Window capture follows its target window rather than a desktop region.");
        if ((region.Width & ~1) != (options.Region.Width & ~1) || (region.Height & ~1) != (options.Region.Height & ~1))
            throw new ArgumentException("Moving the recording region cannot change its output dimensions.", nameof(region));
        RequestResume(region);
    }

    private void RequestResume(Rectangle? region)
    {
        lock (sync)
        {
            if (disposed || !clock.IsPaused) return;
            resumeRegion = region;
            resumeRequested = true;
            changed.Set();
        }
    }

    /// <summary>Nonblocking stop request, safe from a UI thread. Await StopAsync or Completion to finalize MP4.</summary>
    public void RequestStop()
    {
        lock (sync) { if (!disposed) stop.Set(); }
    }

    public Task<NativeRecordingResult> StopAsync()
    {
        lock (sync)
        {
            if (worker == null) throw new InvalidOperationException("The recorder has not been prepared.");
            if (!disposed) stop.Set();
        }
        return Completion;
    }

    private unsafe void Run()
    {
        bool com = false, mediaFoundation = false, ownsOutput = false;
        try
        {
            RecordingInterop.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED).ThrowOnFailure();
            com = true;
            MediaFactory.MFStartup(true).CheckError();
            mediaFoundation = true;
            List<GraphicsCapture.Target> targets = GraphicsCapture.GetTargets(options, out int width, out int height);
            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
            // Reserve the name atomically. Never overwrite or delete a pre-existing user file.
            using (new FileStream(options.OutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            ownsOutput = true;
            using GraphicsDevice graphics = new(targets[0].Monitor);
            using GpuVideoProcessor processor = new(graphics, width, height, options.FramesPerSecond);
            using MediaFoundationWriter writer = new(graphics, options, width, height);
            Encoder = writer.Encoder;
            using VideoTexturePool textures = new(graphics, processor, writer.EncoderBindFlags);
            using GraphicsCapture capture = new(graphics, processor, targets, options.IncludeCursor, options.FramesPerSecond, options.HideCaptureBorder);
            AudioMixer mixer = new(clock);
            using WasapiCapture? audio = options.HasAudio ? new(options, mixer) : null;
            prepared.TrySetResult();
            if (WaitHandle.WaitAny([stop, begin]) == 0) throw new OperationCanceledException("Recording stopped before capture began.");
            audio?.Start();
            capture.Start();
            long waitStart = RecordingClock.Now;
            while (!capture.HasFrame)
            {
                if (stop.WaitOne(5)) throw new OperationCanceledException("Recording stopped before the first video frame.");
                if (audio?.Failure is Exception audioFailure) throw new IOException("Windows audio capture failed.", audioFailure);
                capture.Update();
                if (capture.HasEnded) throw new IOException("The capture target closed or changed size before recording started.");
                if (RecordingClock.Now - waitStart > TimeSpan.FromSeconds(10).Ticks)
                    throw new TimeoutException("Windows did not deliver a capture frame. The window may be minimized, protected, or unavailable.");
            }
            clock.Start(capture.LatestTimestamp);
            Volatile.Write(ref recording, 1);
            long nextFrame = 0, written = 0, dropped = 0, audioFrame = 0;
            bool discontinuity = false;
            short[] audioBlock = new short[480 * 2]; // 10 ms, 48 kHz stereo
            using FrameTimer timer = new();
            WaitHandle[] waits = [stop, changed, timer];
            long duration = 0;
            Rectangle currentRegion = options.Region;
            long resumeWaitStart = 0, lastTrack = 0, lastRetarget = 0;
            bool audioDisabled = false;
            while (!stop.WaitOne(0))
            {
                if (capture.HasEnded) break;
                if (!audioDisabled && audio?.Failure is Exception audioFailure)
                {
                    audioDisabled = true;
                    audio.Dispose();
                    ReportDiagnostic($"Audio capture stopped; video recording continues with silence. {audioFailure.Message}");
                    try { AudioCaptureFailed?.Invoke(audioFailure); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }
                if (clock.IsPaused)
                {
                    Rectangle? requestedRegion;
                    lock (sync) requestedRegion = resumeRequested ? resumeRegion : null;
                    if (requestedRegion is Rectangle region && region != currentRegion)
                    {
                        // Only the capture sessions change. Keep the GPU, encoder, audio and
                        // media timeline alive, including when the region moves onto another display.
                        List<GraphicsCapture.Target> movedTargets = GraphicsCapture.GetTargets(options with { Region = region }, out _, out _);
                        capture.Retarget(movedTargets);
                        currentRegion = region;
                        resumeWaitStart = RecordingClock.Now;
                    }
                    // Drain WGC so resume never displays frames retained during the pause.
                    capture.Update();
                    if (capture.HasEnded) break;
                    if (!capture.HasFrame && RecordingClock.Now - resumeWaitStart > TimeSpan.FromSeconds(10).Ticks)
                        throw new TimeoutException("Windows did not deliver a frame for the moved recording region.");
                    WriteAudioUntil(writer, mixer, audioBlock, ref audioFrame, clock.Elapsed, options.HasAudio);
                    lock (sync)
                    {
                        // Keep media time paused until the new region has a frame. A later pause or
                        // resume request supersedes this one without briefly recording an old target.
                        if (resumeRequested && resumeRegion == requestedRegion && capture.HasFrame)
                        {
                            clock.Resume();
                            resumeRequested = false;
                            resumeRegion = null;
                        }
                    }
                    discontinuity = true;
                    if (!clock.IsPaused) continue;
                    timer.Arm(TimeSpan.FromMilliseconds(20).Ticks);
                    if (WaitHandle.WaitAny(waits) == 0) break;
                    continue;
                }
                long elapsed = clock.Elapsed;
                if (options.Duration > TimeSpan.Zero && elapsed >= options.Duration.Ticks) break;
                long frameTime = nextFrame * TimeSpan.TicksPerSecond / options.FramesPerSecond;
                if (elapsed >= frameTime)
                {
                    // Skip overdue frame slots. Never accelerate playback or queue a catch-up burst.
                    long dueFrame = elapsed * options.FramesPerSecond / TimeSpan.TicksPerSecond;
                    if (written > 0 && dueFrame > nextFrame)
                    {
                        dropped += dueFrame - nextFrame;
                        nextFrame = dueFrame;
                        discontinuity = true;
                    }
                    if (options.TrackWindowHandle != 0)
                    {
                        if (!RecordingInterop.IsWindow(options.TrackWindowHandle)) break;
                        FollowTrackedWindow(capture, ref currentRegion, ref lastTrack, ref lastRetarget);
                    }
                    capture.Update();
                    long timestamp = nextFrame * TimeSpan.TicksPerSecond / options.FramesPerSecond;
                    if (capture.HasEnded) break;
                    long end = (nextFrame + 1) * TimeSpan.TicksPerSecond / options.FramesPerSecond;
                    if (textures.TryRent(out VideoTexturePool.Slot? slot))
                    {
                        try { processor.Convert(slot!.View, (uint)nextFrame); }
                        catch { textures.Return(slot!); throw; }
                        using var sample = textures.CreateSample(slot!);
                        writer.WriteVideo(sample, timestamp, end - timestamp, discontinuity);
                        written++;
                        duration = end;
                        discontinuity = false;
                        started.TrySetResult();
                    }
                    else { dropped++; discontinuity = true; }
                    nextFrame++;
                }
                // Allow 50 ms for WASAPI packets to arrive; timestamps remain aligned with video.
                WriteAudioUntil(writer, mixer, audioBlock, ref audioFrame, Math.Max(0, clock.Elapsed - TimeSpan.FromMilliseconds(50).Ticks), options.HasAudio);
                long remaining = nextFrame * TimeSpan.TicksPerSecond / options.FramesPerSecond - clock.Elapsed;
                timer.Arm(remaining);
                if (WaitHandle.WaitAny(waits) == 0) break;
            }
            // Stop and drain capture before writing the final audio blocks and finalizing the container.
            audio?.Dispose();
            clock.Stop();
            duration = Math.Max(duration, options.Duration > TimeSpan.Zero ? Math.Min(clock.Elapsed, options.Duration.Ticks) : clock.Elapsed);
            WriteAudioUntil(writer, mixer, audioBlock, ref audioFrame, duration, options.HasAudio, true);
            if (written == 0) throw new IOException("No video frames were accepted by the encoder.");
            writer.Finish();
            completed.TrySetResult(new(options.OutputPath, TimeSpan.FromTicks(duration), written, dropped, mixer.Discontinuities, writer.Encoder));
        }
        catch (Exception ex)
        {
            if (ownsOutput)
            {
                try { File.Delete(options.OutputPath); }
                catch (Exception cleanup) { ex = new AggregateException("Recording failed and its incomplete output could not be removed.", ex, cleanup); }
            }
            prepared.TrySetException(ex); started.TrySetException(ex); completed.TrySetException(ex);
        }
        finally
        {
            clock.Stop();
            Volatile.Write(ref recording, 0);
            if (mediaFoundation) MediaFactory.MFShutdown();
            if (com) RecordingInterop.CoUninitialize();
        }
    }

    /// <summary>
    /// Popup-inclusive window recording: the desktop region follows the window, keeping the output size fixed.
    /// Moves within the same displays only update the GPU crop; a move onto another display recreates the sessions.
    /// </summary>
    private void FollowTrackedWindow(GraphicsCapture capture, ref Rectangle currentRegion, ref long lastTrack, ref long lastRetarget)
    {
        long now = RecordingClock.Now;
        if (now - lastTrack < TimeSpan.FromMilliseconds(15).Ticks) return;
        lastTrack = now;
        if (RecordingInterop.IsIconic(options.TrackWindowHandle) ||
            !RecordingInterop.TryGetWindowBounds(options.TrackWindowHandle, out Rectangle bounds)) return;
        Rectangle moved = new(bounds.Location, currentRegion.Size);
        if (moved == currentRegion) return;
        if (capture.TryMove(moved))
        {
            currentRegion = moved;
            return;
        }
        // Throttle session recreation while the window is dragged across a display boundary.
        if (now - lastRetarget < TimeSpan.FromMilliseconds(250).Ticks) return;
        lastRetarget = now;
        try
        {
            capture.Retarget(GraphicsCapture.GetTargets(options with { Region = moved }, out _, out _));
            currentRegion = moved;
        }
        catch (ArgumentException)
        {
            // The window moved entirely off the desktop; keep the last region until it returns.
        }
    }

    private void ReportDiagnostic(string message)
    {
        try { Diagnostic?.Invoke(message); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private static void WriteAudioUntil(MediaFoundationWriter writer, AudioMixer mixer, short[] block,
        ref long firstFrame, long until, bool enabled, bool partial = false)
    {
        if (!enabled) return;
        long endFrame = (long)((Int128)until * AudioMixer.SampleRate / TimeSpan.TicksPerSecond);
        while (firstFrame + block.Length / 2 <= endFrame || (partial && firstFrame < endFrame))
        {
            int count = (int)Math.Min(block.Length / 2, endFrame - firstFrame);
            Span<short> output = block.AsSpan(0, count * 2);
            mixer.Read(firstFrame, output);
            writer.WriteAudio(output, firstFrame);
            firstFrame += count;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            stop.Set();
        }
        worker?.Join();
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            stop.Dispose(); begin.Dispose(); changed.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        RequestStop();
        if (worker != null) { try { await Completion.ConfigureAwait(false); } catch { } }
        Dispose();
    }
}
