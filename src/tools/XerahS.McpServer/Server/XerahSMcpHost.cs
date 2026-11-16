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

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using XerahS.McpServer.Prompts;
using XerahS.McpServer.Resources;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Tools;

namespace XerahS.McpServer.Server;

/// <summary>
/// XerahS tool, resource and prompt surface exposed through the official MCP C# SDK.
/// The SDK owns the protocol (JSON-RPC framing, initialize handshake, transports);
/// this class only maps SDK requests onto the XerahS runtime.
/// </summary>
public sealed class XerahSMcpHost
{
    /// <summary>MCP error code returned when the user dismisses an interactive capture.</summary>
    public const int UserCancelledErrorCode = -32500;

    /// <summary>Generic server-side failure (JSON-RPC implementation-defined server error).</summary>
    public const int ServerErrorCode = -32000;

    private readonly CaptureTools _captureTools;
    private readonly AnnotationTools _annotationTools;
    private readonly UploadTools _uploadTools;
    private readonly HistoryTools _historyTools;
    private readonly SettingsTools _settingsTools;
    private readonly HistoryResourceProvider _historyResourceProvider = new();
    private readonly SettingsResourceProvider _settingsResourceProvider = new();
    private readonly WorkflowResourceProvider _workflowResourceProvider = new();

    public XerahSMcpHost(IXerahSMcpRuntime runtime)
    {
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _captureTools = new CaptureTools(Runtime);
        _annotationTools = new AnnotationTools(Runtime);
        _uploadTools = new UploadTools(Runtime);
        _historyTools = new HistoryTools(Runtime);
        _settingsTools = new SettingsTools(Runtime);
    }

    public IXerahSMcpRuntime Runtime { get; }

