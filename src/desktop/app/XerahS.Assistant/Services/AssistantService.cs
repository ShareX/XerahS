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

using SkiaSharp;
using System.Text.Json;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;
using XerahS.Assistant.Routing;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using XerahS.Platform.Abstractions;
using CoreOcrOptions = XerahS.Core.OCROptions;

namespace XerahS.Assistant.Services;

public interface IAssistantService
{
    Task<AssistantResponse> ProcessPromptAsync(string prompt, CancellationToken cancellationToken);
    Task<AssistantResponse> ExecuteActionAsync(AssistantAction action, bool confirmed, CancellationToken cancellationToken);
}

public sealed class AssistantService : IAssistantService
{
    private readonly AssistantCommandRouter _router;
    private readonly IAssistantHistoryService _history;
    private readonly AssistantPrivacyGuard _privacyGuard;
    private readonly IDesktopTaskManager? _taskManager;
    private readonly AssistantLocalMemoryStore _memoryStore;
    private readonly Func<AssistantProviderRuntimeSettings?> _activeProviderResolver;
    private readonly Func<AssistantProviderRuntimeSettings, IAssistantModelProvider> _providerFactory;

    public AssistantService()
        : this(new AssistantCommandRouter(), new AssistantHistoryService(), new AssistantPrivacyGuard(), null, null, null, null)
    {
    }

    public AssistantService(IDesktopTaskManager taskManager)
        : this(new AssistantCommandRouter(), new AssistantHistoryService(), new AssistantPrivacyGuard(), taskManager, null, null, null)
    {
    }

    public AssistantService(
        AssistantCommandRouter router,
        IAssistantHistoryService history,
        AssistantPrivacyGuard privacyGuard,
        IDesktopTaskManager? taskManager = null,
        AssistantLocalMemoryStore? memoryStore = null,
        Func<AssistantProviderRuntimeSettings?>? activeProviderResolver = null,
        Func<AssistantProviderRuntimeSettings, IAssistantModelProvider>? providerFactory = null)
    {
        _router = router;
        _history = history;
        _privacyGuard = privacyGuard;
        _taskManager = taskManager;
        _memoryStore = memoryStore ?? new AssistantLocalMemoryStore();
        _activeProviderResolver = activeProviderResolver ?? ResolveActiveProvider;
        _providerFactory = providerFactory ?? AssistantModelProviderFactory.Create;
    }

    public async Task<AssistantResponse> ProcessPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        DebugHelper.WriteLine($"[Assistant] ProcessPromptAsync start. Prompt='{prompt}'.");

        if (_memoryStore.TryParseAliasDefinition(prompt, out AssistantAliasDefinition aliasDefinition))
        {
            DebugHelper.WriteLine($"[Assistant] Saving alias '{aliasDefinition.Alias}'.");
            _memoryStore.SaveAlias(aliasDefinition);
            return AssistantResponse.Info($"Saved assistant alias: {aliasDefinition.Alias}");
        }

        if (_memoryStore.TryParseAliasDeletion(prompt, out string aliasToDelete))
        {
            DebugHelper.WriteLine($"[Assistant] Deleting alias '{aliasToDelete}'.");
            return _memoryStore.DeleteAlias(aliasToDelete)
                ? AssistantResponse.Info($"Deleted assistant alias: {aliasToDelete}")
                : AssistantResponse.Info($"No saved assistant alias found: {aliasToDelete}");
        }

        if (_memoryStore.TryResolveAlias(prompt, out string aliasCommand))
        {
            DebugHelper.WriteLine($"[Assistant] Resolved alias prompt to '{aliasCommand}'.");
            prompt = aliasCommand;
        }

        AssistantResponse? providerResponse = await TryProcessWithProviderAsync(prompt, cancellationToken);
        if (providerResponse != null)
        {
            DebugHelper.WriteLine($"[Assistant] Provider path returned kind={providerResponse.Kind}, items={providerResponse.Items.Count}, actions={providerResponse.Actions.Count}, pendingConfirmation={providerResponse.PendingConfirmation != null}.");
            _memoryStore.RecordExecution(new AssistantDeterministicIntent(AssistantDeterministicIntentKind.Unknown, 0, CopyRequested: false), BuildActionSummary(providerResponse));
            return providerResponse;
        }

