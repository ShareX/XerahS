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

using System.IO;
using System.Text;
using System.Text.Json;
using XerahS.McpServer.Server;

namespace XerahS.McpServer.Transport;

/// <summary>
/// SSE stream writer for MCP server-to-client notifications
/// </summary>
public class SseStream : IAsyncDisposable, IDisposable
{
    private readonly Stream _outputStream;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CancellationToken _cancellationToken;
    private bool _isDisposed;

    public SseStream(Stream outputStream, CancellationToken cancellationToken = default)
    {
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        _cancellationToken = cancellationToken;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Send an MCP protocol notification
    /// </summary>
    public async Task SendNotificationAsync(string method, object? result = null)
    {
        var notification = new
        {
            jsonrpc = "2.0",
            method,
            @params = result
        };
        var json = JsonSerializer.Serialize(notification, _jsonOptions);
        await SendEventAsync("notification", json);
    }

    /// <summary>
    /// Send a progress update for long-running operations (e.g., capture_scrolling)
    /// </summary>
    public async Task SendProgressAsync(string method, ProgressNotification progress)
    {
        var json = JsonSerializer.Serialize(progress, _jsonOptions);
        await SendEventAsync("progress", json);
    }

    /// <summary>
    /// Send initialized notification after handshake
    /// </summary>
    public async Task SendInitializedAsync()
    {
        await SendNotificationAsync("notifications/initialized");
    }

    /// <summary>
    /// Send a tool list changed notification
    /// </summary>
    public async Task SendToolsListChangedAsync()
    {
        await SendNotificationAsync("notifications/tools/listChanged");
    }

    /// <summary>
    /// Send a resources list changed notification
    /// </summary>
    public async Task SendResourcesListChangedAsync()
    {
        await SendNotificationAsync("notifications/resources/listChanged");
    }

    /// <summary>
    /// Send a prompts list changed notification
    /// </summary>
    public async Task SendPromptsListChangedAsync()
    {
        await SendNotificationAsync("notifications/prompts/listChanged");
    }

    private async Task SendEventAsync(string eventType, string data)
    {
        if (_isDisposed)
            return;

        var sb = new StringBuilder();
        sb.Append($"event: {eventType}\n");
        foreach (var line in data.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            sb.Append("data: ");
            sb.Append(line);
            sb.Append('\n');
        }
        sb.Append('\n');

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await _outputStream.WriteAsync(bytes, _cancellationToken);
        await _outputStream.FlushAsync(_cancellationToken);
    }

    /// <summary>
    /// Send a comment line to keep the connection alive (heartbeat)
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        if (_isDisposed)
            return;

        var bytes = Encoding.UTF8.GetBytes(": heartbeat\n\n");
        await _outputStream.WriteAsync(bytes, _cancellationToken);
        await _outputStream.FlushAsync(_cancellationToken);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Progress notification for long-running operations
/// </summary>
public class ProgressNotification
{
    public string Method { get; set; } = string.Empty;
    public string? ProgressToken { get; set; }
    public long Current { get; set; }
    public long Total { get; set; }
    public string? Message { get; set; }
}
