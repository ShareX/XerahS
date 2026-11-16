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

using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using XerahS.McpServer.Transport;
using Xunit;

namespace XerahS.McpServer.Tests;

public class McpHttpTransportTests
{
    private const string ApiKey = "test-api-key";
    private const string ToolsList = """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""";

    [Fact]
    public void UnwrapSseData_ReturnsTheMessageEventPayload()
    {
        const string sse = "event: message\ndata: {\"id\":1}\n\n";

        Assert.Equal("""{"id":1}""", HttpServer.UnwrapSseData(sse));
    }

    [Fact]
    public async Task Http_AppliesBearerAuthAndKeepsPlainJsonPostsWorking()
    {
        int port = GetFreePort();
        using var server = new HttpServer(new XerahSMcpServerTests.FakeRuntime(), port);
        using var cts = new CancellationTokenSource();
        Task run = server.StartAsync(cts.Token);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await WaitForHealthAsync(http);

            // /health is anonymous.
            var health = await http.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            // No key: 401 with a JSON-RPC error body, as before.
            var unauthorized = await http.PostAsync("/mcp/", Json(ToolsList));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            var error = JsonNode.Parse(await unauthorized.Content.ReadAsStringAsync())!;
            Assert.Equal("Invalid or missing API key", error["error"]?["message"]?.GetValue<string>());

            // Wrong key is rejected too.
            using var wrong = Authorized(ToolsList, "wrong-key");
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(wrong)).StatusCode);

            // Plain JSON client (no Accept, no initialize): a plain application/json body.
            using var plain = Authorized(ToolsList, ApiKey);
            var plainResponse = await http.SendAsync(plain);
            Assert.Equal(HttpStatusCode.OK, plainResponse.StatusCode);
            Assert.Equal("application/json", plainResponse.Content.Headers.ContentType?.MediaType);
            var tools = JsonNode.Parse(await plainResponse.Content.ReadAsStringAsync())!;
            Assert.Contains(tools["result"]!["tools"]!.AsArray(), tool => tool?["name"]?.GetValue<string>() == "capture_region");

            // SDK-style client that accepts SSE keeps the SDK's event framing.
            using var sdkStyle = Authorized(ToolsList, ApiKey);
            sdkStyle.Headers.Accept.ParseAdd("application/json");
            sdkStyle.Headers.Accept.ParseAdd("text/event-stream");
            var sdkResponse = await http.SendAsync(sdkStyle);
            Assert.Equal("text/event-stream", sdkResponse.Content.Headers.ContentType?.MediaType);
            Assert.Contains("data: ", await sdkResponse.Content.ReadAsStringAsync());

            // The heartbeat stream sits behind the same key.
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/mcp/events/")).StatusCode);
        }
        finally
        {
            cts.Cancel();
            try { await run; } catch (OperationCanceledException) { }
        }
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static HttpRequestMessage Authorized(string body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/") { Content = Json(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForHealthAsync(HttpClient http)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                if ((await http.GetAsync("/health")).IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The MCP HTTP server did not start.");
    }
}
