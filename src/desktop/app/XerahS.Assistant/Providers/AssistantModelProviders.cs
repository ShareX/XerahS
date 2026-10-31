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

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using XerahS.Common;
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;

namespace XerahS.Assistant.Providers;

public interface IAssistantModelProvider
{
    AssistantProviderMetadata Metadata { get; }
    Task<AssistantModelResult> CompleteAsync(AssistantModelRequest request, CancellationToken cancellationToken);
    Task<AssistantModelResult> ValidateAsync(string modelId, CancellationToken cancellationToken);
}

public static class AssistantModelProviderFactory
{
    public static IAssistantModelProvider Create(AssistantProviderRuntimeSettings settings) => new HttpAssistantModelProvider(settings);
}

internal sealed class HttpAssistantModelProvider : IAssistantModelProvider
{
    private readonly AssistantProviderRuntimeSettings _settings;
    private readonly HttpClient? _client;

    public HttpAssistantModelProvider(AssistantProviderRuntimeSettings settings, HttpClient? client = null)
    {
        _settings = settings;
        _client = client;
    }

    public AssistantProviderMetadata Metadata => _settings.Metadata;

    public Task<AssistantModelResult> ValidateAsync(string modelId, CancellationToken cancellationToken)
    {
        AssistantModelRequest request = new(
            _settings.Metadata.Id,
            modelId,
            [
                new AssistantMessage(AssistantModelMessageRole.System, "Reply with ok."),
                new AssistantMessage(AssistantModelMessageRole.User, "ok")
            ],
            [],
            AssistantPrivacyScope.CloudText,
            AllowImageContent: false);

        return CompleteAsync(request, cancellationToken);
    }

    public async Task<AssistantModelResult> CompleteAsync(AssistantModelRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage message = BuildRequest(request);
            HttpClient client = _client ?? HttpClientFactory.Create(allowAutoRedirect: false, infiniteTimeout: true);
            using HttpResponseMessage response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var limited = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (limited.Length + read > 2_000_000) return Error("Provider response exceeded the analysis limit.");
                limited.Write(buffer, 0, read);
            }
            string responseText = Encoding.UTF8.GetString(limited.ToArray());

            if (!response.IsSuccessStatusCode)
            {
                return Error(request.AllowImageContent ? $"Image analysis provider returned {(int)response.StatusCode}." : $"Provider returned {(int)response.StatusCode}: {TrimForStatus(responseText)}");
            }

