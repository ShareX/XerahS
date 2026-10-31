#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

namespace XerahS.Core.Cloud;

public sealed class XerahSCloudOptions
{
    public Uri ApiBaseAddress { get; init; } = new("https://cloud.xerahs.com/");
    public Uri? OAuthAuthority { get; init; }
    public string? OAuthClientId { get; init; }
    public Uri OAuthRedirectUri { get; init; } = new("https://cloud.xerahs.com/auth/desktop/callback");
    public Uri OAuthCallbackUri { get; init; } = new("xerahs://oauth/callback");
    public bool FeatureEnabled { get; init; }

    public bool IsOAuthConfigured => FeatureEnabled &&
        OAuthAuthority != null &&
        !string.IsNullOrWhiteSpace(OAuthClientId);

    /// <summary>
    /// Authorized XerahS Cloud public client. PKCE public clients have no secret.
    /// </summary>
    internal static readonly Uri CloudApiBaseAddress = new("https://cloud.xerahs.com/");
    // The XerahS Cloud site is its own OAuth server (Better Auth, /api/auth).
    internal static readonly Uri CloudOAuthAuthority = new("https://cloud.xerahs.com/");
    internal const string CloudOAuthClientId = "8d8adf92-86c4-4036-a4c9-09901230f2c4";
    internal static readonly Uri CloudOAuthRedirectUri = new("https://cloud.xerahs.com/auth/desktop/callback");

    internal const string AuthorizePath = "/api/auth/oauth2/authorize";
    internal const string TokenPath = "/api/auth/oauth2/token";
    internal const string JwksPath = "/api/auth/jwks";
    internal const string DesktopScopes = "openid email profile offline_access";

    /// <summary>Token issuer: the authority's origin, without a path.</summary>
    internal static string Issuer(Uri authority) => authority.GetLeftPart(UriPartial.Authority);

    /// <summary>Audience (RFC 8707 resource) of desktop access tokens: the owner API.</summary>
    internal static string ApiAudience(Uri authority) => Issuer(authority) + "/api/v1";

    public static XerahSCloudOptions FromEnvironment() =>
        FromValues(
            Environment.GetEnvironmentVariable("XERAHS_CLOUD_API_BASE_ADDRESS"),
            Environment.GetEnvironmentVariable("XERAHS_CLOUD_OAUTH_AUTHORITY"),
            Environment.GetEnvironmentVariable("XERAHS_CLOUD_OAUTH_CLIENT_ID"),
            Environment.GetEnvironmentVariable("XERAHS_CLOUD_OAUTH_REDIRECT_URI"),
            Environment.GetEnvironmentVariable("XERAHS_CLOUD_DESKTOP_ENABLED"));

    internal static XerahSCloudOptions FromValues(
        string? apiBaseAddress,
        string? authority,
        string? clientId,
        string? redirectUri,
        string? enabledRaw)
    {
        bool parsedEnabled = bool.TryParse(enabledRaw, out bool enabledFlag);
        bool explicitlyDisabled = parsedEnabled && !enabledFlag;
        bool useCloudDefaults = !explicitlyDisabled &&
            string.IsNullOrWhiteSpace(apiBaseAddress) &&
            string.IsNullOrWhiteSpace(authority) &&
            string.IsNullOrWhiteSpace(clientId);

        if (useCloudDefaults)
        {
            return new XerahSCloudOptions
            {
                ApiBaseAddress = CloudApiBaseAddress,
                OAuthAuthority = CloudOAuthAuthority,
                OAuthClientId = CloudOAuthClientId,
                OAuthRedirectUri = CloudOAuthRedirectUri,
                FeatureEnabled = true
            };
        }

        Uri? parsedApiBaseAddress = TryCreateSecureUri(apiBaseAddress);
        Uri? parsedRedirectUri = TryCreateSecureUri(redirectUri);
        bool invalidEndpoint =
            (!string.IsNullOrWhiteSpace(apiBaseAddress) && parsedApiBaseAddress == null) ||
            (!string.IsNullOrWhiteSpace(redirectUri) && parsedRedirectUri == null);

        return new XerahSCloudOptions
        {
            ApiBaseAddress = parsedApiBaseAddress ?? CloudApiBaseAddress,
            OAuthAuthority = TryCreateSecureUri(authority),
            OAuthClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim(),
            OAuthRedirectUri = parsedRedirectUri ?? CloudOAuthRedirectUri,
            FeatureEnabled = !explicitlyDisabled && parsedEnabled && !invalidEndpoint
        };
    }

    internal static void RequireSecureHttpEndpoint(Uri endpoint, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(endpoint, parameterName);
        if (!endpoint.IsAbsoluteUri ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new ArgumentException("XerahS Cloud endpoints must be absolute HTTPS URLs without credentials, query, or fragment.", parameterName);
        }
    }

    private static Uri? TryCreateSecureUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        try
        {
            RequireSecureHttpEndpoint(uri, nameof(value));
            return uri;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

public sealed record XerahSCloudPublishRequest(
    string ClientItemId,
    string Url,
    string? ThumbnailUrl,
    string Kind,
    string FileName,
    DateTimeOffset CapturedAt,
    string? Host,
    string? ContentType);

public sealed record XerahSCloudPublishResponse(
    string Id,
    string OwnerSubject,
    Uri? ProfileUrl,
    DateTimeOffset PublishedAt);

public enum XerahSCloudDeleteState
{
    Removed,
    AcceptedPending
}

public sealed record XerahSCloudDeleteResponse(XerahSCloudDeleteState State);

public sealed record XerahSCloudAccountSummary(
    string Slug,
    Uri ProfileUrl,
    Uri SettingsUrl,
    string TimeZone,
    bool StrongAuth,
    string TrialStatus,
    DateTimeOffset? TrialEndsAt,
    string? SubscriptionStatus,
    DateTimeOffset? PaidThrough,
    bool CanPublish,
    bool DisputeSuspended);

public interface IXerahSCloudClient
{
    bool IsConfigured { get; }
    bool HasSessionCredential { get; }
    string? CurrentOwnerSubject { get; }

    Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default);

    Task<XerahSCloudAccountSummary> GetAccountAsync(CancellationToken cancellationToken = default);

    void SignOut();

    Task<XerahSCloudPublishResponse> PublishAsync(
        XerahSCloudPublishRequest request,
        CancellationToken cancellationToken = default);

    Task<XerahSCloudDeleteResponse> UnpublishAsync(
        string clientItemId,
        string expectedOwnerSubject,
        CancellationToken cancellationToken = default);
}

public class XerahSCloudException : Exception
{
    public XerahSCloudException(string message) : base(message)
    {
    }

    public XerahSCloudException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class XerahSCloudSecurityException : XerahSCloudException
{
    public XerahSCloudSecurityException(string message) : base(message)
    {
    }

    public XerahSCloudSecurityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
