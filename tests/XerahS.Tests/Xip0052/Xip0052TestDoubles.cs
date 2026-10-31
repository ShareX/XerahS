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

using ShareX.ImageEditor.Core.Editor;
using SkiaSharp;
using System.Runtime.CompilerServices;
using XerahS.Bootstrap;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Tasks;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using XerahS.Services.Abstractions;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;
using XerahS.Uploaders.PluginSystem;
using XerahS.UI.Views.Dialogs;

namespace XerahS.Tests.Xip0052;

internal sealed class FakeDesktopTaskManager : IDesktopTaskManager
{
    private EventHandler<WorkerTask>? _taskCompleted;
    private EventHandler<WorkerTask>? _taskStarted;

    public event EventHandler<WorkerTask>? TaskCompleted
    {
        add => _taskCompleted += value;
        remove => _taskCompleted -= value;
    }

    public event EventHandler<WorkerTask>? TaskStarted
    {
        add => _taskStarted += value;
        remove => _taskStarted -= value;
    }

    public IEnumerable<WorkerTask> Tasks => Array.Empty<WorkerTask>();

    public int StartTaskCalls { get; private set; }
    public int StartFileTaskCalls { get; private set; }
    public int StartImageUploadTaskCalls { get; private set; }
    public int StartTextTaskCalls { get; private set; }
    public TaskSettings? LastTaskSettings { get; private set; }
    public string? LastFilePath { get; private set; }
    public string? LastText { get; private set; }

    public Task StartTask(TaskSettings? taskSettings, SKBitmap? inputImage = null)
    {
        StartTaskCalls++;
        LastTaskSettings = taskSettings;
        return Task.CompletedTask;
    }

    public Task StartFileTask(TaskSettings? taskSettings, string filePath)
    {
        StartFileTaskCalls++;
        LastTaskSettings = taskSettings;
        LastFilePath = filePath;
        return Task.CompletedTask;
    }

    public Task StartImageUploadTask(TaskSettings? taskSettings, SKBitmap image)
    {
        StartImageUploadTaskCalls++;
        LastTaskSettings = taskSettings;
        return Task.CompletedTask;
    }

    public Task StartTextTask(TaskSettings? taskSettings, string text)
    {
        StartTextTaskCalls++;
        LastTaskSettings = taskSettings;
        LastText = text;
        return Task.CompletedTask;
    }

    public void StopAllTasks()
    {
    }
}

internal sealed class FakeScreenRecordingCoordinator : IScreenRecordingCoordinator
{
    private EventHandler<RecordingStatusEventArgs>? _statusChanged;
    private EventHandler<RecordingErrorEventArgs>? _errorOccurred;
    private EventHandler<RecordingStartedEventArgs>? _recordingStarted;

    public event EventHandler<RecordingStatusEventArgs>? StatusChanged
    {
        add => _statusChanged += value;
        remove => _statusChanged -= value;
    }

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred
    {
        add => _errorOccurred += value;
        remove => _errorOccurred -= value;
    }

    public event EventHandler<RecordingStartedEventArgs>? RecordingStarted
    {
        add => _recordingStarted += value;
        remove => _recordingStarted -= value;
    }

    public bool IsRecording { get; set; }
    public bool IsPaused { get; set; }
    public bool IsUsingFallback { get; set; }
    public RecordingRuntimeCapabilities CurrentCapabilities { get; set; } = RecordingRuntimeCapabilities.SegmentedRestart;
    public Task? PlatformInitializationTask { get; set; }

    public int AbortCalls { get; private set; }
    public int StartCalls { get; private set; }
    public int StopCalls { get; private set; }
    public int TogglePauseResumeCalls { get; private set; }
    public int SignalStopCalls { get; private set; }
    public RecordingOptions? LastRecordingOptions { get; private set; }

    public Task StartRecordingAsync(RecordingOptions options)
    {
        StartCalls++;
        LastRecordingOptions = options;
        return Task.CompletedTask;
    }

    public Task<string?> StopRecordingAsync()
    {
        StopCalls++;
        return Task.FromResult<string?>(null);
    }

    public Task AbortRecordingAsync()
    {
        AbortCalls++;
        return Task.CompletedTask;
    }

    public Task TogglePauseResumeAsync()
    {
        TogglePauseResumeCalls++;
        return Task.CompletedTask;
    }

    public void SignalStop()
    {
        SignalStopCalls++;
    }

    public void RaiseStatusChanged(RecordingStatus status, TimeSpan duration)
    {
        _statusChanged?.Invoke(this, new RecordingStatusEventArgs(status, duration));
    }

    public void RaiseErrorOccurred(Exception error, bool isFatal = false)
    {
        _errorOccurred?.Invoke(this, new RecordingErrorEventArgs(error, isFatal));
    }

    public void RaiseRecordingStarted(bool isUsingFallback, RecordingOptions options)
    {
        _recordingStarted?.Invoke(this, new RecordingStartedEventArgs(isUsingFallback, options));
    }
}

