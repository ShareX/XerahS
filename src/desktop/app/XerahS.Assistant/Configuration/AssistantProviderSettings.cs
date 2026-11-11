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

using XerahS.Core;
using XerahS.Core.Uploaders;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;

namespace XerahS.Assistant.Configuration;

public sealed record AssistantProviderRuntimeSettings(
    AssistantProviderMetadata Metadata,
    string ModelId,
    string BaseUrl,
    string? ApiKey,
    bool StreamResponses = false);

public static class AssistantProviderSecrets
{
    private const string SecretProviderId = "xerahs.assistant";
    private const string ApiKeyName = "apiKey";
    internal const string ChatGptSessionName = "chatGptSession";

    public static bool HasApiKey(string providerId) =>
        ProviderContextManager.EnsureProviderContext().Secrets.HasSecret(SecretProviderId, BuildSecretKey(providerId), ApiKeyName);

    public static string? GetApiKey(string providerId) =>
        ProviderContextManager.EnsureProviderContext().Secrets.GetSecret(SecretProviderId, BuildSecretKey(providerId), ApiKeyName);

    public static void SetApiKey(string providerId, string apiKey) =>
        ProviderContextManager.EnsureProviderContext().Secrets.SetSecret(SecretProviderId, BuildSecretKey(providerId), ApiKeyName, apiKey);

    public static void DeleteApiKey(string providerId) =>
        ProviderContextManager.EnsureProviderContext().Secrets.DeleteSecret(SecretProviderId, BuildSecretKey(providerId), ApiKeyName);

    internal static string? GetNamedSecret(string providerId, string name) =>
        ProviderContextManager.EnsureProviderContext().Secrets.GetSecret(SecretProviderId, BuildSecretKey(providerId), name);

    internal static void SetNamedSecret(string providerId, string name, string value) =>
        ProviderContextManager.EnsureProviderContext().Secrets.SetSecret(SecretProviderId, BuildSecretKey(providerId), name, value);

    internal static void DeleteNamedSecret(string providerId, string name) =>
        ProviderContextManager.EnsureProviderContext().Secrets.DeleteSecret(SecretProviderId, BuildSecretKey(providerId), name);

    private static string BuildSecretKey(string providerId) => $"provider:{providerId}";
}

public static class AssistantProviderSettingsResolver
{
    public static bool TryGetActive(out AssistantProviderRuntimeSettings settings)
    {
        settings = default!;

        string providerId = SettingsManager.Settings.AssistantActiveProviderId;
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return false;
        }

        AssistantProviderMetadata? metadata = AssistantProviderCatalog.Find(providerId);
        if (metadata == null)
        {
            return false;
        }

        AssistantProviderConfig config = GetOrCreateConfig(providerId);
        string baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? metadata.DefaultBaseUrl : config.BaseUrl.TrimEnd('/');
        string modelId = string.IsNullOrWhiteSpace(config.ModelId) ? metadata.DefaultModelId : config.ModelId;
        bool streamResponses = false;
        string? apiKey = metadata.Protocol == AssistantProviderProtocol.OllamaGenerate ? null : AssistantProviderSecrets.GetApiKey(metadata.Id);
        if (string.Equals(metadata.Id, "openai", StringComparison.OrdinalIgnoreCase) &&
            ChatGptSessionStore.TryGetUsable(out ChatGptSession chatGpt))
        {
            apiKey = chatGpt.AccessToken;
            streamResponses = true;
        }

        if (metadata.Protocol != AssistantProviderProtocol.OllamaGenerate && string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        settings = new AssistantProviderRuntimeSettings(metadata, modelId, baseUrl, apiKey, streamResponses);
        return true;
    }

    public static AssistantProviderConfig GetOrCreateConfig(string providerId)
    {
        AssistantProviderConfig? config = SettingsManager.Settings.AssistantProviders
            .FirstOrDefault(item => string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));

        if (config != null)
        {
            return config;
        }

        AssistantProviderMetadata? metadata = AssistantProviderCatalog.Find(providerId);
        config = new AssistantProviderConfig
        {
            ProviderId = providerId,
            ModelId = metadata?.DefaultModelId ?? string.Empty,
            BaseUrl = metadata?.DefaultBaseUrl ?? string.Empty
        };

        SettingsManager.Settings.AssistantProviders.Add(config);
        return config;
    }
}
