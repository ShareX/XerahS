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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XerahS.McpServer.Runtime;
using XerahS.McpServer.Server;
using XerahS.McpServer.Transport;

namespace XerahS.McpServer;

/// <summary>
/// Entry point for the dedicated XerahS MCP server executable.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var mcpMode = args.Contains("--mcp") || args.Contains("--mcp-server");
        var showHelp = args.Contains("--help") || args.Contains("-h");

        if (showHelp || !mcpMode)
        {
            PrintHelp();
            return showHelp ? 0 : 1;
        }

        var transportIndex = Array.IndexOf(args, "--transport");
        var transport = transportIndex >= 0 && transportIndex < args.Length - 1
            ? args[transportIndex + 1].ToLowerInvariant()
            : "stdio";

        var portIndex = Array.IndexOf(args, "--port");
        var port = portIndex >= 0 && portIndex < args.Length - 1
            && int.TryParse(args[portIndex + 1], out var parsedPort)
            ? parsedPort
            : 7890;

        return transport switch
        {
            "stdio" => await RunMcpStdioServerAsync(),
            "http" => await RunMcpHttpServerAsync(port),
            _ => throw new ArgumentException($"Unsupported transport '{transport}'. Expected 'stdio' or 'http'.")
        };
    }

    private static async Task<int> RunMcpStdioServerAsync()
    {
        try
        {
            var runtime = new XerahSMcpRuntime();

            // stdout carries only MCP JSON lines; host logging, if any, goes to stderr.
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            // The SDK logs every handler exception; XerahS reports those to the client as JSON-RPC errors instead.
            builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Critical);
            XerahSMcpHost.AddXerahSMcp(builder.Services, runtime).WithStdioServerTransport();

            using var host = builder.Build();
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await host.RunAsync(cts.Token);

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"MCP Server error: {ex}");
            return 1;
        }
    }

    private static async Task<int> RunMcpHttpServerAsync(int port)
    {
        try
        {
            var runtime = new XerahSMcpRuntime();
            using var httpServer = new HttpServer(runtime, port);
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Console.WriteLine($"XerahS MCP HTTP Server starting on port {port}...");
            Console.WriteLine($"  POST /mcp/       - MCP requests (JSON-RPC 2.0)");
            Console.WriteLine($"  GET  /mcp/events/ - SSE stream");
            Console.WriteLine($"  GET  /health     - Health check");
            Console.WriteLine("Press Ctrl+C to stop.");

            await httpServer.StartAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"MCP HTTP Server error: {ex}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
Usage: xerahs-mcp [options]

Options:
  --mcp, --mcp-server    Run in MCP server mode (stdio transport)
  --transport <type>     Transport type: stdio (default) or http
  --port <number>        HTTP server port (default: 7890, only with --transport http)
  --help                 Show this help message
""");
    }
}
