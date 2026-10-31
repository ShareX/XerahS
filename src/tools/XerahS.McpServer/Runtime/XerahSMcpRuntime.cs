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

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Services;
using XerahS.Core.Tasks;
using XerahS.History;
using XerahS.McpServer.Transport;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.McpServer.Runtime;

public sealed class XerahSMcpRuntime : IXerahSMcpRuntime
{
    internal const long MaxInlineHistoryBlobBytes = McpHistoryService.MaxInlineBlobBytes;

    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private readonly McpHistoryService _historyService;
    private readonly McpSettingsWorkflowService _settingsWorkflowService;
    private readonly McpResourceService _resourceService;
    private IServiceProvider? _services;
    private IDesktopTaskManager? _taskManager;

    public XerahSMcpRuntime()
    {
        _historyService = new McpHistoryService();
        _settingsWorkflowService = new McpSettingsWorkflowService();
        _resourceService = new McpResourceService(_historyService, _settingsWorkflowService);
    }

    internal XerahSMcpRuntime(
        McpHistoryService historyService,
        McpSettingsWorkflowService settingsWorkflowService,
        McpResourceService resourceService)
    {
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _settingsWorkflowService = settingsWorkflowService ?? throw new ArgumentNullException(nameof(settingsWorkflowService));
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
    }