internal sealed class FakeDialogService : IDialogService
{
    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    public Task<bool> ShowConfirmationAsync(string title, string message) => Task.FromResult(true);
    public Task ShowErrorAsync(string title, string error) => Task.CompletedTask;
    public Task ShowWarningAsync(string title, string warning) => Task.CompletedTask;
    public Task<string?> ShowInputAsync(string title, string label, string? defaultValue = null) => Task.FromResult(defaultValue);
    public Task<T?> ShowSelectionAsync<T>(string title, string label, IEnumerable<T> items) where T : class => Task.FromResult(items.FirstOrDefault());
}

internal sealed class FakeViewDialogService : IViewDialogService
{
    public Task ShowDialogAsync<TWindow>(object dataContext) where TWindow : class, new() => Task.CompletedTask;
    public Task<TResult?> ShowDialogAsync<TWindow, TResult>(object dataContext) where TWindow : class, new() => Task.FromResult(default(TResult));
    public Task<bool> ShowPluginInstallerAsync(PluginInstallerViewModel viewModel) => Task.FromResult(false);
    public Task<bool> ShowCustomUploaderEditorAsync(CustomUploaderEditorViewModel viewModel) => Task.FromResult(false);
    public Task<bool> ShowWorkflowEditorAsync(WorkflowEditorViewModel viewModel) => Task.FromResult(false);
    public Task ShowImageEffectsBrowserAsync(ImageEffectsViewModel viewModel) => Task.CompletedTask;
    public Task ShowFFmpegOptionsAsync(FFmpegOptionsViewModel viewModel) => Task.CompletedTask;
    public Task ShowMediaBrowserAsync(MediaBrowserViewModel viewModel) => Task.CompletedTask;
    public Task ShowQrCodeGeneratorAsync(QrCodeGeneratorViewModel viewModel) => Task.CompletedTask;
    public Task ShowShareLogsAsync(ShareLogsViewModel viewModel) => Task.CompletedTask;
    public Task<bool> ShowWatchFolderEditorAsync(WatchFolderEditViewModel viewModel) => Task.FromResult(false);
    public Task<OpenImageChoice> ShowOpenImageChoiceAsync() => Task.FromResult(OpenImageChoice.Cancel);
    public Task<string?> ShowFilePickerAsync(string title, IEnumerable<string>? filters = null) => Task.FromResult<string?>(null);
    public Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string defaultExtension, IEnumerable<string>? filters = null) => Task.FromResult<string?>(null);
    public Task<string?> ShowSecretInputAsync(string title, string label) => Task.FromResult<string?>(null);
    public Task<string?> ShowFolderPickerAsync(string title) => Task.FromResult<string?>(null);
    public object? GetMainWindow() => null;
    public IEnumerable<object> GetOpenWindows() => Array.Empty<object>();
}

internal sealed class FakeUiViewModelFactory : IUiViewModelFactory
{
    private readonly FakeViewDialogService _viewDialogService = new();
    private readonly FakeDialogService _coreDialogService = new();

    public FakeUiViewModelFactory(IDesktopTaskManager? taskManager = null, IScreenRecordingCoordinator? screenRecordingCoordinator = null)
    {
        TaskManager = taskManager ?? new FakeDesktopTaskManager();
        ScreenRecordingCoordinator = screenRecordingCoordinator ?? new FakeScreenRecordingCoordinator();
    }

    public IViewDialogService ViewDialogService => _viewDialogService;
    public IDialogService CoreDialogService => _coreDialogService;
    public IDesktopTaskManager TaskManager { get; }
    public IScreenRecordingCoordinator ScreenRecordingCoordinator { get; }

    public CustomUploaderEditorViewModel CreateCustomUploaderEditorViewModel() => new();
    public SettingsViewModel CreateApplicationSettingsViewModel() => new();
    public DestinationSettingsViewModel CreateDestinationSettingsViewModel() => new(this);
    public HistoryViewModel CreateHistoryViewModel(bool autoLoadHistory = true) =>
        new(TaskManager, CoreDialogService, autoLoadHistory);
    public IndexFolderViewModel CreateIndexFolderViewModel(TaskSettings? taskSettings = null, bool isWorkflowConfigMode = false) =>
        CreateUninitialized<IndexFolderViewModel>();
    public PluginInstallerViewModel CreatePluginInstallerViewModel() => new(ViewDialogService);
    public MediaBrowserViewModel CreateMediaBrowserViewModel(IReadOnlyList<MediaBrowserSource> sources, MediaBrowserSource? initialSource = null) =>
        new(sources, CoreDialogService, initialSource);
    public QrCodeGeneratorViewModel CreateQrCodeGeneratorViewModel() => new(ViewDialogService);
    public WorkflowsViewModel CreateWorkflowsViewModel() => new(this);
    public WorkflowEditorViewModel CreateWorkflowEditorViewModel(WorkflowSettings model, bool loadUploaderCategories = true) =>
        new(model, this, loadUploaderCategories);
    public RecordingViewModel CreateRecordingViewModel() => new(ScreenRecordingCoordinator);
    public AutoCaptureViewModel CreateAutoCaptureViewModel() => new(TaskManager);
    public UploadContentViewModel CreateUploadContentViewModel() => new(TaskManager);
    public TaskSettingsViewModel CreateTaskSettingsViewModel(TaskSettings settings) =>
        new(settings, ViewDialogService);

    private static T CreateUninitialized<T>() where T : class =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
