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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Server;

namespace XerahS.McpServer.Transport;

/// <summary>
/// HTTP host for the XerahS MCP server: the official SDK's Streamable HTTP transport (stateless)
/// mounted at <c>/mcp/</c>, behind the XerahS Bearer API key, plus the <c>/mcp/events/</c>
/// heartbeat stream and the anonymous <c>/health</c> probe.
/// </summary>
public sealed class HttpServer : IDisposable
{
    private const string McpPath = "/mcp";
    private const string EventsPath = "/mcp/events/";

    private readonly IXerahSMcpRuntime _runtime;
    private readonly int _port;
    private WebApplication? _app;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    public HttpServer(IXerahSMcpRuntime runtime, int port = 7890)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _port = port;
    }

    /// <summary>
    /// Start the HTTP server and run until cancelled.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(options => options.ListenAnyIP(_port));
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Critical);

        XerahSMcpHost.AddXerahSMcp(builder.Services, _runtime)
            .WithHttpTransport(options => options.Stateless = true);

        _app = builder.Build();

        // CORS for the MCP and SSE endpoints.
        _app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(McpPath))
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Append("Access-Control-Allow-Headers", "Authorization, Content-Type, Accept, Mcp-Session-Id, MCP-Protocol-Version");
                context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            }

            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await next();
        });

        // Everything under /mcp requires the Bearer API key; /health stays anonymous.
        _app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(McpPath) && !await ValidateAuthorizationAsync(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var error = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = null,
                    ["error"] = new JsonObject
                    {
                        ["code"] = XerahSMcpHost.ServerErrorCode,
                        ["message"] = "Invalid or missing API key"
                    }
                };
                await context.Response.WriteAsync(error.ToJsonString());
                return;
            }

            await next();
        });

        // Clients that POST plain JSON (no "text/event-stream" in Accept) get a plain JSON body,
        // as the pre-SDK server always returned. SDK-aware clients are not affected.
        _app.Use(PlainJsonResponseAsync);

        _app.MapMcp(McpPath);
        _app.MapGet(EventsPath, HandleSseAsync);
        _app.MapGet("/health", () => Results.Ok(new { status = "ok", server = "xerahs-mcp" }));

        await _app.RunAsync(_cts.Token);
    }

    /// <summary>
    /// Stop the HTTP server.
    /// </summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_app != null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }

    /// <summary>
    /// The SDK answers a POST with an SSE-framed <c>message</c> event. For a client that did not ask for
    /// SSE, unwrap the single event's data into an <c>application/json</c> response.
    /// </summary>
    internal static async Task PlainJsonResponseAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Method != HttpMethods.Post || !context.Request.Path.StartsWithSegments(McpPath))
        {
            await next(context);
            return;
        }

        // The SDK rejects a POST unless Accept lists both media types (406). Older XerahS clients sent neither
        // or only one, so complete the header; only a client that did not ask for SSE gets its reply unwrapped.
        bool acceptsSse = context.Request.Headers.Accept.Any(value => value?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true);
        bool acceptsJson = context.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
        if (!acceptsSse || !acceptsJson)
        {
            context.Request.Headers.Accept = "application/json, text/event-stream";
        }

        if (acceptsSse)
        {
            await next(context);
            return;
        }

        Stream original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        string body = Encoding.UTF8.GetString(buffer.ToArray());
        if (context.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            body = UnwrapSseData(body);
            context.Response.ContentType = "application/json";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentLength = bytes.Length;
        if (bytes.Length > 0)
        {
            await original.WriteAsync(bytes, context.RequestAborted);
        }
    }

    /// <summary>
    /// Returns the data of the last event in an SSE body (the JSON-RPC response follows any notifications the
    /// server sent first). Multiple <c>data:</c> lines inside one event are joined with a newline.
    /// </summary>
    internal static string UnwrapSseData(string sseBody)
    {
        string lastEventData = string.Empty;
        var data = new StringBuilder();
        bool hasData = false;

        void FlushEvent()
        {
            if (hasData)
            {
                lastEventData = data.ToString();
            }

            data.Clear();
            hasData = false;
        }

        foreach (var rawLine in sseBody.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                FlushEvent();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (hasData)
                {
                    data.Append('\n');
                }

                data.Append(line.AsSpan(line.Length > 5 && line[5] == ' ' ? 6 : 5));
                hasData = true;
            }
        }

        FlushEvent();
        return lastEventData;
    }

    /// <summary>
    /// Handle the SSE GET request: an initialized notification followed by heartbeats.
    /// </summary>
    private static async Task HandleSseAsync(HttpContext context)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        await using var stream = new SseStream(context.Response.Body, context.RequestAborted);

        // Send heartbeat every 30 seconds to keep connection alive
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        await stream.SendInitializedAsync();

        try
        {
            while (!context.RequestAborted.IsCancellationRequested)
            {
                await heartbeatTimer.WaitForNextTickAsync(context.RequestAborted);
                await stream.SendHeartbeatAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }

    /// <summary>
    /// Validate the Authorization Bearer token.
    /// </summary>
    private async Task<bool> ValidateAuthorizationAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            return false;
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var configuredApiKey = await _runtime.GetApiKeyAsync(context.RequestAborted);
        return !string.IsNullOrWhiteSpace(configuredApiKey) &&
               CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(configuredApiKey));
    }

    /// <summary>
    /// Generate a new API key (32 characters).
    /// </summary>
    public static string GenerateApiKey()
    {
        var bytes = new byte[24];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)[..32];
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _cts?.Cancel();
        _cts?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