    public string ServerVersion => Server.Capabilities.ServerVersion;

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return SettingsManager.Settings.McpApiKey;
    }

    public async Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var settings = CreateCaptureTaskSettings(workflowId, WorkflowType.RectangleRegion);
        var selection = await PlatformServices.ScreenCapture.SelectRegionAsync(CreateCaptureOptions(settings));

        if (selection.IsEmpty)
        {
            throw new McpUserCancelledException("Capture cancelled by user.");
        }

        if (monitor != null)
        {
            var screens = PlatformServices.Screen.GetAllScreens();
            if (monitor < 0 || monitor >= screens.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(monitor), "The requested monitor index is out of range.");
            }

            var bounds = screens[monitor.Value].Bounds;
            var intersected = new SkiaSharp.SKRectI(
                Math.Max(selection.Left, bounds.Left),
                Math.Max(selection.Top, bounds.Top),
                Math.Min(selection.Right, bounds.Right),
                Math.Min(selection.Bottom, bounds.Bottom));

            if (intersected.Width <= 0 || intersected.Height <= 0)
            {
                throw new InvalidOperationException("The selected region does not intersect the requested monitor.");
            }

            selection = intersected;
        }

        using var image = await PlatformServices.ScreenCapture.CaptureRectAsync(selection, CreateCaptureOptions(settings))
            ?? throw new InvalidOperationException("XerahS failed to capture the selected region.");
        var savedPath = SaveImageToFile(image, settings);
        _historyService.AppendItem(savedPath, "Image");

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["monitor"] = monitor,
            ["workflow_id"] = workflowId
        };
    }

    public async Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var settings = CreateCaptureTaskSettings(null, WorkflowType.ActiveWindow);
        settings.CaptureSettings.CaptureClientArea = !includeDecoration;

        var handle = string.IsNullOrWhiteSpace(windowTitle)
            ? PlatformServices.Window.GetForegroundWindow()
            : PlatformServices.Window.SearchWindow(windowTitle);

        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("No matching window was found.");
        }

        using var image = await PlatformServices.ScreenCapture.CaptureWindowAsync(handle, PlatformServices.Window, CreateCaptureOptions(settings))
            ?? throw new InvalidOperationException("XerahS failed to capture the requested window.");
        var savedPath = SaveImageToFile(image, settings);
        _historyService.AppendItem(savedPath, "Image", null, PlatformServices.Window.GetWindowText(handle), ResolveProcessName(handle));

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["window_title"] = PlatformServices.Window.GetWindowText(handle),
            ["include_decoration"] = includeDecoration
        };
    }

    public async Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var settings = CreateCaptureTaskSettings(null, WorkflowType.PrintScreen);
        using var image = await CaptureFullScreenBitmapAsync(monitor, settings);
        var savedPath = SaveImageToFile(image, settings);
        _historyService.AppendItem(savedPath, "Image");

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["monitor"] = monitor
        };
    }

    public async Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        scrollDirection = string.IsNullOrWhiteSpace(scrollDirection) ? "down" : scrollDirection.Trim().ToLowerInvariant();
        if (!string.Equals(scrollDirection, "down", StringComparison.Ordinal))
        {
            throw new NotSupportedException("capture_scrolling currently supports only scroll_direction='down'.");
        }

        maxFrames = Math.Clamp(maxFrames, 1, 1000);

        if (PlatformServices.ScrollingCapture == null || !PlatformServices.ScrollingCapture.IsSupported)
        {
            throw new InvalidOperationException("Scrolling capture is not supported on this platform.");
        }

        var handle = PlatformServices.Window.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("No active window is available for scrolling capture.");
        }

        var bounds = PlatformServices.Window.GetWindowClientBounds(handle);
        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("The active window does not expose a capturable client area.");
        }

        var settings = CreateCaptureTaskSettings(null, WorkflowType.ScrollingCapture);
        var options = settings.CaptureSettings.ScrollingCaptureOptions ?? new ScrollingCaptureOptions();
        var manager = new ScrollingCaptureManager(
            PlatformServices.ScrollingCapture,
            PlatformServices.ScreenCapture,
            PlatformServices.Window);

        var result = await manager.CaptureAsync(
            handle,
            new SkiaSharp.SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom),
            options.ScrollMethod,
            scrollAmount: options.ScrollAmount,
            startDelayMs: options.StartDelay,
            scrollDelayMs: options.ScrollDelay,
            maxFrames: maxFrames,
            autoScrollTop: true,
            autoIgnoreBottomEdge: options.AutoIgnoreBottomEdge,
            cancellationToken: cancellationToken);

        using var image = result.Image ?? throw new InvalidOperationException("Scrolling capture did not produce an image.");
        var savedPath = SaveImageToFile(image, settings);
        _historyService.AppendItem(savedPath, "Image", null, PlatformServices.Window.GetWindowText(handle), ResolveProcessName(handle));

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["frames_captured"] = result.FramesCaptured,
            ["status"] = result.Status.ToString(),
            ["scroll_direction"] = scrollDirection,
            ["max_frames"] = maxFrames
        };
    }

    public async Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("image_path is required.", nameof(imagePath));
        }

        var absolutePath = Path.GetFullPath(imagePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The specified image_path does not exist.", absolutePath);
        }

        using var bitmap = SkiaSharp.SKBitmap.Decode(absolutePath)
            ?? throw new InvalidOperationException("The specified image could not be decoded.");
        var applied = annotations != null ? SkiaAnnotationRenderer.ApplyAnnotations(bitmap, annotations) : Array.Empty<string>();
        var outputPath = GetAnnotatedOutputPath(absolutePath);
        SaveBitmap(bitmap, outputPath);

        return new JsonObject
        {
            ["input_path"] = absolutePath,
            ["output_path"] = outputPath,
            ["auto_save"] = autoSave,
            ["annotations_applied"] = McpJsonSerialization.ToJsonArray(applied),
            ["interactive_editor_available"] = false
        };
    }

    public async Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("file_path is required.", nameof(filePath));
        }

        var absolutePath = Path.GetFullPath(filePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The specified file_path does not exist.", absolutePath);
        }

        var settings = CreateUploadTaskSettings(absolutePath, destination);
        var task = await RunTaskAsync(
            () => _taskManager!.StartFileTask(settings, absolutePath),
            TimeSpan.FromMinutes(5),
            cancellationToken);

        var url = task.Info.Metadata?.UploadURL;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(task.Error?.Message ?? "Upload failed.");
        }

        var fileInfo = new FileInfo(task.Info.FilePath);
        return new JsonObject
        {
            ["url"] = url,
            ["filename"] = fileInfo.Name,
            ["size_bytes"] = fileInfo.Length,
            ["destination"] = McpSettingsWorkflowService.ResolveDestinationSummary(settings.DestinationInstanceId)
        };
    }

    public async Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (PlatformServices.Clipboard.ContainsImage())
        {
            using var image = PlatformServices.Clipboard.GetImage();
            if (image == null)
            {
                throw new InvalidOperationException("Clipboard reported image data but no image could be read.");
            }

            var settings = CreateUploadTaskSettings("clipboard.png", destination, UploaderCategory.Image);
            var task = await RunTaskAsync(
                () => _taskManager!.StartImageUploadTask(settings, image.Copy()),
                TimeSpan.FromMinutes(5),
                cancellationToken);

            return CreateUploadTaskResult(task, "clipboard.png");
        }

        if (PlatformServices.Clipboard.ContainsText())
        {
            var text = await PlatformServices.Clipboard.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Clipboard text was empty.");
            }

            var settings = CreateUploadTaskSettings("clipboard.txt", destination, UploaderCategory.Text);
            var task = await RunTaskAsync(
                () => _taskManager!.StartTextTask(settings, text),
                TimeSpan.FromMinutes(5),
                cancellationToken);

            return CreateUploadTaskResult(task, "clipboard.txt");
        }

        if (PlatformServices.Clipboard.ContainsFileDropList())
        {
            var files = await PlatformServices.Clipboard.GetFileDropListAsync();
            var firstFile = files?.FirstOrDefault(path => File.Exists(path));
            if (!string.IsNullOrWhiteSpace(firstFile))
            {
                return await UploadFileAsync(firstFile, destination, cancellationToken);
            }
        }

        throw new InvalidOperationException("Clipboard does not currently contain supported image, text, or file data.");
    }

    public async Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _historyService.Query(query, fromDate, toDate, fileType, limit);
    }

    public async Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _historyService.GetItemAsync(id, cancellationToken);
    }

    public async Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _settingsWorkflowService.ListWorkflows();
    }

    public async Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _settingsWorkflowService.GetSettings(category);
    }

    public async Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _resourceService.ReadAsync(uri, cancellationToken);
    }

    internal static bool IsHistorySearchResourceUri(string uri) =>
        McpResourceService.IsHistorySearchResourceUri(uri);

    internal static string? DecodeResourceQueryComponent(string value) =>
        McpResourceService.DecodeResourceQueryComponent(value);

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_services != null && _taskManager != null && PlatformServices.IsInitialized)
        {
            return;
        }

        await _initializeGate.WaitAsync(cancellationToken);
        try
        {
            if (_services != null && _taskManager != null && PlatformServices.IsInitialized)
            {
                return;
            }

            var result = await ShareXBootstrap.InitializeAsync(new BootstrapOptions
            {
                EnableLogging = true,
                InitializeRecording = true,
                UIService = new HeadlessMcpUIService(),
                ToastService = new HeadlessMcpToastService()
            });

            _services = result.ServiceProvider ?? throw new InvalidOperationException("XerahS bootstrap did not provide a service provider.");
            _taskManager = _services.GetRequiredService<IDesktopTaskManager>();
            EnsureApiKey();
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    private static void EnsureApiKey()
    {
        if (!string.IsNullOrWhiteSpace(SettingsManager.Settings.McpApiKey))
        {
            return;
        }

        SettingsManager.Settings.McpApiKey = HttpServer.GenerateApiKey();
        SettingsManager.SaveApplicationConfig();
    }

    private static TaskSettings CreateCaptureTaskSettings(string? workflowId, WorkflowType fallbackJob)
    {
        var settings = CloneTaskSettings(ResolveWorkflowTaskSettings(workflowId));
        settings.Job = fallbackJob;
        settings.AfterCaptureJob = AfterCaptureTasks.None;
        settings.AfterUploadJob = AfterUploadTasks.None;
        settings.WorkflowId = workflowId ?? settings.WorkflowId;
        return settings;
    }

    private static TaskSettings CreateUploadTaskSettings(string filePathOrName, string? destination, UploaderCategory? forcedCategory = null)
    {
        var settings = CloneTaskSettings(SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload).TaskSettings);
        settings.Job = WorkflowType.FileUpload;
        settings.AfterCaptureJob = AfterCaptureTasks.None;
        settings.AfterUploadJob = AfterUploadTasks.None;
        settings.DestinationInstanceId = McpSettingsWorkflowService.ResolveDestinationInstanceId(
            destination,
            forcedCategory ?? GuessUploaderCategory(filePathOrName));
        return settings;
    }

    private async Task<WorkerTask> RunTaskAsync(Func<Task> start, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_taskManager);

        var tcs = new TaskCompletionSource<WorkerTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        WorkerTask? expectedTask = null;
        EventHandler<WorkerTask>? startedHandler = null;
        startedHandler = (_, task) =>
        {
            _taskManager.TaskStarted -= startedHandler;
            expectedTask = task;
        };

        _taskManager.TaskStarted += startedHandler;

        EventHandler<WorkerTask>? handler = null;
        handler = (_, task) =>
        {
            if (expectedTask == null || task != expectedTask)
            {
                return;
            }

            _taskManager.TaskCompleted -= handler;
            tcs.TrySetResult(task);
        };

        _taskManager.TaskCompleted += handler;

        try
        {
            await start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await using var _ = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));
            return await tcs.Task;
        }
        catch
        {
            _taskManager.TaskStarted -= startedHandler;
            _taskManager.TaskCompleted -= handler;
            throw;
        }
    }

    private static async Task<SkiaSharp.SKBitmap> CaptureFullScreenBitmapAsync(int? monitor, TaskSettings settings)
    {
        var bitmap = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(CreateCaptureOptions(settings))
            ?? throw new InvalidOperationException("XerahS failed to capture the screen.");

        if (monitor == null)
        {
            return bitmap;
        }

        var screens = PlatformServices.Screen.GetAllScreens();
        if (monitor < 0 || monitor >= screens.Length)
        {
            bitmap.Dispose();
            throw new ArgumentOutOfRangeException(nameof(monitor), "The requested monitor index is out of range.");
        }

        var virtualBounds = PlatformServices.Screen.GetVirtualScreenBounds();
        var monitorBounds = screens[monitor.Value].Bounds;
        var crop = new SkiaSharp.SKRectI(
            monitorBounds.Left - virtualBounds.Left,
            monitorBounds.Top - virtualBounds.Top,
            monitorBounds.Right - virtualBounds.Left,
            monitorBounds.Bottom - virtualBounds.Top);

        var cropped = new SkiaSharp.SKBitmap(crop.Width, crop.Height);
        using var canvas = new SkiaSharp.SKCanvas(cropped);
        canvas.DrawBitmap(bitmap, crop, new SkiaSharp.SKRect(0, 0, crop.Width, crop.Height), SkiaSharp.SKSamplingOptions.Default);
        bitmap.Dispose();
        return cropped;
    }

    private static CaptureOptions CreateCaptureOptions(TaskSettings settings)
    {
        return new CaptureOptions
        {
            UseModernCapture = settings.CaptureSettings.UseModernCapture,
            LinuxRegionSelectorPreference = settings.CaptureSettings.LinuxRegionSelectorPreference,
            MacOSRegionSelectorPreference = settings.CaptureSettings.MacOSRegionSelectorPreference,
            MacOSPlayCaptureSound = settings.CaptureSettings.MacOSPlayCaptureSound,
            ShowCursor = settings.CaptureSettings.ShowCursor,
            CaptureTransparent = settings.CaptureSettings.CaptureTransparent,
            CaptureShadow = settings.CaptureSettings.CaptureShadow,
            CaptureClientArea = settings.CaptureSettings.CaptureClientArea,
            WorkflowId = settings.WorkflowId,
            WorkflowCategory = settings.Job.GetHotkeyCategory(),
            CaptureStartDelaySeconds = (double)settings.CaptureSettings.ScreenshotDelay
        };
    }

    private static TaskSettings ResolveWorkflowTaskSettings(string? workflowId)
    {
        if (!string.IsNullOrWhiteSpace(workflowId))
        {
            var workflow = SettingsManager.GetWorkflowById(workflowId)
                ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");
            return workflow.TaskSettings;
        }

        return SettingsManager.DefaultTaskSettings ?? new TaskSettings();
    }

    private static TaskSettings CloneTaskSettings(TaskSettings source)
    {
        var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        var json = JsonConvert.SerializeObject(source, jsonSettings);
        return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
    }

    private static string SaveImageToFile(SkiaSharp.SKBitmap bitmap, TaskSettings settings)
    {
        var path = XerahS.Core.TaskHelpers.SaveImageAsFile(bitmap, settings);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("XerahS did not produce a saved file path.");
        }

        return path;
    }

    private static void SaveBitmap(SkiaSharp.SKBitmap bitmap, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(GetEncodedFormat(outputPath), 100);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static SkiaSharp.SKEncodedImageFormat GetEncodedFormat(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg,
            ".bmp" => SkiaSharp.SKEncodedImageFormat.Bmp,
            ".gif" => SkiaSharp.SKEncodedImageFormat.Gif,
            ".webp" => SkiaSharp.SKEncodedImageFormat.Webp,
            _ => SkiaSharp.SKEncodedImageFormat.Png
        };
    }

    private static string GetAnnotatedOutputPath(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        var candidate = Path.Combine(directory, $"{name}_annotated{extension}");
        var suffix = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{name}_annotated_{suffix}{extension}");
            suffix++;
        }

        return candidate;
    }

    private static string? ResolveProcessName(IntPtr handle)
    {
        try
        {
            var processId = PlatformServices.Window.GetWindowProcessId(handle);
            if (processId == 0)
            {
                return null;
            }

            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static UploaderCategory GuessUploaderCategory(string filePathOrName)
    {
        if (FileHelpers.IsImageFile(filePathOrName))
        {
            return UploaderCategory.Image;
        }

        if (FileHelpers.IsTextFile(filePathOrName))
        {
            return UploaderCategory.Text;
        }

        return UploaderCategory.File;
    }

    private static JsonObject CreateUploadTaskResult(WorkerTask task, string fileName)
    {
        var url = task.Info.Metadata?.UploadURL;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(task.Error?.Message ?? "Upload failed.");
        }

        return new JsonObject
        {
            ["url"] = url,
            ["filename"] = fileName,
            ["size_bytes"] = task.Info.FilePath is { Length: > 0 } path && File.Exists(path) ? new FileInfo(path).Length : 0
        };
    }

    internal Task<JsonObject> CreateHistoryDetailsAsync(HistoryItem item, CancellationToken cancellationToken) =>
        _historyService.CreateDetailsAsync(item, cancellationToken);

    internal static string ResolveHistoryBlobPath(HistoryItem item) =>
        McpHistoryService.ResolveBlobPath(item);

    internal static string CreateHistoryBlobResourceUri(HistoryItem item) =>
        McpHistoryService.CreateBlobResourceUri(item);

    internal static JsonObject CreateHistoryBlobTooLargeResponse(string uri, string blobPath, long byteLength) =>
        McpHistoryService.CreateBlobTooLargeResponse(uri, blobPath, byteLength);

    internal static JsonObject CreateHistoryBlobMissingResponse(string uri, HistoryItem item) =>
        McpHistoryService.CreateBlobMissingResponse(uri, item);

    internal static string? CreateFileUrl(string? filePath) =>
        McpHistoryService.CreateFileUrl(filePath);

}

public sealed class McpUserCancelledException(string message) : OperationCanceledException(message);
