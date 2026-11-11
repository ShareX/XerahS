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
using SkiaSharp;
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;

namespace XerahS.Assistant.Ai;

/// <summary>
/// The AI provider the user set up under Settings, as a feature needs to know it.
/// </summary>
/// <param name="Identity">Changes when the provider, model, address or key changes.</param>
public sealed record AiConnection(string ProviderName, string ModelId, string BaseUrl, bool SupportsImages, string Identity)
{
    public string DisplayName => $"{ProviderName} / {ModelId}";
}

/// <summary>
/// One question for the model. <see cref="Instructions"/> say what to do and how to answer; <see cref="Input"/> is the
/// material to work on. Text and images that come from a screenshot or a file belong in the input, never in the
/// instructions, so the model treats them as data.
/// </summary>
public sealed record AiPrompt(string Instructions, string Input)
{
    public IReadOnlyList<SKBitmap> Images { get; init; } = [];

    /// <summary>The provider clamps this to 80 to 8192.</summary>
    public int MaxOutputTokens { get; init; } = 1024;

    /// <summary>
    /// Shrink an image that is too large to send. Turn off when the answer refers to exact pixel sizes.
    /// </summary>
    public bool ShrinkImagesToFit { get; init; } = true;
}

public enum AiReplyStatus
{
    Ok,
    /// <summary>No provider is set up, or it has no API key.</summary>
    NotConfigured,
    /// <summary>The prompt has images and the provider's model cannot read them.</summary>
    ImagesNotSupported,
    Cancelled,
    Failed
}

public sealed record AiReply(AiReplyStatus Status, string? Text = null, string? Error = null)
{
    public bool Success => Status == AiReplyStatus.Ok && Text != null;

    /// <summary>What to show the user when the reply is not a success.</summary>
    public string ErrorMessage => Status switch
    {
        AiReplyStatus.Ok => string.Empty,
        AiReplyStatus.NotConfigured => "Set up an AI provider and its API key in Settings first.",
        AiReplyStatus.ImagesNotSupported => "The AI provider's model cannot read images. Choose a provider that can.",
        AiReplyStatus.Cancelled => "Cancelled.",
        _ => string.IsNullOrWhiteSpace(Error) ? "The AI request failed." : Error
    };

