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

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XerahS.Common;

namespace XerahS.Core.Cloud;

public sealed record XerahSCloudOAuthAttempt(
    Uri AuthorizationUri,
    string State,
    string Nonce,
    string CodeVerifier,
    DateTimeOffset ExpiresAt);

public sealed record XerahSCloudOAuthCallback(string? Code, string State, string? Error);

public enum XerahSCloudOAuthCompletion
{
    Accepted,
    InvalidCallback,
    UnknownOrReplayedState,
    Expired,
    Denied,
    TokenRejected
}

public interface IXerahSCloudOAuthCoordinator
{
    XerahSCloudOAuthAttempt Begin();
    Task<XerahSCloudOAuthCompletion> WaitForCompletionAsync(
        string state,
        CancellationToken cancellationToken = default);
    Task<XerahSCloudOAuthCompletion> CompleteAsync(Uri callbackUri, CancellationToken cancellationToken = default);
}

public interface IXerahSCloudOAuthTokenExchange
{
    Task<XerahSCloudSession> ExchangeAsync(
        string code,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken);

    Task<XerahSCloudSession> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);
}

public interface IXerahSCloudTokenValidator
{
    Task<XerahSCloudSession> ValidateAsync(
        string accessToken,
        string refreshToken,
        string? idToken,
        int expiresInSeconds,
        string? expectedNonce,
        XerahSCloudOptions options,
        CancellationToken cancellationToken);
}