            if (request.AllowImageContent && !IsCompleteResponse(responseText))
                return Error("Image analysis was incomplete.");
            string? text = ExtractText(responseText);
            return string.IsNullOrWhiteSpace(text)
                ? Error("Provider returned an empty response.")
                : new AssistantModelResult(AssistantModelResultKind.Text, text.Trim(), [], null, TryGetRequestId(response));
        }
        catch (OperationCanceledException)
        {
            return new AssistantModelResult(AssistantModelResultKind.Cancelled, null, [], null, null);
        }
        catch (Exception ex)
        {
            if (!request.AllowImageContent) DebugHelper.WriteException(ex, $"Assistant provider request failed: {_settings.Metadata.Id}");
            return Error("Provider request failed.");
        }
    }

    internal HttpRequestMessage BuildRequest(AssistantModelRequest request)
    {
        var images = request.Messages.SelectMany(message => message.Images ?? []).ToList();
        if (images.Count > 0 && (!request.AllowImageContent || !_settings.Metadata.SupportsImageInput ||
            images.Count > 16 || images.Any(image => image.MediaType != "image/png" || image.Data.Length == 0 || image.Data.Length > 20_000_000)))
            throw new InvalidOperationException("Image input is unavailable or invalid.");
        return _settings.Metadata.Protocol switch
        {
            AssistantProviderProtocol.OpenAiResponses => BuildOpenAiResponsesRequest(request),
            AssistantProviderProtocol.OpenAiCompatibleChat => BuildOpenAiCompatibleChatRequest(request),
            AssistantProviderProtocol.GeminiGenerateContent => BuildGeminiRequest(request),
            AssistantProviderProtocol.AnthropicMessages => BuildAnthropicRequest(request),
            AssistantProviderProtocol.OllamaGenerate => BuildOllamaRequest(request),
            _ => throw new NotSupportedException($"Unsupported assistant provider: {_settings.Metadata.Protocol}")
        };
    }

    private HttpRequestMessage BuildOpenAiResponsesRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            input = request.AllowImageContent
                ? (object)request.Messages.Select(message => new { role = message.Role.ToString().ToLowerInvariant(), content = ImageParts(message, "responses") })
                : string.Join("\n\n", request.Messages.Select(message => $"{message.Role}: {message.Text}")),
            max_output_tokens = Math.Clamp(request.MaxOutputTokens, 80, 8192),
            store = false
        };

        return BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/responses", payload, bearerToken: _settings.ApiKey);
    }

    private HttpRequestMessage BuildOpenAiCompatibleChatRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            messages = request.Messages.Select(message => new
            {
                role = message.Role.ToString().ToLowerInvariant(),
                content = request.AllowImageContent ? (object)ImageParts(message, "chat") : message.Text
            }),
            max_tokens = Math.Clamp(request.MaxOutputTokens, 80, 8192),
            temperature = 0
        };

        return BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/chat/completions", payload, bearerToken: _settings.ApiKey);
    }

    private HttpRequestMessage BuildGeminiRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            contents = request.Messages
                .Where(message => message.Role != AssistantModelMessageRole.System)
                .Select(message => new
                {
                    role = message.Role == AssistantModelMessageRole.User ? "user" : "model",
                    parts = ImageParts(message, "gemini")
                }),
            systemInstruction = new
            {
                parts = request.Messages
                    .Where(message => message.Role == AssistantModelMessageRole.System)
                    .Select(message => new { text = message.Text })
            },
            generationConfig = new
            {
                maxOutputTokens = Math.Clamp(request.MaxOutputTokens, 80, 8192),
                temperature = 0
            }
        };

        string url = $"{_settings.BaseUrl}/models/{Uri.EscapeDataString(request.ModelId)}:generateContent?key={Uri.EscapeDataString(_settings.ApiKey ?? string.Empty)}";
        return BuildJsonRequest(HttpMethod.Post, url, payload);
    }

    private HttpRequestMessage BuildAnthropicRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            max_tokens = Math.Clamp(request.MaxOutputTokens, 80, 8192),
            temperature = 0,
            system = string.Join("\n", request.Messages.Where(message => message.Role == AssistantModelMessageRole.System).Select(message => message.Text)),
            messages = request.Messages
                .Where(message => message.Role != AssistantModelMessageRole.System)
                .Select(message => new
                {
                    role = message.Role == AssistantModelMessageRole.User ? "user" : "assistant",
                    content = request.AllowImageContent ? (object)ImageParts(message, "anthropic") : message.Text
                })
        };

        HttpRequestMessage message = BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/messages", payload);
        message.Headers.Add("x-api-key", _settings.ApiKey ?? string.Empty);
        message.Headers.Add("anthropic-version", "2023-06-01");
        return message;
    }

    private HttpRequestMessage BuildOllamaRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            prompt = string.Join("\n\n", request.Messages.Select(message => $"{message.Role}: {message.Text}")),
            stream = false,
            options = new
            {
                temperature = 0
            }
        };

        return BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/api/generate", payload);
    }

    private static object[] ImageParts(AssistantMessage message, string protocol)
    {
        var parts = new List<object>();
        parts.Add(protocol switch
        {
            "responses" => new { type = "input_text", text = message.Text },
            "gemini" => (object)new { text = message.Text },
            _ => new { type = "text", text = message.Text }
        });
        foreach (var image in message.Images ?? [])
        {
            string data = Convert.ToBase64String(image.Data.Span);
            parts.Add(protocol switch
            {
                "responses" => new { type = "input_image", image_url = $"data:{image.MediaType};base64,{data}", detail = "high" },
                "chat" => (object)new { type = "image_url", image_url = new { url = $"data:{image.MediaType};base64,{data}", detail = "high" } },
                "gemini" => new { inlineData = new { mimeType = image.MediaType, data } },
                _ => (object)new { type = "image", source = new { type = "base64", media_type = image.MediaType, data } }
            });
        }
        return parts.ToArray();
    }

    private bool IsCompleteResponse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        return _settings.Metadata.Protocol switch
        {
            AssistantProviderProtocol.OpenAiResponses => root.TryGetProperty("status", out var status) && status.GetString() == "completed",
            AssistantProviderProtocol.OpenAiCompatibleChat => root.GetProperty("choices").EnumerateArray().All(choice =>
                choice.TryGetProperty("finish_reason", out var reason) && reason.GetString() == "stop"),
            AssistantProviderProtocol.GeminiGenerateContent => root.GetProperty("candidates").EnumerateArray().All(candidate =>
                candidate.TryGetProperty("finishReason", out var reason) && reason.GetString() == "STOP"),
            AssistantProviderProtocol.AnthropicMessages => root.TryGetProperty("stop_reason", out var reason) && reason.GetString() == "end_turn",
            _ => false
        };
    }

    private static HttpRequestMessage BuildJsonRequest(HttpMethod method, string url, object payload, string? bearerToken = null)
    {
        var message = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return message;
    }

    private string? ExtractText(string responseText)
    {
        using JsonDocument document = JsonDocument.Parse(responseText);
        JsonElement root = document.RootElement;

        return _settings.Metadata.Protocol switch
        {
            AssistantProviderProtocol.OpenAiResponses => TryGetString(root, "output_text") ?? ExtractOpenAiOutputText(root),
            AssistantProviderProtocol.OpenAiCompatibleChat => ExtractPath(root, "choices", 0, "message", "content"),
            AssistantProviderProtocol.GeminiGenerateContent => ExtractPath(root, "candidates", 0, "content", "parts", 0, "text"),
            AssistantProviderProtocol.AnthropicMessages => ExtractPath(root, "content", 0, "text"),
            AssistantProviderProtocol.OllamaGenerate => TryGetString(root, "response"),
            _ => null
        };
    }

    private static string? ExtractOpenAiOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement contentItem in content.EnumerateArray())
            {
                string? text = TryGetString(contentItem, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? ExtractPath(JsonElement root, params object[] segments)
    {
        JsonElement current = root;
        foreach (object segment in segments)
        {
            if (segment is string property)
            {
                if (!current.TryGetProperty(property, out current))
                {
                    return null;
                }
            }
            else if (segment is int index)
            {
                if (current.ValueKind != JsonValueKind.Array || current.GetArrayLength() <= index)
                {
                    return null;
                }

                current = current[index];
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string TrimForStatus(string value)
    {
        value = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return value.Length <= 160 ? value : value[..157] + "...";
    }

    private static string? TryGetRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private static AssistantModelResult Error(string message) =>
        new(AssistantModelResultKind.Error, message, [], null, null);
}