    /// <summary>
    /// Reads the reply as JSON. Models often wrap JSON in a code fence or a sentence; the object or array inside is
    /// what gets read.
    /// </summary>
    public bool TryReadJson(out JsonDocument? document)
    {
        document = null;
        string? json = Success ? AiJson.Extract(Text!) : null;

        if (json == null)
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// The one way XerahS features talk to the user's AI provider. A feature builds an <see cref="AiPrompt"/> and reads
/// an <see cref="AiReply"/>; the provider, its protocol, the API key, image encoding and error handling are here.
/// </summary>
public interface IAiClient
{
    /// <summary>The provider in use, or null when none is set up. Read it each time: the user can change it.</summary>
    AiConnection? Connection { get; }

    Task<AiReply> AskAsync(AiPrompt prompt, CancellationToken cancellationToken = default);
}

public sealed class AiClient : IAiClient
{
    private readonly Func<AssistantProviderRuntimeSettings?> _settings;
    private readonly Func<AssistantProviderRuntimeSettings, IAssistantModelProvider> _providers;

    public AiClient(
        Func<AssistantProviderRuntimeSettings?>? settings = null,
        Func<AssistantProviderRuntimeSettings, IAssistantModelProvider>? providers = null)
    {
        _settings = settings ?? (() => AssistantProviderSettingsResolver.TryGetActive(out AssistantProviderRuntimeSettings active) ? active : null);
        _providers = providers ?? AssistantModelProviderFactory.Create;
    }

    public AiConnection? Connection => ReadSettings() is { } settings ? Describe(settings) : null;

    public async Task<AiReply> AskAsync(AiPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        AssistantProviderRuntimeSettings? settings = ReadSettings();

        if (settings == null)
        {
            return new AiReply(AiReplyStatus.NotConfigured);
        }

        bool hasImages = prompt.Images.Count > 0;

        if (hasImages && !settings.Metadata.SupportsImageInput)
        {
            return new AiReply(AiReplyStatus.ImagesNotSupported);
        }

        try
        {
            List<AssistantImageAttachment> images = [];

            foreach (SKBitmap image in prompt.Images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[]? png = await Task.Run(() => AiImage.Encode(image, prompt.ShrinkImagesToFit), cancellationToken).ConfigureAwait(false);

                if (png == null)
                {
                    return new AiReply(AiReplyStatus.Failed, Error: "The image is too large to send to the AI provider.");
                }

                images.Add(new AssistantImageAttachment("image/png", png));
            }

            AssistantModelRequest request = new(
                settings.Metadata.Id,
                settings.ModelId,
                [
                    new AssistantMessage(AssistantModelMessageRole.System, prompt.Instructions),
                    new AssistantMessage(AssistantModelMessageRole.User, prompt.Input, Images: hasImages ? images : null)
                ],
                [],
                hasImages ? AssistantPrivacyScope.CloudImage : AssistantPrivacyScope.CloudText,
                AllowImageContent: hasImages,
                prompt.MaxOutputTokens);

            AssistantModelResult result = await _providers(settings).CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            return result.Kind switch
            {
                AssistantModelResultKind.Text when !string.IsNullOrWhiteSpace(result.Text) => new AiReply(AiReplyStatus.Ok, result.Text),
                AssistantModelResultKind.Cancelled => new AiReply(AiReplyStatus.Cancelled),
                _ => new AiReply(AiReplyStatus.Failed, Error: result.Text)
            };
        }
        catch (OperationCanceledException)
        {
            return new AiReply(AiReplyStatus.Cancelled);
        }
        catch (Exception)
        {
            // The exception can quote the request, which may hold a screenshot or its text. It is not logged.
            return new AiReply(AiReplyStatus.Failed);
        }
    }

    private AssistantProviderRuntimeSettings? ReadSettings()
    {
        try
        {
            return _settings();
        }
        catch (Exception)
        {
            // Settings or the secret store are not loaded (a headless host, or very early in start-up).
            return null;
        }
    }

    private static AiConnection Describe(AssistantProviderRuntimeSettings settings) => new(
        settings.Metadata.DisplayName,
        settings.ModelId,
        settings.BaseUrl,
        settings.Metadata.SupportsImageInput,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{settings.Metadata.Id}|{settings.ModelId}|{settings.BaseUrl}|{settings.ApiKey}"))));
}

/// <summary>
/// The AI client the application's features use. Tests replace <see cref="Client"/>.
/// </summary>
public static class AiServices
{
    public static IAiClient Client { get; set; } = new AiClient();

    /// <summary>A provider is set up and has what it needs to answer.</summary>
    public static bool IsAvailable => Client.Connection != null;

    /// <summary>A provider is set up and its model reads images.</summary>
    public static bool CanReadImages => Client.Connection?.SupportsImages == true;
}

internal static class AiImage
{
    // Providers refuse larger uploads; HttpAssistantModelProvider enforces the same limit.
    internal const int MaxBytes = 20_000_000;

    /// <summary>PNG bytes for an image, or null when it cannot be made small enough.</summary>
    internal static byte[]? Encode(SKBitmap image, bool shrinkToFit)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return null;
        }

        byte[] png = EncodePng(image);

        if (png.Length <= MaxBytes)
        {
            return png;
        }

        if (!shrinkToFit)
        {
            return null;
        }

        float scale = 1f;

        for (int attempt = 0; attempt < 6; attempt++)
        {
            scale *= 0.7f;
            using var smaller = new SKBitmap(new SKImageInfo(
                Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Premul));
            image.ScalePixels(smaller, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            png = EncodePng(smaller);

            if (png.Length <= MaxBytes)
            {
                return png;
            }
        }

        return null;
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

internal static class AiJson
{
    /// <summary>The JSON object or array inside a model's reply, or null when there is none.</summary>
    internal static string? Extract(string text)
    {
        int objectStart = text.IndexOf('{');
        int arrayStart = text.IndexOf('[');
        int start = objectStart < 0 ? arrayStart : arrayStart < 0 ? objectStart : Math.Min(objectStart, arrayStart);

        if (start < 0)
        {
            return null;
        }

        int end = text.LastIndexOf(text[start] == '{' ? '}' : ']');
        return end > start ? text[start..(end + 1)] : null;
    }
}
