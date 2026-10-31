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

using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace XerahS.Assistant.Models;

public static class AssistantToolNames
{
    public const string HistorySearch = "history.search";
    public const string HistoryLatest = "history.latest";
    public const string ClipboardCopyText = "clipboard.copy_text";
    public const string FileReveal = "file.reveal";
    public const string EditorOpenImage = "editor.open_image";
    public const string OcrRun = "ocr.run";
    public const string UploadFile = "upload.file";
    public const string WorkflowRun = "workflow.run";
    public const string AnnotationAction = "annotation.action";
    public const string AliasSave = "alias.save";
}

public static class AssistantToolSchema
{
    public const string SchemaVersion = "1.0.0";

    public static readonly IReadOnlySet<string> AllowlistedTools = new HashSet<string>(StringComparer.Ordinal)
    {
        AssistantToolNames.HistorySearch,
        AssistantToolNames.HistoryLatest,
        AssistantToolNames.ClipboardCopyText,
        AssistantToolNames.FileReveal,
        AssistantToolNames.EditorOpenImage,
        AssistantToolNames.OcrRun,
        AssistantToolNames.UploadFile,
        AssistantToolNames.WorkflowRun,
        AssistantToolNames.AnnotationAction,
        AssistantToolNames.AliasSave
    };
}

public enum AssistantProviderProtocol
{
    OpenAiResponses,
    OpenAiCompatibleChat,
    GeminiGenerateContent,
    AnthropicMessages,
    OllamaGenerate
}

public sealed record AssistantProviderMetadata(
    string Id,
    string DisplayName,
    AssistantProviderProtocol Protocol,
    string DefaultModelId,
    string DefaultBaseUrl,
    bool SupportsTools,
    bool SupportsImageInput,
    IReadOnlyList<string> SuggestedModelIds);

public static class AssistantProviderCatalog
{
    private static readonly IReadOnlyList<AssistantProviderMetadata> Providers =
    [
        new(
            "openai",
            "OpenAI",
            AssistantProviderProtocol.OpenAiResponses,
            "gpt-5.4",
            "https://api.openai.com/v1",
            SupportsTools: true,
            SupportsImageInput: true,
            ["gpt-5.4", "gpt-5.4-mini", "gpt-4o-mini"]),
        new(
            "minimax",
            "MiniMax",
            AssistantProviderProtocol.OpenAiCompatibleChat,
            "MiniMax-M2.7",
            "https://api.minimax.io/v1",
            SupportsTools: true,
            SupportsImageInput: false,
            ["MiniMax-M2.7"]),
        new(
            "kimi",
            "Kimi",
            AssistantProviderProtocol.OpenAiCompatibleChat,
            "kimi-k2.5",
            "https://api.moonshot.ai/v1",
            SupportsTools: true,
            SupportsImageInput: true,
            ["kimi-k2.5"]),
        new(
            "gemini",
            "Gemini",
            AssistantProviderProtocol.GeminiGenerateContent,
            "gemini-3.1-flash",
            "https://generativelanguage.googleapis.com/v1beta",
            SupportsTools: true,
            SupportsImageInput: true,
            ["gemini-3.1-flash", "gemini-2.5-flash"]),
        new(
            "anthropic",
            "Anthropic",
            AssistantProviderProtocol.AnthropicMessages,
            "claude-sonnet-4-6",
            "https://api.anthropic.com/v1",
            SupportsTools: true,
            SupportsImageInput: true,
            ["claude-sonnet-4-6", "claude-3-5-sonnet-latest"]),
        new(
            "ollama",
            "Ollama",
            AssistantProviderProtocol.OllamaGenerate,
            "llama3.1",
            "http://127.0.0.1:11434",
            SupportsTools: false,
            SupportsImageInput: false,
            ["llama3.1", "llama3.2"])
    ];

    public static IReadOnlyList<AssistantProviderMetadata> GetProviders() => Providers;

    public static AssistantProviderMetadata? Find(string providerId) =>
        Providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));
}

public enum AssistantPrivacyScope
{
    MetadataOnly,
    LocalContent,
    CloudText,
    CloudImage,
    ExternalShare,
    Destructive
}

public enum AssistantModelMessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public sealed record AssistantImageAttachment(string MediaType, ReadOnlyMemory<byte> Data);

public sealed record AssistantMessage(
    AssistantModelMessageRole Role,
    string Text,
    string? ToolCallId = null,
    string? ToolName = null,
    IReadOnlyList<AssistantImageAttachment>? Images = null);

public sealed record AssistantToolDefinition(
    string Name,
    string Description,
    JsonObject Parameters);

public sealed record AssistantToolCall(
    string Id,
    string Name,
    JsonObject Arguments);

public sealed record AssistantModelRequest(
    string ProviderId,
    string ModelId,
    IReadOnlyList<AssistantMessage> Messages,
    IReadOnlyList<AssistantToolDefinition> Tools,
    AssistantPrivacyScope PrivacyScope,
    bool AllowImageContent,
    int MaxOutputTokens = 80);

public enum AssistantModelResultKind
{
    Text,
    ToolCalls,
    Error,
    Cancelled
}

public sealed record AssistantUsage(int? InputTokens, int? OutputTokens, decimal? EstimatedCostUsd);

public sealed record AssistantModelResult(
    AssistantModelResultKind Kind,
    string? Text,
    IReadOnlyList<AssistantToolCall> ToolCalls,
    AssistantUsage? Usage,
    string? ProviderRequestId);

public enum AssistantResponseKind
{
    Information,
    Results,
    ConfirmationRequired,
    Error
}

public enum AssistantActionKind
{
    CopyText,
    OpenFile,
    RevealFile,
    RunOcr,
    UploadFile,
    RunWorkflow,
    ConfigureProvider,
    SaveAlias
}

public sealed record AssistantAction(
    AssistantActionKind Kind,
    string Label,
    string? Text = null,
    string? FilePath = null,
    string? ToolName = null,
    bool RequiresConfirmation = false);

public sealed record AssistantResultItem(
    string Id,
    string Title,
    string? Subtitle,
    string? FilePath,
    DateTimeOffset? CapturedAt,
    bool Exists,
    IReadOnlyList<AssistantAction> Actions);

public sealed record AssistantPendingConfirmation(
    string ToolName,
    string Copy,
    AssistantAction Action);

public sealed record AssistantResponse(
    AssistantResponseKind Kind,
    string Message,
    IReadOnlyList<AssistantResultItem> Items,
    IReadOnlyList<AssistantAction> Actions,
    AssistantPendingConfirmation? PendingConfirmation = null)
{
    public static AssistantResponse Info(string message, params AssistantAction[] actions) =>
        new(AssistantResponseKind.Information, message, ReadOnlyCollection<AssistantResultItem>.Empty, actions);

    public static AssistantResponse Error(string message) =>
        new(AssistantResponseKind.Error, message, ReadOnlyCollection<AssistantResultItem>.Empty, ReadOnlyCollection<AssistantAction>.Empty);
}
