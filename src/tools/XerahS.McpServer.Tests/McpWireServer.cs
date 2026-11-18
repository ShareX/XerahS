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

using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Server;

namespace XerahS.McpServer.Tests;

/// <summary>JSON-RPC request as the tests write it; serialized onto the wire by <see cref="McpWireServer"/>.</summary>
public sealed class JsonRpcRequest
{
    public string JsonRpc { get; set; } = "2.0";
    public object? Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public object? Params { get; set; }
}

public sealed class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>JSON-RPC response read back from the wire.</summary>
public sealed class JsonRpcResponse
{
    public JsonNode? Result { get; set; }
    public JsonRpcError? Error { get; set; }
}

public static class JsonRpcErrorCodes
{
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int ServerError = XerahSMcpHost.ServerErrorCode;
    public const int UserCancelled = XerahSMcpHost.UserCancelledErrorCode;
}

/// <summary>
/// Runs the real XerahS MCP server (official SDK, stream transport) over in-memory pipes and exchanges raw
/// JSON-RPC lines with it, so tests assert on exactly what a client would see on the wire.
/// </summary>
public sealed class McpWireServer : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Pipe _toServer = new();
    private readonly Pipe _fromServer = new();
    private IHost? _host;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private bool _initialized;

    public McpWireServer(IXerahSMcpRuntime runtime)
    {
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public IXerahSMcpRuntime Runtime { get; }

    public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync();
            if (request.Method != "initialize")
            {
                await EnsureInitializedAsync();
            }
            else
            {
                _initialized = true;
            }

            return await ExchangeAsync(request, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureStartedAsync()
    {
        if (_host is not null)
        {
            return;
        }

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        XerahSMcpHost.AddXerahSMcp(builder.Services, Runtime)
            .WithStreamServerTransport(_toServer.Reader.AsStream(), _fromServer.Writer.AsStream());
        _host = builder.Build();
        await _host.StartAsync();

        _writer = new StreamWriter(_toServer.Writer.AsStream()) { AutoFlush = true };
        _reader = new StreamReader(_fromServer.Reader.AsStream());
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await ExchangeAsync(new JsonRpcRequest
        {
            Id = 0,
            Method = "initialize",
            Params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "xerahs-tests", version = "1.0" }
            }
        }, CancellationToken.None);
        await _writer!.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
    }

    private async Task<JsonRpcResponse> ExchangeAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = request.JsonRpc,
            ["id"] = JsonSerializer.SerializeToNode(request.Id),
            ["method"] = request.Method,
            ["params"] = request.Params switch
            {
                null => null,
                JsonNode node => node.DeepClone(),
                var other => JsonSerializer.SerializeToNode(other)
            }
        };
        await _writer!.WriteLineAsync(message.ToJsonString().AsMemory(), cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            string? line = await _reader!.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                throw new InvalidOperationException("The MCP server closed the stream before answering.");
            }

            var node = JsonNode.Parse(line)!.AsObject();
            if (node["id"] is null || node["id"]!.ToJsonString() != JsonSerializer.Serialize(request.Id))
            {
                continue; // server-initiated notification or request
            }

            var response = new JsonRpcResponse { Result = node["result"] };
            if (node["error"] is JsonObject error)
            {
                response.Error = new JsonRpcError
                {
                    Code = error["code"]!.GetValue<int>(),
                    Message = error["message"]?.GetValue<string>() ?? string.Empty
                };
            }

            return response;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            _writer?.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }

        _gate.Dispose();
    }
}
