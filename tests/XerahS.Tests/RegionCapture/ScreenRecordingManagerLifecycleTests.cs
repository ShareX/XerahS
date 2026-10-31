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
using NUnit.Framework;
using XerahS.Core.Managers;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.RegionCapture;

[TestFixture, NonParallelizable]
public sealed class ScreenRecordingManagerLifecycleTests
{
    private Func<IRecordingService>? _nativeFactory, _fallbackFactory;
    private Task? _initialization;
    private ScreenRecordingManager _manager = null!;
    private FakeRecording _native = null!, _fallback = null!;
    private string _folder = null!;
    private static readonly byte[] CompleteContainer = [0, 0, 0, 9, 109, 111, 111, 118, 1, 0, 0, 0, 9, 109, 100, 97, 116, 1];

    [SetUp]
    public void SetUp()
    {
        _nativeFactory = ScreenRecorderService.NativeRecordingServiceFactory;
        _fallbackFactory = ScreenRecorderService.FallbackServiceFactory;
        _initialization = ScreenRecordingManager.PlatformInitializationTask;
        ScreenRecordingManager.PlatformInitializationTask = Task.CompletedTask;
        _folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "native-recording-lifecycle", Guid.NewGuid().ToString("N"));
        _manager = new ScreenRecordingManager();
        _native = new FakeRecording(); _fallback = new FakeRecording();
        ScreenRecorderService.NativeRecordingServiceFactory = () => _native;
        ScreenRecorderService.FallbackServiceFactory = () => _fallback;
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_manager.IsRecording) await _manager.AbortRecordingAsync();
        _native.Dispose();
        _fallback.Dispose();
        ScreenRecorderService.NativeRecordingServiceFactory = _nativeFactory;
        ScreenRecorderService.FallbackServiceFactory = _fallbackFactory;
        ScreenRecordingManager.PlatformInitializationTask = _initialization;
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private RecordingOptions Options() => new()
    {
        Mode = CaptureMode.Region, Region = new Rectangle(-100, 50, 400, 300),
        OutputPath = Path.Combine(_folder, "capture.mp4"), Settings = new ScreenRecordingSettings()
    };

    [TestCase(false), TestCase(true)]
    public async Task RecoverableNativeFailure_ReleasesStateAndStartsRegisteredFallback(bool platformException)
    {
        _native.StartError = platformException ? new PlatformNotSupportedException("GPU unavailable") : new NotSupportedException("Encoder unavailable");
        await _manager.StartRecordingAsync(Options());
        Assert.Multiple(() =>
        {
            Assert.That(_native.Disposed, Is.True);
            Assert.That(_fallback.Starts, Is.EqualTo(1));
            Assert.That(_manager.IsUsingFallback, Is.True);
        });
        Assert.That(await _manager.StopRecordingAsync(), Is.EqualTo(Path.Combine(_folder, "capture.mp4")));
    }

    [Test]
    public void InvalidRequest_DoesNotFallBackAndLeavesManagerReusable()
    {
        _native.StartError = new ArgumentException("Invalid region");
        Assert.ThrowsAsync<ArgumentException>(() => _manager.StartRecordingAsync(Options()));
        Assert.That(_fallback.Starts, Is.Zero);
        Assert.That(_manager.IsRecording, Is.False);
    }

    [Test]
    public void OccupiedOutput_PreventsStartingAnyBackend()
    {
        var options = Options();
        Directory.CreateDirectory(_folder);
        File.WriteAllText(options.OutputPath!, "existing recording");
        Assert.ThrowsAsync<IOException>(() => _manager.StartRecordingAsync(options));
        Assert.That(File.ReadAllText(options.OutputPath!), Is.EqualTo("existing recording"));
        Assert.That(_fallback.Starts, Is.Zero);
        Assert.That(_manager.IsRecording, Is.False);
    }

    [Test]
    public void NativeFailure_ThatLeavesAnOccupiedSegment_PreventsFallbackOverwrite()
    {
        _native.StartError = new PlatformNotSupportedException("GPU failed and cleanup could not remove the partial file");
        _native.LeaveOutputOnFailure = true;
        Assert.ThrowsAsync<IOException>(() => _manager.StartRecordingAsync(Options()));
        Assert.That(File.ReadAllText(Path.Combine(_folder, "capture.part000.mp4")), Is.EqualTo("partial recording"));
        Assert.That(_fallback.Starts, Is.Zero);
        Assert.That(_manager.IsRecording, Is.False);
    }

    [Test]
    public async Task StartingAgain_LeavesTheActiveSessionAndOutputIntact()
    {
        var options = Options();
        await _manager.StartRecordingAsync(options);
        var duplicate = Options(); duplicate.OutputPath = Path.Combine(_folder, "other.mp4");
        Assert.ThrowsAsync<InvalidOperationException>(() => _manager.StartRecordingAsync(duplicate));
        Assert.That(_native.Starts, Is.EqualTo(1));
        Assert.That(await _manager.StopRecordingAsync(), Is.EqualTo(options.OutputPath));
    }

    [Test]
    public async Task Finalization_DoesNotOverwriteAFileCreatedAfterStart()
    {
        var options = Options();
        await _manager.StartRecordingAsync(options);
        File.WriteAllText(options.OutputPath!, "new user file");
        Assert.ThrowsAsync<IOException>(() => _manager.StopRecordingAsync());
        Assert.That(File.ReadAllText(options.OutputPath!), Is.EqualTo("new user file"));
        Assert.That(File.Exists(Path.Combine(_folder, "capture.part000.mp4")), Is.True);
    }

    [Test]
    public async Task Abort_DoesNotDeleteAFileCreatedAtThePlannedFinalPath()
    {
        var options = Options();
        await _manager.StartRecordingAsync(options);
        File.WriteAllText(options.OutputPath!, "new user file");
        await _manager.AbortRecordingAsync();
        Assert.That(File.ReadAllText(options.OutputPath!), Is.EqualTo("new user file"));
        Assert.That(File.Exists(Path.Combine(_folder, "capture.part000.mp4")), Is.False);
    }

    [Test]
    public void FailedFallback_ClearsManagerState()
    {
        _native.StartError = new NotSupportedException("GPU unavailable");
        _fallback.StartError = new IOException("FFmpeg unavailable");
        Assert.ThrowsAsync<IOException>(() => _manager.StartRecordingAsync(Options()));
        Assert.That(_manager.IsRecording, Is.False);
        Assert.That(_fallback.Disposed, Is.True);
    }

    [TestCase(false), TestCase(true)]
    public async Task NaturalCompletion_UnblocksWorkflowEvenBeforeItStartsWaiting_AndPreservesOutput(bool beforeWait)
    {
        _native.CompleteDuringStart = beforeWait;
        await _manager.StartRecordingAsync(Options());
        Task stopped = _manager.WaitForStopSignalAsync();
        if (!beforeWait) _native.Complete();
        await stopped.WaitAsync(TimeSpan.FromSeconds(2));
        string? output = await _manager.StopRecordingAsync();
        Assert.That(output, Is.EqualTo(Path.Combine(_folder, "capture.mp4")));
        Assert.That(File.ReadAllBytes(output!), Is.EqualTo(CompleteContainer));
        Assert.That(_manager.IsRecording, Is.False);
    }

    [Test]
    public async Task PauseMoveResume_UsesOneSessionAndUpdatesRestartRegion()
    {
        await _manager.StartRecordingAsync(Options());
        await _manager.PauseRecordingAsync();
        var moved = new Rectangle(2000, -100, 400, 300);
        await _manager.ResumeRecordingAtAsync(moved);
        Assert.Multiple(() =>
        {
            Assert.That(_native.Starts, Is.EqualTo(1));
            Assert.That(_native.ResumedRegion, Is.EqualTo(moved));
            Assert.That(_manager.CurrentOptions!.Region, Is.EqualTo(moved));
            Assert.That(_manager.IsPaused, Is.False);
        });
    }

    [Test]
    public async Task MovingPausedRegion_RejectsResizing()
    {
        await _manager.StartRecordingAsync(Options());
        await _manager.PauseRecordingAsync();
        Assert.ThrowsAsync<ArgumentException>(() => _manager.ResumeRecordingAtAsync(new Rectangle(0, 0, 500, 300)));
        Assert.That(_manager.IsPaused, Is.True);
    }

    [Test]
    public async Task SegmentedFallbackResume_PreservesTheWorkflowStopSignal()
    {
        _fallback.PauseBehavior = RecordingPauseBehavior.SegmentedRestart;
        var options = Options(); options.Settings!.ForceFFmpeg = true;
        await _manager.StartRecordingAsync(options);
        Task stopped = _manager.WaitForStopSignalAsync();
        await _manager.PauseRecordingAsync();
        _fallback = new FakeRecording { PauseBehavior = RecordingPauseBehavior.SegmentedRestart };
        await _manager.ResumeRecordingAsync();
        _manager.SignalStop();
        await stopped.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(_manager.IsPaused, Is.False);
    }

    [Test]
    public async Task AbortAndWorkflowStop_AreSerialized_AndDoNotPublishAnAbortedTake()
    {
        _native.StopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _manager.StartRecordingAsync(Options());
        int completions = 0;
        _manager.RecordingCompleted += (_, _) => completions++;
        Task abort = _manager.AbortRecordingAsync();
        Task<string?> stop = _manager.StopRecordingAsync();
        try
        {
            Assert.That(stop.IsCompleted, Is.False);
            Assert.That(_native.Stops, Is.EqualTo(1));
        }
        finally { _native.StopGate.TrySetResult(); }
        await abort;
        Assert.That(await stop, Is.Null);
        Assert.That(completions, Is.Zero);
        Assert.That(Directory.GetFiles(_folder), Is.Empty);
    }

    [Test]
    public async Task StopWhilePaused_FinalizesOnce_AndRestartStartsFreshSession()
    {
        await _manager.StartRecordingAsync(Options());
        await _manager.PauseRecordingAsync();
        string? first = await _manager.StopRecordingAsync();
        Assert.That(await _manager.StopRecordingAsync(), Is.EqualTo(first));
        Assert.That(_native.Stops, Is.EqualTo(1));
        _native = new FakeRecording();
        var options = Options(); options.OutputPath = Path.Combine(_folder, "second.mp4");
        await _manager.StartRecordingAsync(options);
        Assert.That(_manager.IsPaused, Is.False);
        Assert.That(_native.Starts, Is.EqualTo(1));
    }

    [Test]
    public async Task FatalRecordingFailure_UnblocksWorkflowAndClearsActiveBackend()
    {
        await _manager.StartRecordingAsync(Options());
        Task stopped = _manager.WaitForStopSignalAsync();
        _native.Fail();
        await stopped.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(_manager.IsRecording, Is.False);
        Assert.That(_native.Disposed, Is.True);
        Assert.That(_fallback.Starts, Is.Zero, "An in-progress recording must not silently restart from time zero.");
    }

    [Test]
    public async Task FatalFailure_NeverPublishesOrRecoversAPathAsSuccess()
    {
        await _manager.StartRecordingAsync(Options());
        int completions = 0;
        _manager.RecordingCompleted += (_, _) => completions++;
        _native.Fail();
        Assert.ThrowsAsync<InvalidOperationException>(() => _manager.StopRecordingAsync());
        Assert.That(completions, Is.Zero);
        Assert.That(File.Exists(Path.Combine(_folder, "capture.part000.mp4")), Is.True);
    }

    [Test]
    public async Task FatalError_IsRecordedBeforeReentrantStopFromAnErrorSubscriber()
    {
        await _manager.StartRecordingAsync(Options());
        Task<string?>? stop = null;
        _manager.ErrorOccurred += (_, _) => stop = _manager.StopRecordingAsync();
        int completions = 0;
        _manager.RecordingCompleted += (_, _) => completions++;
        _native.Fail();
        Assert.That(stop, Is.Not.Null);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await stop!);
        Assert.That(completions, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task IncompleteOutput_NeverCompletesAndKeepsPartialFile(bool empty)
    {
        await _manager.StartRecordingAsync(Options());
        string partial = Path.Combine(_folder, "capture.part000.mp4");
        File.WriteAllBytes(partial, empty ? [] : [0, 0, 0, 9, 109, 100, 97, 116, 1]);
        int completions = 0;
        _manager.RecordingCompleted += (_, _) => completions++;
        Assert.ThrowsAsync<InvalidDataException>(() => _manager.StopRecordingAsync());
        Assert.That(completions, Is.Zero);
        Assert.That(File.Exists(partial), Is.True);
    }

    private sealed class FakeRecording : IRecordingService, IMovableRecordingService, IRecordingCompletionSource
    {
        public Exception? StartError;
        public bool CompleteDuringStart, Disposed, LeaveOutputOnFailure;
        public int Starts, Stops;
        public RecordingPauseBehavior PauseBehavior = RecordingPauseBehavior.NativePauseResume;
        public TaskCompletionSource? StopGate;
        public Rectangle? ResumedRegion;
        public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        public event EventHandler? RecordingEnded;
        public RecordingRuntimeCapabilities GetCapabilities(RecordingOptions options) =>
            new(PauseBehavior, SupportsRegionMove: PauseBehavior == RecordingPauseBehavior.NativePauseResume);
        public Task StartRecordingAsync(RecordingOptions options)
        {
            Starts++;
            if (StartError != null)
            {
                if (LeaveOutputOnFailure)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath!)!);
                    File.WriteAllText(options.OutputPath!, "partial recording");
                }
                throw StartError;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath!)!);
            File.WriteAllBytes(options.OutputPath!, CompleteContainer);
            StatusChanged?.Invoke(this, new(RecordingStatus.Recording, TimeSpan.Zero));
            if (CompleteDuringStart) Complete();
            return Task.CompletedTask;
        }
        public async Task StopRecordingAsync()
        {
            Stops++;
            if (StopGate != null) await StopGate.Task;
            StatusChanged?.Invoke(this, new(RecordingStatus.Idle, TimeSpan.FromSeconds(1)));
        }
        public Task PauseRecordingAsync() => Task.CompletedTask;
        public Task ResumeRecordingAsync() => Task.CompletedTask;
        public Task ResumeRecordingAsync(Rectangle region) { ResumedRegion = region; return Task.CompletedTask; }
        public void Complete() => RecordingEnded?.Invoke(this, EventArgs.Empty);
        public void Fail() => ErrorOccurred?.Invoke(this, new(new IOException("Encoder failed"), true));
        public void Dispose() => Disposed = true;
    }
}
