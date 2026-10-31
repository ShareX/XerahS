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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XerahS.Core.Cloud;

/// <summary>
/// Validates XerahS Cloud (Better Auth) OAuth/OIDC tokens locally against the site's asymmetric JWKS.
/// Symmetric legacy JWT secrets are intentionally unsupported because a public desktop client
/// must never receive a shared signing secret.
/// </summary>
public sealed class XerahSCloudTokenValidator : IXerahSCloudTokenValidator
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan JwksCacheLifetime = TimeSpan.FromMinutes(15);
    // XerahS Cloud issues 900s access tokens. Reject anything longer than one hour so a
    // public desktop client never accepts week-long access tokens.
    private static readonly TimeSpan MaximumAccessTokenLifetime = TimeSpan.FromHours(1);

    private readonly HttpClient? _httpClient;
    private readonly IXerahSCloudClock _clock;
    private readonly SemaphoreSlim _jwksLock = new(1, 1);
    private IReadOnlyList<JsonWebKey> _keys = [];
    private DateTimeOffset _keysExpireAt;

    public XerahSCloudTokenValidator(HttpClient? httpClient, IXerahSCloudClock clock)
    {
        _httpClient = httpClient;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    private HttpClient Http => _httpClient ?? XerahS.Common.HttpClientFactory.Create();

    public async Task<XerahSCloudSession> ValidateAsync(
        string accessToken,
        string refreshToken,
        string? idToken,
        int expiresInSeconds,
        string? expectedNonce,
        XerahSCloudOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        ArgumentNullException.ThrowIfNull(options);
        if (options.OAuthAuthority == null || string.IsNullOrWhiteSpace(options.OAuthClientId))
        {
            throw new XerahSCloudSecurityException("OAuth token validation requires a configured authority and client ID.");
        }

        if (expiresInSeconds <= 0 || expiresInSeconds > MaximumAccessTokenLifetime.TotalSeconds)
        {
            throw new XerahSCloudSecurityException(
                $"OAuth access-token lifetime is outside the accepted desktop policy ({expiresInSeconds}s).");
        }

        // Better Auth's OAuth server names the site origin as issuer; access tokens are for the owner API.
        string issuer = XerahSCloudOptions.Issuer(options.OAuthAuthority);
        string apiAudience = XerahSCloudOptions.ApiAudience(options.OAuthAuthority);
        JsonElement accessClaims = await VerifyJwtAsync(accessToken, options, cancellationToken).ConfigureAwait(false);
        DateTimeOffset accessExpiry = ValidateCommonClaims(accessClaims, issuer, apiAudience);
        string subject = RequireString(accessClaims, "sub");
        string sessionId = RequireString(accessClaims, "session_id");
        string clientId = RequireString(accessClaims, "client_id");
        string aal = RequireString(accessClaims, "aal");

        if (!string.Equals(clientId, options.OAuthClientId, StringComparison.Ordinal))
        {
            throw new XerahSCloudSecurityException("OAuth access token was not issued to this desktop client.");
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new XerahSCloudSecurityException("OAuth access token is missing a session identifier.");
        }

        if (!string.Equals(aal, "aal2", StringComparison.Ordinal))
        {
            throw new XerahSCloudSecurityException(
                $"OAuth access token authenticator assurance is '{aal}', expected aal2.");
        }

        if (accessExpiry - _clock.UtcNow > MaximumAccessTokenLifetime + ClockSkew)
        {
            throw new XerahSCloudSecurityException("OAuth access token exceeds the accepted desktop lifetime.");
        }

        if (expectedNonce != null)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new XerahSCloudSecurityException("The OpenID Connect token response did not contain an ID token.");
            }

            JsonElement idClaims = await VerifyJwtAsync(idToken, options, cancellationToken).ConfigureAwait(false);
            ValidateCommonClaims(idClaims, issuer, options.OAuthClientId);
            if (!FixedTimeEquals(RequireString(idClaims, "nonce"), expectedNonce) ||
                !string.Equals(RequireString(idClaims, "sub"), subject, StringComparison.Ordinal))
            {
                throw new XerahSCloudSecurityException("OpenID Connect nonce or subject validation failed.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(idToken))
        {
            JsonElement idClaims = await VerifyJwtAsync(idToken, options, cancellationToken).ConfigureAwait(false);
            ValidateCommonClaims(idClaims, issuer, options.OAuthClientId);
            if (!string.Equals(RequireString(idClaims, "sub"), subject, StringComparison.Ordinal))
            {
                throw new XerahSCloudSecurityException("Refreshed OpenID Connect subject validation failed.");
            }
        }

        DateTimeOffset responseExpiry = _clock.UtcNow.AddSeconds(expiresInSeconds);
        return new XerahSCloudSession(
            accessToken,
            refreshToken,
            subject,
            responseExpiry < accessExpiry ? responseExpiry : accessExpiry);
    }

    private async Task<JsonElement> VerifyJwtAsync(
        string token,
        XerahSCloudOptions options,
        CancellationToken cancellationToken)
    {
        string[] segments = token.Split('.');
        if (segments.Length != 3 || segments.Any(string.IsNullOrWhiteSpace) || token.Length > 32768)
        {
            throw new XerahSCloudSecurityException("OAuth server returned a malformed JWT.");
        }

        JsonElement header = ParseSegment(segments[0]);
        JsonElement claims = ParseSegment(segments[1]);
        string algorithm = RequireString(header, "alg");
        string keyId = RequireString(header, "kid");
        if (algorithm is not ("RS256" or "ES256"))
        {
            throw new XerahSCloudSecurityException("OAuth JWT uses an unsupported signing algorithm.");
        }

        JsonWebKey? key = await FindKeyAsync(keyId, algorithm, options, forceRefresh: false, cancellationToken)
            .ConfigureAwait(false);
        key ??= await FindKeyAsync(keyId, algorithm, options, forceRefresh: true, cancellationToken)
            .ConfigureAwait(false);
        if (key == null)
        {
            throw new XerahSCloudSecurityException("OAuth JWT signing key was not found in the project JWKS.");
        }

        byte[] signedBytes = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        bool valid;
        try
        {
            byte[] signature = DecodeBase64Url(segments[2]);
            valid = algorithm switch
            {
                "RS256" => VerifyRsa(key, signedBytes, signature),
                "ES256" => VerifyEc(key, signedBytes, signature),
                _ => false
            };
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            throw new XerahSCloudSecurityException("OAuth JWT signing key or signature is invalid.", ex);
        }

        if (!valid)
        {
            throw new XerahSCloudSecurityException("OAuth JWT signature validation failed.");
        }

        return claims;
    }

    private async Task<JsonWebKey?> FindKeyAsync(
        string keyId,
        string algorithm,
        XerahSCloudOptions options,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!forceRefresh && _keysExpireAt > _clock.UtcNow)
        {
            return _keys.FirstOrDefault(key => key.Matches(keyId, algorithm));
        }

        await _jwksLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (forceRefresh || _keysExpireAt <= _clock.UtcNow)
            {
                if (options.OAuthAuthority == null)
                {
                    return null;
                }

                Uri jwksEndpoint = new(options.OAuthAuthority, XerahSCloudOptions.JwksPath);
                using HttpResponseMessage response = await Http.GetAsync(jwksEndpoint, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new XerahSCloudSecurityException($"OAuth JWKS request failed with HTTP {(int)response.StatusCode}.");
                }

                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty("keys", out JsonElement keysElement) ||
                    keysElement.ValueKind != JsonValueKind.Array)
                {
                    throw new XerahSCloudSecurityException("OAuth JWKS response is invalid.");
                }

                _keys = keysElement.EnumerateArray().Select(ParseKey).ToArray();
                _keysExpireAt = _clock.UtcNow.Add(JwksCacheLifetime);
            }

            return _keys.FirstOrDefault(key => key.Matches(keyId, algorithm));
        }
        catch (JsonException ex)
        {
            throw new XerahSCloudSecurityException("OAuth JWKS response is invalid.", ex);
        }
        finally
        {
            _jwksLock.Release();
        }
    }

    private DateTimeOffset ValidateCommonClaims(JsonElement claims, string expectedIssuer, string expectedAudience)
    {
        if (!string.Equals(RequireString(claims, "iss").TrimEnd('/'), expectedIssuer, StringComparison.Ordinal) ||
            !HasAudience(claims, expectedAudience))
        {
            throw new XerahSCloudSecurityException("OAuth JWT issuer or audience validation failed.");
        }

        long expirySeconds = RequireInt64(claims, "exp");
        DateTimeOffset expiry = DateTimeOffset.FromUnixTimeSeconds(expirySeconds);
        if (expiry <= _clock.UtcNow.Subtract(ClockSkew))
        {
            throw new XerahSCloudSecurityException("OAuth JWT has expired.");
        }

        if (claims.TryGetProperty("nbf", out JsonElement notBeforeElement) &&
            notBeforeElement.ValueKind == JsonValueKind.Number &&
            notBeforeElement.TryGetInt64(out long notBeforeSeconds) &&
            DateTimeOffset.FromUnixTimeSeconds(notBeforeSeconds) > _clock.UtcNow.Add(ClockSkew))
        {
            throw new XerahSCloudSecurityException("OAuth JWT is not valid yet.");
        }

        return expiry;
    }

    private static bool HasAudience(JsonElement claims, string expected)
    {
        if (!claims.TryGetProperty("aud", out JsonElement audience))
        {
            return false;
        }

        return audience.ValueKind switch
        {
            JsonValueKind.String => string.Equals(audience.GetString(), expected, StringComparison.Ordinal),
            JsonValueKind.Array => audience.EnumerateArray().Any(value =>
                value.ValueKind == JsonValueKind.String &&
                string.Equals(value.GetString(), expected, StringComparison.Ordinal)),
            _ => false
        };
    }

    private static JsonElement ParseSegment(string segment)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(DecodeBase64Url(segment));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new XerahSCloudSecurityException("OAuth JWT segment is not a JSON object.");
            }

            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new XerahSCloudSecurityException("OAuth JWT contains invalid encoding.", ex);
        }
    }

    private static JsonWebKey ParseKey(JsonElement element) => new(
        RequireString(element, "kid"),
        RequireString(element, "kty"),
        element.TryGetProperty("alg", out JsonElement alg) ? alg.GetString() : null,
        element.TryGetProperty("n", out JsonElement modulus) ? modulus.GetString() : null,
        element.TryGetProperty("e", out JsonElement exponent) ? exponent.GetString() : null,
        element.TryGetProperty("crv", out JsonElement curve) ? curve.GetString() : null,
        element.TryGetProperty("x", out JsonElement x) ? x.GetString() : null,
        element.TryGetProperty("y", out JsonElement y) ? y.GetString() : null);

    private static bool VerifyRsa(JsonWebKey key, byte[] data, byte[] signature)
    {
        if (key.KeyType != "RSA" || key.Modulus == null || key.Exponent == null)
        {
            return false;
        }

        using RSA rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = DecodeBase64Url(key.Modulus),
            Exponent = DecodeBase64Url(key.Exponent)
        });
        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static bool VerifyEc(JsonWebKey key, byte[] data, byte[] signature)
    {
        if (key.KeyType != "EC" || key.Curve != "P-256" || key.X == null || key.Y == null)
        {
            return false;
        }

        using ECDsa ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = DecodeBase64Url(key.X),
                Y = DecodeBase64Url(key.Y)
            }
        });
        return ecdsa.VerifyData(
            data,
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new XerahSCloudSecurityException($"OAuth JWT is missing the required '{propertyName}' claim.");
        }

        return value.GetString()!;
    }

    private static long RequireInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long result))
        {
            throw new XerahSCloudSecurityException($"OAuth JWT is missing the required '{propertyName}' claim.");
        }

        return result;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            0 => string.Empty,
            2 => "==",
            3 => "=",
            _ => throw new FormatException("Invalid base64url length.")
        };
        return Convert.FromBase64String(padded);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
            CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private sealed record JsonWebKey(
        string KeyId,
        string KeyType,
        string? Algorithm,
        string? Modulus,
        string? Exponent,
        string? Curve,
        string? X,
        string? Y)
    {
        public bool Matches(string keyId, string algorithm) =>
            string.Equals(KeyId, keyId, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(Algorithm) || string.Equals(Algorithm, algorithm, StringComparison.Ordinal));
    }
}