    /// <summary>
    /// Registers the XerahS MCP server (identity plus tool, resource and prompt handlers) on the service collection.
    /// Add a transport to the returned builder (stdio, HTTP or a stream pair).
    /// </summary>
    public static IMcpServerBuilder AddXerahSMcp(IServiceCollection services, IXerahSMcpRuntime runtime)
    {
        var host = new XerahSMcpHost(runtime);
        return services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = Capabilities.ServerName, Version = runtime.ServerVersion };
            })
            .WithListToolsHandler((_, _) => ValueTask.FromResult(host.ListTools()))
            .WithCallToolHandler((context, cancellationToken) =>
                host.CallToolAsync(context.Params?.Name, context.Params?.Arguments, cancellationToken))
            .WithListResourcesHandler((_, _) => ValueTask.FromResult(host.ListResources()))
            .WithListResourceTemplatesHandler((_, _) => ValueTask.FromResult(host.ListResourceTemplates()))
            .WithReadResourceHandler((context, cancellationToken) =>
                host.ReadResourceAsync(context.Params?.Uri, cancellationToken))
            .WithListPromptsHandler((_, _) => ValueTask.FromResult(host.ListPrompts()))
            .WithGetPromptHandler((context, _) => host.GetPrompt(context.Params?.Name, context.Params?.Arguments));
    }

    public ListToolsResult ListTools()
    {
        var tools = new List<Tool>();
        foreach (var json in _captureTools.GetToolDefinitionsJson()
                     .Concat(_annotationTools.GetToolDefinitionsJson())
                     .Concat(_uploadTools.GetToolDefinitionsJson())
                     .Concat(_historyTools.GetToolDefinitionsJson())
                     .Concat(_settingsTools.GetToolDefinitionsJson()))
        {
            tools.Add(JsonSerializer.Deserialize<Tool>(json, McpJsonUtilities.DefaultOptions)!);
        }

        return new ListToolsResult { Tools = tools };
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        string? name,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Missing tool name");
            }

            var result = await ExecuteToolAsync(name, ToJsonObject(arguments), cancellationToken).ConfigureAwait(false);
            return ToCallToolResult(result);
        }
        catch (Exception ex) when (ShouldMap(ex, cancellationToken))
        {
            throw ToProtocolException(ex);
        }
    }

    public ListResourcesResult ListResources()
    {
        var resources = new List<Resource>();
        foreach (var node in EnumerateResourceDefinitions())
        {
            if (!IsTemplate(node))
            {
                resources.Add(node.Deserialize<Resource>(McpJsonUtilities.DefaultOptions)!);
            }
        }

        return new ListResourcesResult { Resources = resources };
    }

    public ListResourceTemplatesResult ListResourceTemplates()
    {
        var templates = new List<ResourceTemplate>();
        foreach (var node in EnumerateResourceDefinitions())
        {
            if (IsTemplate(node))
            {
                templates.Add(new ResourceTemplate
                {
                    UriTemplate = node["uri"]!.GetValue<string>(),
                    Name = node["name"]?.GetValue<string>() ?? string.Empty,
                    Description = node["description"]?.GetValue<string>(),
                    MimeType = node["mimeType"]?.GetValue<string>()
                });
            }
        }

        return new ListResourceTemplatesResult { ResourceTemplates = templates };
    }

    public async ValueTask<ReadResourceResult> ReadResourceAsync(string? uri, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                throw new ArgumentException("Missing resource URI");
            }

            JsonObject result = await Runtime.ReadResourceAsync(uri, cancellationToken).ConfigureAwait(false);
            return result.Deserialize<ReadResourceResult>(McpJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("The resource reader returned no content.");
        }
        catch (Exception ex) when (ShouldMap(ex, cancellationToken))
        {
            throw ToProtocolException(ex);
        }
    }

    public ListPromptsResult ListPrompts()
    {
        return new ListPromptsResult
        {
            Prompts = PromptTemplates.GetPrompts()
                .Select(prompt => new Prompt
                {
                    Name = prompt.Name,
                    Description = prompt.Description,
                    Arguments = prompt.Arguments
                        .Select(argument => new PromptArgument
                        {
                            Name = argument.Name,
                            Description = argument.Description,
                            Required = argument.Required
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    public ValueTask<GetPromptResult> GetPrompt(string? name, IDictionary<string, JsonElement>? arguments)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Missing prompt name");
            }

            var text = PromptTemplates.GetPromptTemplate(name);
            if (arguments is not null)
            {
                foreach (var pair in arguments)
                {
                    var replacement = pair.Value.ValueKind == JsonValueKind.String
                        ? pair.Value.GetString() ?? string.Empty
                        : pair.Value.GetRawText();
                    text = text.Replace($"{{{{{pair.Key}}}}}", replacement, StringComparison.Ordinal);
                }
            }

            return ValueTask.FromResult(new GetPromptResult
            {
                Description = PromptTemplates.GetPrompts().FirstOrDefault(prompt => prompt.Name == name)?.Description,
                Messages =
                [
                    new PromptMessage
                    {
                        Role = Role.User,
                        Content = new TextContentBlock { Text = text }
                    }
                ]
            });
        }
        catch (Exception ex) when (ShouldMap(ex, CancellationToken.None))
        {
            throw ToProtocolException(ex);
        }
    }

    /// <summary>
    /// Maps a domain exception onto the JSON-RPC error the XerahS server has always reported.
    /// The SDK only forwards the message and code of an <see cref="McpProtocolException"/>; any other
    /// exception would be reduced to a generic error.
    /// </summary>
    public static McpProtocolException ToProtocolException(Exception ex)
    {
        return ex switch
        {
            McpProtocolException protocol => protocol,
            McpUserCancelledException => new McpProtocolException(ex.Message, ex, (McpErrorCode)UserCancelledErrorCode),
            ArgumentException or FileNotFoundException => new McpProtocolException(ex.Message, ex, McpErrorCode.InvalidParams),
            InvalidOperationException => new McpProtocolException(ex.Message, ex, (McpErrorCode)ServerErrorCode),
            _ => new McpProtocolException(ex.Message, ex, McpErrorCode.InternalError)
        };
    }

    private static bool ShouldMap(Exception ex, CancellationToken cancellationToken)
    {
        // A genuine cancellation (the client abandoned the request) must keep propagating; the user
        // dismissing a capture overlay (McpUserCancelledException) is an ordinary protocol error.
        return ex is not OperationCanceledException || ex is McpUserCancelledException || !cancellationToken.IsCancellationRequested;
    }

    private IEnumerable<JsonObject> EnumerateResourceDefinitions()
    {
        foreach (var json in _historyResourceProvider.GetResourceTemplatesJson()
                     .Concat(_settingsResourceProvider.GetResourceTemplatesJson())
                     .Concat(_workflowResourceProvider.GetResourceTemplatesJson()))
        {
            yield return JsonNode.Parse(json)!.AsObject();
        }
    }

    private static bool IsTemplate(JsonObject definition) =>
        definition["uri"]?.GetValue<string>()?.Contains('{') == true;

    private static JsonObject? ToJsonObject(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        var result = new JsonObject();
        foreach (var pair in arguments)
        {
            result[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
        }

        return result;
    }

    private static CallToolResult ToCallToolResult(JsonObject result)
    {
        // Runtime tools return bare result objects. MCP clients expect a content array, so the
        // object is sent both as a JSON text block and as structuredContent.
        if (result["content"] is JsonArray)
        {
            return result.Deserialize<CallToolResult>(McpJsonUtilities.DefaultOptions)!;
        }

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.ToJsonString() }],
            StructuredContent = JsonSerializer.SerializeToElement(result)
        };
    }

    private async Task<JsonObject> ExecuteToolAsync(string name, JsonObject? arguments, CancellationToken cancellationToken)
    {
        string? query = arguments?["query"]?.GetValue<string>();
        string? fromDate = arguments?["from_date"]?.GetValue<string>();
        string? toDate = arguments?["to_date"]?.GetValue<string>();
        string fileType = arguments?["file_type"]?.GetValue<string>() ?? "all";
        int limit = arguments?["limit"]?.GetValue<int>() ?? 20;

        return name switch
        {
            "capture_region" => await _captureTools.CaptureRegionAsync(
                arguments?["workflow_id"]?.GetValue<string>(),
                arguments?["monitor"]?.GetValue<int>(),
                cancellationToken),
            "capture_window" => await _captureTools.CaptureWindowAsync(
                arguments?["window_title"]?.GetValue<string>(),
                arguments?["include_decoration"]?.GetValue<bool>() ?? true,
                cancellationToken),
            "capture_full_screen" => await _captureTools.CaptureFullScreenAsync(
                arguments?["monitor"]?.GetValue<int>(),
                cancellationToken),
            "capture_scrolling" => await _captureTools.CaptureScrollingAsync(
                arguments?["scroll_direction"]?.GetValue<string>() ?? "down",
                arguments?["max_frames"]?.GetValue<int>() ?? 50,
                cancellationToken),
            "annotate_image" => await _annotationTools.AnnotateImageAsync(
                arguments?["image_path"]?.GetValue<string>(),
                arguments?["annotations"] as JsonArray,
                arguments?["auto_save"]?.GetValue<bool>() ?? true,
                cancellationToken),
            "upload_file" => await _uploadTools.UploadFileAsync(
                arguments?["file_path"]?.GetValue<string>(),
                arguments?["destination"]?.GetValue<string>(),
                cancellationToken),
            "upload_clipboard" => await _uploadTools.UploadClipboardAsync(
                arguments?["destination"]?.GetValue<string>(),
                cancellationToken),
            "query_history" => await _historyTools.QueryHistoryAsync(query, fromDate, toDate, fileType, limit, cancellationToken),
            "get_history_item" => await _historyTools.GetHistoryItemAsync(
                arguments?["id"]?.GetValue<string>(),
                cancellationToken),
            "list_workflows" => await _settingsTools.ListWorkflowsAsync(cancellationToken),
            "get_settings" => await _settingsTools.GetSettingsAsync(
                arguments?["category"]?.GetValue<string>(),
                cancellationToken),
            _ => throw new ArgumentException($"Unknown tool: {name}")
        };
    }
}