        var intent = _router.Parse(prompt);
        DebugHelper.WriteLine($"[Assistant] Fallback router parsed kind={intent.Kind}, limit={intent.Limit}, copyRequested={intent.CopyRequested}, separator='{intent.Separator ?? "<null>"}', argument='{intent.Argument ?? "<null>"}'.");
        if (intent.Kind == AssistantDeterministicIntentKind.Unknown)
        {
            AssistantResponse noMatchResponse = AssistantResponse.Info("The assistant could not map that request to a safe local command. Try one of the suggestions.");
            DebugHelper.WriteLine("[Assistant] Returning no-match response from fallback router.");
            _memoryStore.RecordExecution(intent, BuildActionSummary(noMatchResponse));
            return noMatchResponse;
        }

        AssistantResponse response = await ProcessIntentAsync(intent, cancellationToken);
        DebugHelper.WriteLine($"[Assistant] Fallback path returned kind={response.Kind}, items={response.Items.Count}, actions={response.Actions.Count}, pendingConfirmation={response.PendingConfirmation != null}.");
        _memoryStore.RecordExecution(intent, BuildActionSummary(response));
        return response;
    }

    private async Task<AssistantResponse> ProcessIntentAsync(
        AssistantDeterministicIntent intent,
        CancellationToken cancellationToken)
    {
        return intent.Kind switch
        {
            AssistantDeterministicIntentKind.LatestScreenshotPaths => await GetLatestScreenshotPathsAsync(intent, cancellationToken),
            AssistantDeterministicIntentKind.CopyLatestScreenshotPath => await CopyLatestScreenshotPathAsync(cancellationToken),
            AssistantDeterministicIntentKind.OpenLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.OpenFile, cancellationToken),
            AssistantDeterministicIntentKind.RevealLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.RevealFile, cancellationToken),
            AssistantDeterministicIntentKind.OcrLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.RunOcr, cancellationToken),
            AssistantDeterministicIntentKind.CopyOcrLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.RunOcr, cancellationToken, "copy"),
            AssistantDeterministicIntentKind.UploadLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.UploadFile, cancellationToken),
            AssistantDeterministicIntentKind.SearchScreenshotText => await SearchScreenshotTextAsync(intent, cancellationToken),
            AssistantDeterministicIntentKind.RunWorkflow => await PrepareWorkflowRunAsync(intent.Argument, cancellationToken),
            _ => AssistantResponse.Info(
                "AI provider not configured. XerahS Assistant can only run local commands without a provider.",
                new AssistantAction(AssistantActionKind.ConfigureProvider, "Configure AI Provider"))
        };
    }

    private async Task<AssistantResponse?> TryProcessWithProviderAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        AssistantProviderRuntimeSettings? providerSettings = _activeProviderResolver();
        if (providerSettings == null)
        {
            DebugHelper.WriteLine("[Assistant] No active provider configured. Skipping provider path.");
            return null;
        }

        DebugHelper.WriteLine($"[Assistant] Trying provider path with provider='{providerSettings.Metadata.Id}', model='{providerSettings.ModelId}'.");

        AssistantPrivacyDecision decision = _privacyGuard.Evaluate(new AssistantPrivacyCheck(
            AssistantToolNames.HistorySearch,
            AssistantPrivacyScope.CloudText,
            Text: prompt,
            UserExplicitlyRequested: true));

        if (!decision.Allowed || decision.RequiresConfirmation)
        {
            DebugHelper.WriteLine($"[Assistant] Provider path blocked by privacy guard. Allowed={decision.Allowed}, RequiresConfirmation={decision.RequiresConfirmation}.");
            return null;
        }

        AssistantModelRequest request = new(
            providerSettings.Metadata.Id,
            providerSettings.ModelId,
            [
                new AssistantMessage(
                    AssistantModelMessageRole.System,
                    """
                    You are the local intent planner for XerahS Assistant.
                    Convert the user's request into exactly one JSON object with no surrounding prose.
                    Allowed intents: latest_screenshot_paths, copy_latest_screenshot_path, open_latest_screenshot,
                    reveal_latest_screenshot, ocr_latest_screenshot, copy_ocr_latest_screenshot,
                    upload_latest_screenshot, search_screenshot_text, run_workflow, no_match.
                    JSON schema:
                    {
                      "intent": string,
                      "limit": number or null,
                      "copyRequested": boolean or null,
                      "separator": string or null,
                      "argument": string or null
                    }
                    Rules:
                    - Use latest_screenshot_paths for requests asking for one or more screenshot file paths.
                    - Use search_screenshot_text for requests to find screenshots by visible words. Put the search text in argument.
                    - Preserve an explicit separator like ";", ",", "|", "newline", or "tab" in the separator field.
                    - For run_workflow, place the workflow name in argument.
                    - Return no_match when the request does not cleanly map to one safe local action.
                    """),
                new AssistantMessage(
                    AssistantModelMessageRole.User,
                    prompt)
            ],
            [],
            AssistantPrivacyScope.CloudText,
            AllowImageContent: false);

        IAssistantModelProvider provider = _providerFactory(providerSettings);
        AssistantModelResult result = await provider.CompleteAsync(request, cancellationToken);
        DebugHelper.WriteLine($"[Assistant] Provider returned result kind={result.Kind}, hasText={!string.IsNullOrWhiteSpace(result.Text)}, toolCalls={result.ToolCalls.Count}.");
        if (result.Kind == AssistantModelResultKind.Cancelled)
        {
            return AssistantResponse.Error("Provider request cancelled.");
        }

        if (result.Kind == AssistantModelResultKind.Error || string.IsNullOrWhiteSpace(result.Text))
        {
            return AssistantResponse.Error(result.Text ?? "Provider request failed.");
        }

        AssistantDeterministicIntent? providerIntent = TryParseProviderIntent(result.Text);
        if (providerIntent != null)
        {
            DebugHelper.WriteLine($"[Assistant] Provider JSON parsed to kind={providerIntent.Kind}, limit={providerIntent.Limit}, copyRequested={providerIntent.CopyRequested}, separator='{providerIntent.Separator ?? "<null>"}', argument='{providerIntent.Argument ?? "<null>"}'.");
            return providerIntent.Kind == AssistantDeterministicIntentKind.Unknown
                ? null
                : await ProcessIntentAsync(providerIntent, cancellationToken);
        }

        AssistantDeterministicIntent inferredIntent = _router.Parse(result.Text);
        DebugHelper.WriteLine($"[Assistant] Provider text fallback parsed to kind={inferredIntent.Kind}, limit={inferredIntent.Limit}, copyRequested={inferredIntent.CopyRequested}, separator='{inferredIntent.Separator ?? "<null>"}'.");
        if (inferredIntent.Kind == AssistantDeterministicIntentKind.Unknown)
        {
            return null;
        }

        return await ProcessIntentAsync(inferredIntent, cancellationToken);
    }

    public async Task<AssistantResponse> ExecuteActionAsync(AssistantAction action, bool confirmed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AssistantPrivacyDecision decision = EvaluateAction(action);
        if (!decision.Allowed)
        {
            return AssistantResponse.Error(decision.Reason ?? "Action blocked.");
        }

        if (decision.RequiresConfirmation && !confirmed)
        {
            return new AssistantResponse(
                AssistantResponseKind.ConfirmationRequired,
                decision.ConfirmationCopy ?? "Confirmation required.",
                [],
                [],
                new AssistantPendingConfirmation(action.ToolName ?? string.Empty, decision.ConfirmationCopy ?? "Confirmation required.", action));
        }

        try
        {
            switch (action.Kind)
            {
                case AssistantActionKind.CopyText:
                    return await CopyTextToClipboardOrErrorAsync(action.Text ?? string.Empty, "Copied to clipboard.");

                case AssistantActionKind.RevealFile:
                    return PlatformServices.System.ShowFileInExplorer(action.FilePath ?? string.Empty)
                        ? AssistantResponse.Info("Opened file location.")
                        : AssistantResponse.Error("File no longer available. It may have been moved or deleted.");

                case AssistantActionKind.OpenFile:
                    return await OpenImageInEditorAsync(action.FilePath, cancellationToken);

                case AssistantActionKind.RunOcr:
                    return await RunOcrAsync(action, cancellationToken);

                case AssistantActionKind.UploadFile:
                    return await UploadFileAsync(action.FilePath, cancellationToken);

                case AssistantActionKind.RunWorkflow:
                    return await RunWorkflowAsync(action.Text, cancellationToken);

                default:
                    return AssistantResponse.Error("Unsupported assistant action.");
            }
        }
        catch (OperationCanceledException)
        {
            return AssistantResponse.Error("Action cancelled. You can retry if needed.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Assistant action failed: {action.Kind}");
            return AssistantResponse.Error("Assistant action failed.");
        }
    }

    private async Task<AssistantResponse> GetLatestScreenshotPathsAsync(
        AssistantDeterministicIntent intent,
        CancellationToken cancellationToken)
    {
        var items = await _history.GetLatestScreenshotsAsync(intent.Limit, cancellationToken);
        DebugHelper.WriteLine($"[Assistant] GetLatestScreenshotPathsAsync fetched {items.Count} item(s) for limit={intent.Limit}.");
        if (items.Count == 0)
        {
            return AssistantResponse.Error("No recent captures found. Try taking a screenshot first.");
        }

        string separator = string.IsNullOrEmpty(intent.Separator) ? Environment.NewLine : intent.Separator;
        string paths = string.Join(separator, items.Select(item => item.FilePath));
        DebugHelper.WriteLine($"[Assistant] Built paths payload length={paths.Length} using separator='{(separator == Environment.NewLine ? "\\n" : separator)}'.");
        if (intent.CopyRequested)
        {
            AssistantResponse? clipboardResponse = await TryCopyTextToClipboardAsync(paths);
            if (clipboardResponse != null)
            {
                DebugHelper.WriteLine($"[Assistant] Clipboard copy for paths failed: {clipboardResponse.Message}");
                return clipboardResponse with
                {
                    Items = items.Select(ToResultItem).ToList(),
                    Actions = [new AssistantAction(AssistantActionKind.CopyText, "Copy paths", paths, ToolName: AssistantToolNames.ClipboardCopyText)]
                };
            }

            DebugHelper.WriteLine("[Assistant] Copied paths payload to clipboard.");
        }

        string message = intent.CopyRequested
            ? $"Copied {items.Count} path(s) to clipboard."
            : items.Count == 1
                ? "Latest screenshot path."
                : $"Last {items.Count} screenshot paths.";

        return new AssistantResponse(
            AssistantResponseKind.Results,
            message,
            items.Select(ToResultItem).ToList(),
            [new AssistantAction(AssistantActionKind.CopyText, "Copy paths", paths, ToolName: AssistantToolNames.ClipboardCopyText)]);
    }

    private async Task<AssistantResponse> SearchScreenshotTextAsync(
        AssistantDeterministicIntent intent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.Argument))
        {
            return AssistantResponse.Error("Enter text to search for in screenshots.");
        }

        IReadOnlyList<AssistantHistoryItem> items = await _history.SearchScreenshotsAsync(intent.Argument, intent.Limit, cancellationToken);
        if (items.Count == 0)
        {
            return AssistantResponse.Error($"No screenshots found containing \"{intent.Argument}\".");
        }

        string paths = string.Join(Environment.NewLine, items.Select(item => item.FilePath));
        if (intent.CopyRequested)
        {
            AssistantResponse? clipboardResponse = await TryCopyTextToClipboardAsync(paths);
            if (clipboardResponse != null)
            {
                return clipboardResponse with
                {
                    Items = items.Select(ToResultItem).ToList(),
                    Actions = [new AssistantAction(AssistantActionKind.CopyText, "Copy paths", paths, ToolName: AssistantToolNames.ClipboardCopyText)]
                };
            }
        }

        return new AssistantResponse(
            AssistantResponseKind.Results,
            intent.CopyRequested
                ? $"Copied {items.Count} matching screenshot path(s)."
                : $"Found {items.Count} matching screenshot(s).",
            items.Select(ToResultItem).ToList(),
            [new AssistantAction(AssistantActionKind.CopyText, "Copy paths", paths, ToolName: AssistantToolNames.ClipboardCopyText)]);
    }

    private async Task<AssistantResponse> CopyLatestScreenshotPathAsync(CancellationToken cancellationToken)
    {
        var items = await _history.GetLatestScreenshotsAsync(1, cancellationToken);
        if (items.Count == 0)
        {
            return AssistantResponse.Error("No recent captures found. Try taking a screenshot first.");
        }

        string path = items[0].FilePath;
        AssistantResponse? clipboardResponse = await TryCopyTextToClipboardAsync(path);
        if (clipboardResponse != null)
        {
            return clipboardResponse with
            {
                Kind = AssistantResponseKind.Results,
                Items = [ToResultItem(items[0])],
                Actions = [new AssistantAction(AssistantActionKind.CopyText, "Copy path", path, ToolName: AssistantToolNames.ClipboardCopyText)]
            };
        }

        return new AssistantResponse(
            AssistantResponseKind.Results,
            "Copied the latest screenshot path to clipboard.",
            [ToResultItem(items[0])],
            [new AssistantAction(AssistantActionKind.CopyText, "Copy path", path, ToolName: AssistantToolNames.ClipboardCopyText)]);
    }

    private async Task<AssistantResponse> ExecuteLatestFileActionAsync(
        AssistantActionKind actionKind,
        CancellationToken cancellationToken,
        string? actionText = null)
    {
        var items = await _history.GetLatestScreenshotsAsync(1, cancellationToken);
        if (items.Count == 0)
        {
            return AssistantResponse.Error("No recent captures found. Try taking a screenshot first.");
        }

        AssistantHistoryItem item = items[0];
        AssistantAction action = actionKind switch
        {
            AssistantActionKind.OpenFile => new AssistantAction(
                AssistantActionKind.OpenFile,
                "Open",
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.EditorOpenImage,
                RequiresConfirmation: true),
            AssistantActionKind.RunOcr => new AssistantAction(
                AssistantActionKind.RunOcr,
                string.Equals(actionText, "copy", StringComparison.OrdinalIgnoreCase) ? "Copy OCR text" : "Run OCR",
                Text: actionText,
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.OcrRun,
                RequiresConfirmation: true),
            AssistantActionKind.UploadFile => new AssistantAction(
                AssistantActionKind.UploadFile,
                "Upload",
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.UploadFile,
                RequiresConfirmation: true),
            _ => new AssistantAction(
                AssistantActionKind.RevealFile,
                "Reveal",
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.FileReveal,
                RequiresConfirmation: true)
        };

        return await ExecuteActionAsync(action, confirmed: false, cancellationToken);
    }

    private async Task<AssistantResponse> OpenImageInEditorAsync(string? filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        using SKBitmap? bitmap = SKBitmap.Decode(filePath);
        if (bitmap == null)
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        SKBitmap? editorBitmap = bitmap.Copy();
        if (editorBitmap == null)
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        SKBitmap? edited = await PlatformServices.UI.ShowEditorAsync(editorBitmap, filePath);
        edited?.Dispose();

        return AssistantResponse.Info("Opened latest screenshot in the editor.");
    }

    private Task<AssistantResponse> PrepareWorkflowRunAsync(string? workflowName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WorkflowSettings? workflow = ResolveWorkflow(workflowName);
        if (workflow == null)
        {
            return Task.FromResult(AssistantResponse.Error("Configured workflow not found."));
        }

        string displayName = GetWorkflowDisplayName(workflow);
        var action = new AssistantAction(
            AssistantActionKind.RunWorkflow,
            $"Run {displayName}",
            Text: workflow.Id,
            ToolName: AssistantToolNames.WorkflowRun,
            RequiresConfirmation: true);

        return ExecuteActionAsync(action, confirmed: false, cancellationToken);
    }

    private async Task<AssistantResponse> RunOcrAsync(AssistantAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(action.FilePath))
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        string? cachedText = await _history.GetCachedOcrTextAsync(action.FilePath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedText))
        {
            return await BuildOcrTextResponseAsync(action, cachedText);
        }

        if (PlatformServices.Ocr == null || !PlatformServices.Ocr.IsSupported)
        {
            return AssistantResponse.Error(PlatformServices.Ocr?.UnavailableReason ?? "OCR is not available on this platform.");
        }

        if (!File.Exists(action.FilePath))
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        using SKBitmap? bitmap = SKBitmap.Decode(action.FilePath);
        if (bitmap == null)
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        OcrResult result;
        try
        {
            result = await PlatformServices.Ocr.RecognizeAsync(bitmap, CreateAssistantOcrOptions());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return AssistantResponse.Error("OCR failed: " + ex.Message);
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            return AssistantResponse.Error(result.ErrorMessage ?? "OCR did not find text in the latest screenshot.");
        }

        await _history.CacheOcrTextAsync(action.FilePath, result.Text, cancellationToken);
        return await BuildOcrTextResponseAsync(action, result.Text);
    }

    private static async Task<AssistantResponse> BuildOcrTextResponseAsync(AssistantAction action, string text)
    {
        var copyAction = new AssistantAction(AssistantActionKind.CopyText, "Copy OCR text", text, ToolName: AssistantToolNames.ClipboardCopyText);

        if (string.Equals(action.Text, "copy", StringComparison.OrdinalIgnoreCase))
        {
            AssistantResponse? clipboardResponse = await TryCopyTextToClipboardAsync(text);
            return clipboardResponse ?? AssistantResponse.Info("Copied OCR text to clipboard.", copyAction);
        }

        return AssistantResponse.Info(text, copyAction);
    }

    private static OcrOptions CreateAssistantOcrOptions()
    {
        CoreOcrOptions? configuredOptions = SettingsManager.DefaultTaskSettings?.CaptureSettings?.OCROptions;

        return new OcrOptions
        {
            Language = NormalizeOcrLanguage(configuredOptions?.Language),
            ScaleFactor = NormalizeOcrScaleFactor(configuredOptions?.ScaleFactor),
            SingleLine = configuredOptions?.SingleLine ?? false
        };
    }

    private static string NormalizeOcrLanguage(string? language)
    {
        string? trimmedLanguage = language?.Trim();
        return string.IsNullOrEmpty(trimmedLanguage) ? "en" : trimmedLanguage;
    }

    private static float NormalizeOcrScaleFactor(float? scaleFactor)
    {
        return scaleFactor.HasValue && float.IsFinite(scaleFactor.Value)
            ? Math.Max(scaleFactor.Value, 1f)
            : 1f;
    }

    private static async Task<AssistantResponse> CopyTextToClipboardOrErrorAsync(string text, string successMessage)
    {
        AssistantResponse? clipboardResponse = await TryCopyTextToClipboardAsync(text);
        return clipboardResponse ?? AssistantResponse.Info(successMessage);
    }

    private static async Task<AssistantResponse?> TryCopyTextToClipboardAsync(string text)
    {
        try
        {
            await PlatformServices.Clipboard.SetTextAsync(text);
            return null;
        }
        catch (InvalidOperationException)
        {
            return new AssistantResponse(
                AssistantResponseKind.Error,
                "Clipboard is not available right now.",
                [],
                [new AssistantAction(AssistantActionKind.CopyText, "Copy text", text, ToolName: AssistantToolNames.ClipboardCopyText)]);
        }
    }

    private async Task<AssistantResponse> UploadFileAsync(string? filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        if (_taskManager == null)
        {
            return AssistantResponse.Error("Upload services are not ready yet.");
        }

        TaskSettings settings = GetUploadTaskSettings();
        WorkerTask? task = null;
        void OnTaskStarted(object? sender, WorkerTask startedTask)
        {
            if (string.Equals(startedTask.Info.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                task = startedTask;
            }
        }

        _taskManager.TaskStarted += OnTaskStarted;
        try
        {
            await _taskManager.StartFileTask(settings, filePath);
        }
        finally
        {
            _taskManager.TaskStarted -= OnTaskStarted;
        }

        string? url = task?.Info.Metadata.UploadURL ?? task?.Info.Result?.URL;
        if (!string.IsNullOrWhiteSpace(url))
        {
            return AssistantResponse.Info(
                $"Uploaded latest screenshot: {url}",
                new AssistantAction(AssistantActionKind.CopyText, "Copy URL", url, ToolName: AssistantToolNames.ClipboardCopyText));
        }

        return task?.Error != null
            ? AssistantResponse.Error($"Upload failed: {task.Error.Message}")
            : AssistantResponse.Error(task?.Info.Result?.Response ?? "Upload finished without a URL.");
    }

    private async Task<AssistantResponse> RunWorkflowAsync(string? workflowId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_taskManager == null)
        {
            return AssistantResponse.Error("Workflow services are not ready yet.");
        }

        WorkflowSettings? workflow = ResolveWorkflow(workflowId);
        if (workflow?.TaskSettings == null)
        {
            return AssistantResponse.Error("Configured workflow not found.");
        }

        TaskSettings settings = WatchFolderManager.CloneTaskSettings(workflow.TaskSettings);
        settings.WorkflowId = workflow.Id;
        await _taskManager.StartTask(settings);
        return AssistantResponse.Info($"Workflow finished: {GetWorkflowDisplayName(workflow)}");
    }

    private AssistantResultItem ToResultItem(AssistantHistoryItem item)
    {
        return new AssistantResultItem(
            item.Id,
            item.FileName,
            item.Exists ? item.FilePath : $"{item.FilePath} (unavailable)",
            item.FilePath,
            item.CapturedAt,
            item.Exists,
            [
                new AssistantAction(AssistantActionKind.CopyText, "Copy path", item.FilePath, ToolName: AssistantToolNames.ClipboardCopyText),
                new AssistantAction(AssistantActionKind.OpenFile, "Open", FilePath: item.FilePath, ToolName: AssistantToolNames.EditorOpenImage, RequiresConfirmation: true),
                new AssistantAction(AssistantActionKind.RevealFile, "Reveal", FilePath: item.FilePath, ToolName: AssistantToolNames.FileReveal, RequiresConfirmation: true),
                new AssistantAction(AssistantActionKind.RunOcr, "OCR", FilePath: item.FilePath, ToolName: AssistantToolNames.OcrRun, RequiresConfirmation: true),
                new AssistantAction(AssistantActionKind.UploadFile, "Upload", FilePath: item.FilePath, ToolName: AssistantToolNames.UploadFile, RequiresConfirmation: true)
            ]);
    }

    private AssistantPrivacyDecision EvaluateAction(AssistantAction action)
    {
        string toolName = action.ToolName ?? action.Kind switch
        {
            AssistantActionKind.CopyText => AssistantToolNames.ClipboardCopyText,
            AssistantActionKind.OpenFile => AssistantToolNames.EditorOpenImage,
            AssistantActionKind.RevealFile => AssistantToolNames.FileReveal,
            AssistantActionKind.RunOcr => AssistantToolNames.OcrRun,
            AssistantActionKind.UploadFile => AssistantToolNames.UploadFile,
            AssistantActionKind.RunWorkflow => AssistantToolNames.WorkflowRun,
            _ => string.Empty
        };

        AssistantPrivacyScope scope = action.Kind == AssistantActionKind.UploadFile
            ? AssistantPrivacyScope.ExternalShare
            : AssistantPrivacyScope.LocalContent;

        bool isKnown = string.IsNullOrWhiteSpace(action.FilePath) || _history.IsKnownHistoryFile(action.FilePath);
        return _privacyGuard.Evaluate(new AssistantPrivacyCheck(
            toolName,
            scope,
            Text: action.Text,
            FilePath: action.FilePath,
            IsKnownHistoryItem: isKnown,
            UserExplicitlyRequested: true));
    }

    private WorkflowSettings? ResolveWorkflow(string? workflowNameOrId)
    {
        if (string.IsNullOrWhiteSpace(workflowNameOrId))
        {
            return null;
        }

        string normalized = NormalizeWorkflowName(workflowNameOrId);
        return SettingsManager.WorkflowsConfig.Hotkeys
            .Where(workflow => workflow.Enabled && workflow.Job != WorkflowType.None)
            .FirstOrDefault(workflow =>
                string.Equals(workflow.Id, workflowNameOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeWorkflowName(workflow.Name), normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeWorkflowName(workflow.TaskSettings?.Description), normalized, StringComparison.OrdinalIgnoreCase) ||
                NormalizeWorkflowName(EnumExtensions.GetDescription(workflow.Job)).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetWorkflowDisplayName(WorkflowSettings workflow) =>
        !string.IsNullOrWhiteSpace(workflow.Name)
            ? workflow.Name
            : EnumExtensions.GetDescription(workflow.Job);

    private static string NormalizeWorkflowName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string BuildActionSummary(AssistantResponse response)
    {
        if (response.PendingConfirmation != null)
        {
            return $"confirmation:{response.PendingConfirmation.ToolName}";
        }

        if (response.Actions.Count > 0)
        {
            return string.Join(",", response.Actions.Select(action => action.Kind.ToString()));
        }

        return response.Kind.ToString();
    }

    private static TaskSettings GetUploadTaskSettings()
    {
        var uploadWorkflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
        TaskSettings settings = uploadWorkflow?.TaskSettings != null
            ? WatchFolderManager.CloneTaskSettings(uploadWorkflow.TaskSettings)
            : WatchFolderManager.CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());

        settings.Job = WorkflowType.FileUpload;
        settings.AfterUploadJob |= AfterUploadTasks.CopyURLToClipboard;
        return settings;
    }

    private static AssistantProviderRuntimeSettings? ResolveActiveProvider() =>
        AssistantProviderSettingsResolver.TryGetActive(out AssistantProviderRuntimeSettings settings) ? settings : null;

    private static AssistantDeterministicIntent? TryParseProviderIntent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("intent", out JsonElement intentElement))
            {
                return null;
            }

            string? intentName = intentElement.GetString();
            if (string.IsNullOrWhiteSpace(intentName))
            {
                return null;
            }

            int limit = root.TryGetProperty("limit", out JsonElement limitElement) && limitElement.ValueKind == JsonValueKind.Number && limitElement.TryGetInt32(out int parsedLimit)
                ? Math.Clamp(parsedLimit, 1, AssistantCommandRouter.MaxLatestScreenshotLimit)
                : 1;

            bool copyRequested = root.TryGetProperty("copyRequested", out JsonElement copyElement) && copyElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? copyElement.GetBoolean()
                : false;

            string? separator = root.TryGetProperty("separator", out JsonElement separatorElement) && separatorElement.ValueKind == JsonValueKind.String
                ? NormalizeProviderSeparator(separatorElement.GetString())
                : null;

            string? argument = root.TryGetProperty("argument", out JsonElement argumentElement) && argumentElement.ValueKind == JsonValueKind.String
                ? argumentElement.GetString()
                : null;

            return intentName switch
            {
                "latest_screenshot_paths" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.LatestScreenshotPaths, limit, copyRequested, separator),
                "copy_latest_screenshot_path" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.CopyLatestScreenshotPath, 1, true),
                "open_latest_screenshot" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.OpenLatestScreenshot, 1, false),
                "reveal_latest_screenshot" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.RevealLatestScreenshot, 1, false),
                "ocr_latest_screenshot" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.OcrLatestScreenshot, 1, false),
                "copy_ocr_latest_screenshot" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.CopyOcrLatestScreenshot, 1, true),
                "upload_latest_screenshot" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.UploadLatestScreenshot, 1, false),
                "search_screenshot_text" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.SearchScreenshotText, limit, copyRequested, Argument: argument),
                "run_workflow" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.RunWorkflow, 1, false, Argument: argument),
                "no_match" => new AssistantDeterministicIntent(AssistantDeterministicIntentKind.Unknown, 0, false),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeProviderSeparator(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "newline" => Environment.NewLine,
            "linebreak" => Environment.NewLine,
            "line-break" => Environment.NewLine,
            "tab" => "\t",
            _ => value
        };
    }
}