public interface IXerahSCloudClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemXerahSCloudClock : IXerahSCloudClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class XerahSCloudOAuthCallbackParser
{
    public static bool TryParse(Uri? uri, out XerahSCloudOAuthCallback? callback)
    {
        callback = null;
        if (uri == null ||
            !uri.IsAbsoluteUri ||
            !uri.Scheme.Equals("xerahs", StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("oauth", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.Equals("/callback", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.UserInfo.Length != 0)
        {
            return false;
        }

        Dictionary<string, string> values = ParseQuery(uri.Query);
        bool hasCode = values.TryGetValue("code", out string? code) && !string.IsNullOrWhiteSpace(code);
        bool hasError = values.TryGetValue("error", out string? error) &&
            !string.IsNullOrWhiteSpace(error) && error.Length <= 128 &&
            error.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
        if (values.Keys.Any(IsForbiddenTokenParameter) ||
            values.Count != 2 ||
            !values.TryGetValue("state", out string? state) ||
            string.IsNullOrWhiteSpace(state) ||
            hasCode == hasError)
        {
            return false;
        }

        callback = new XerahSCloudOAuthCallback(hasCode ? code : null, state, hasError ? error : null);
        return true;
    }

    public static bool IsCallbackArgument(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme.Equals("xerahs", StringComparison.OrdinalIgnoreCase) &&
        uri.Host.Equals("oauth", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string component in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = component.Split('=', 2);
            string key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
            string value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty;
            if (!values.TryAdd(key, value))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        return values;
    }

    private static bool IsForbiddenTokenParameter(string name) =>
        name.Equals("access_token", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("refresh_token", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("id_token", StringComparison.OrdinalIgnoreCase);
}

public static class XerahSCloudArgumentRedactor
{
    public const string RedactedCallback = "xerahs://oauth/callback?[REDACTED]";

    public static string[] Redact(IEnumerable<string>? arguments) =>
        arguments?.Select(argument => XerahSCloudOAuthCallbackParser.IsCallbackArgument(argument)
            ? RedactedCallback
            : argument).ToArray() ?? [];
}

public sealed class XerahSCloudOAuthCoordinator : IXerahSCloudOAuthCoordinator
{
    private static readonly TimeSpan AttemptLifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, XerahSCloudOAuthAttempt> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _consumed = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<XerahSCloudOAuthCompletion>> _waiters = new(StringComparer.Ordinal);
    private readonly XerahSCloudOptions _options;
    private readonly IXerahSCloudOAuthTokenExchange _tokenExchange;
    private readonly IXerahSCloudSessionStore _sessionStore;
    private readonly IXerahSCloudClock _clock;

    public XerahSCloudOAuthCoordinator(
        XerahSCloudOptions options,
        IXerahSCloudOAuthTokenExchange tokenExchange,
        IXerahSCloudSessionStore sessionStore,
        IXerahSCloudClock clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenExchange = tokenExchange ?? throw new ArgumentNullException(nameof(tokenExchange));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public XerahSCloudOAuthAttempt Begin()
    {
        if (!_options.IsOAuthConfigured || _options.OAuthAuthority == null || string.IsNullOrWhiteSpace(_options.OAuthClientId))
        {
            throw new XerahSCloudException("XerahS Cloud OAuth is disabled or incomplete.");
        }

        XerahSCloudOptions.RequireSecureHttpEndpoint(_options.OAuthAuthority, nameof(_options.OAuthAuthority));
        string state = CreateRandomValue(32);
        string nonce = CreateRandomValue(32);
        string verifier = CreateRandomValue(64);
        string challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        DateTimeOffset expiresAt = _clock.UtcNow.Add(AttemptLifetime);

        Uri authorizationEndpoint = new(_options.OAuthAuthority, XerahSCloudOptions.AuthorizePath);
        string query = string.Join('&', new Dictionary<string, string>
        {
            ["client_id"] = _options.OAuthClientId,
            ["redirect_uri"] = _options.OAuthRedirectUri.AbsoluteUri,
            ["response_type"] = "code",
            ["scope"] = XerahSCloudOptions.DesktopScopes,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            // JWT access tokens for the owner API, and always show the consent page:
            // approving creates the revocable desktop grant.
            ["resource"] = XerahSCloudOptions.ApiAudience(_options.OAuthAuthority),
            ["prompt"] = "consent"
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        var attempt = new XerahSCloudOAuthAttempt(
            new UriBuilder(authorizationEndpoint) { Query = query }.Uri,
            state,
            nonce,
            verifier,
            expiresAt);
        _pending[state] = attempt;
        _waiters[state] = new TaskCompletionSource<XerahSCloudOAuthCompletion>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        return attempt;
    }

    public async Task<XerahSCloudOAuthCompletion> WaitForCompletionAsync(
        string state,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (!_waiters.TryGetValue(state, out TaskCompletionSource<XerahSCloudOAuthCompletion>? waiter))
        {
            return XerahSCloudOAuthCompletion.UnknownOrReplayedState;
        }

        try
        {
            return await waiter.Task.WaitAsync(AttemptLifetime, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _pending.TryRemove(state, out _);
            _waiters.TryRemove(state, out _);
            _consumed.TryAdd(state, 0);
            return XerahSCloudOAuthCompletion.Expired;
        }
    }

    public async Task<XerahSCloudOAuthCompletion> CompleteAsync(
        Uri callbackUri,
        CancellationToken cancellationToken = default)
    {
        if (!XerahSCloudOAuthCallbackParser.TryParse(callbackUri, out XerahSCloudOAuthCallback? callback) || callback == null)
        {
            return XerahSCloudOAuthCompletion.InvalidCallback;
        }

        if (_consumed.ContainsKey(callback.State) || !_pending.TryRemove(callback.State, out XerahSCloudOAuthAttempt? attempt))
        {
            return XerahSCloudOAuthCompletion.UnknownOrReplayedState;
        }

        _consumed.TryAdd(callback.State, 0);
        if (attempt.ExpiresAt <= _clock.UtcNow)
        {
            CompleteWaiter(callback.State, XerahSCloudOAuthCompletion.Expired);
            return XerahSCloudOAuthCompletion.Expired;
        }

        if (callback.Error != null)
        {
            CompleteWaiter(callback.State, XerahSCloudOAuthCompletion.Denied);
            return XerahSCloudOAuthCompletion.Denied;
        }

        try
        {
            XerahSCloudSession session = await _tokenExchange
                .ExchangeAsync(callback.Code!, attempt.CodeVerifier, attempt.Nonce, cancellationToken)
                .ConfigureAwait(false);
            _sessionStore.Accept(session);
            CompleteWaiter(callback.State, XerahSCloudOAuthCompletion.Accepted);
            return XerahSCloudOAuthCompletion.Accepted;
        }
        catch (Exception ex) when (ex is XerahSCloudException or HttpRequestException or JsonException)
        {
            DebugHelper.WriteException(ex, "XerahS Cloud OAuth token exchange rejected");
            CompleteWaiter(callback.State, XerahSCloudOAuthCompletion.TokenRejected);
            return XerahSCloudOAuthCompletion.TokenRejected;
        }
    }

    private void CompleteWaiter(string state, XerahSCloudOAuthCompletion completion)
    {
        if (_waiters.TryRemove(state, out TaskCompletionSource<XerahSCloudOAuthCompletion>? waiter))
        {
            waiter.TrySetResult(completion);
        }
    }

    private static string CreateRandomValue(int byteCount) => Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class XerahSCloudOAuthTokenExchange : IXerahSCloudOAuthTokenExchange
{
    private readonly HttpClient? _httpClient;
    private readonly XerahSCloudOptions _options;
    private readonly IXerahSCloudTokenValidator _tokenValidator;

    public XerahSCloudOAuthTokenExchange(
        HttpClient? httpClient,
        XerahSCloudOptions options,
        IXerahSCloudTokenValidator tokenValidator)
    {
        _httpClient = httpClient;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
    }

    private HttpClient Http => _httpClient ?? XerahS.Common.HttpClientFactory.Create();

    public async Task<XerahSCloudSession> ExchangeAsync(
        string code,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        if (_options.OAuthAuthority == null || string.IsNullOrWhiteSpace(_options.OAuthClientId))
        {
            throw new XerahSCloudException("XerahS Cloud OAuth is not configured.");
        }

        Uri tokenEndpoint = new(_options.OAuthAuthority, XerahSCloudOptions.TokenPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _options.OAuthClientId,
                ["redirect_uri"] = _options.OAuthRedirectUri.AbsoluteUri,
                ["code_verifier"] = codeVerifier,
                ["resource"] = XerahSCloudOptions.ApiAudience(_options.OAuthAuthority)
            })
        };

        using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new XerahSCloudSecurityException(
                $"OAuth token exchange failed with HTTP {(int)response.StatusCode}.");
        }

        OAuthTokenResponse? token = await response.Content
            .ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new XerahSCloudSecurityException("OAuth token exchange returned an invalid response.");
        }

        return await _tokenValidator.ValidateAsync(
            token.AccessToken,
            token.RefreshToken,
            token.IdToken,
            token.ExpiresIn,
            expectedNonce,
            _options,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<XerahSCloudSession> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        if (_options.OAuthAuthority == null || string.IsNullOrWhiteSpace(_options.OAuthClientId))
        {
            throw new XerahSCloudException("XerahS Cloud OAuth is not configured.");
        }

        Uri tokenEndpoint = new(_options.OAuthAuthority, XerahSCloudOptions.TokenPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.OAuthClientId,
                ["resource"] = XerahSCloudOptions.ApiAudience(_options.OAuthAuthority)
            })
        };

        using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode is >= 400 and < 500)
            {
                throw new XerahSCloudSecurityException($"OAuth refresh was rejected with HTTP {(int)response.StatusCode}.");
            }

            throw new XerahSCloudException($"OAuth refresh failed with HTTP {(int)response.StatusCode}.");
        }

        OAuthTokenResponse? token = await response.Content
            .ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new XerahSCloudSecurityException("OAuth refresh returned an invalid response.");
        }

        return await _tokenValidator.ValidateAsync(
            token.AccessToken,
            token.RefreshToken,
            token.IdToken,
            token.ExpiresIn,
            expectedNonce: null,
            _options,
            cancellationToken).ConfigureAwait(false);
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
