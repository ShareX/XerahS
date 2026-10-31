#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using XerahS.Core.Cloud;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Cloud;

[TestFixture]
public sealed class XerahSCloudSecurityTests
{
    [Test]
    public void SessionStore_PersistsRefreshCredentialButNeverAccessToken()
    {
        var secrets = new FakeSecretStore(isFallback: false);
        var store = new XerahSCloudSessionStore(secrets);

        store.Accept(new XerahSCloudSession("access-sensitive", "refresh-sensitive", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5)));

        Assert.Multiple(() =>
        {
            Assert.That(secrets.Values.Values, Does.Contain("refresh-sensitive"));
            Assert.That(secrets.Values.Values, Does.Contain("owner-a"));
            Assert.That(secrets.Values.Values, Does.Not.Contain("access-sensitive"));
            Assert.That(store.Current?.AccessToken, Is.EqualTo("access-sensitive"));
        });
    }

    [Test]
    public void SessionStore_RejectsFallbackBackend()
    {
        var store = new XerahSCloudSessionStore(new FakeSecretStore(isFallback: true));

        Assert.Throws<XerahSCloudSecurityException>(() => store.Accept(
            new XerahSCloudSession("access", "refresh", "owner", DateTimeOffset.UtcNow.AddMinutes(5))));
    }

    [TestCase("xerahs://oauth/callback?code=abc&state=xyz", true)]
    [TestCase("xerahs://oauth/callback?error=access_denied&state=xyz", true)]
    [TestCase("xerahs://oauth/callback?error=access-denied&state=xyz", false)]
    [TestCase("xerahs://oauth/callback?code=abc&error=access_denied&state=xyz", false)]
    [TestCase("xerahs://oauth/callback?code=abc&state=xyz&access_token=secret", false)]
    [TestCase("xerahs://hostile/callback?code=abc&state=xyz", false)]
    [TestCase("https://cloud.xerahs.com/oauth/callback?code=abc&state=xyz", false)]
    [TestCase("xerahs://oauth/callback?code=abc&code=again&state=xyz", false)]
    public void CallbackParser_EnforcesExactResultAndStateOnly(string value, bool expected)
    {
        bool parsed = XerahSCloudOAuthCallbackParser.TryParse(new Uri(value), out XerahSCloudOAuthCallback? callback);

        Assert.That(parsed, Is.EqualTo(expected));
        Assert.That(callback != null, Is.EqualTo(expected));
    }

    [Test]
    public async Task OAuthCoordinator_ResumesDeniedAuthorizationWithoutTokenExchange()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sessions = new MemorySessionStore();
        var exchange = new FakeTokenExchange(clock);
        var coordinator = new XerahSCloudOAuthCoordinator(CreateOptions(), exchange, sessions, clock);
        XerahSCloudOAuthAttempt attempt = coordinator.Begin();
        var callback = new Uri($"xerahs://oauth/callback?error=access_denied&state={Uri.EscapeDataString(attempt.State)}");

        Task<XerahSCloudOAuthCompletion> resumed = coordinator.WaitForCompletionAsync(attempt.State);
        XerahSCloudOAuthCompletion completion = await coordinator.CompleteAsync(callback);
        XerahSCloudOAuthCompletion resumedCompletion = await resumed;

        Assert.Multiple(() =>
        {
            Assert.That(completion, Is.EqualTo(XerahSCloudOAuthCompletion.Denied));
            Assert.That(resumedCompletion, Is.EqualTo(XerahSCloudOAuthCompletion.Denied));
            Assert.That(exchange.LastVerifier, Is.Null);
            Assert.That(sessions.Current, Is.Null);
        });
    }

    [Test]
    public void ArgumentRedactor_RemovesCallbackSecrets()
    {
        string sensitive = "xerahs://oauth/callback?code=secret-code&state=secret-state";

        string[] result = XerahSCloudArgumentRedactor.Redact(["--safe", sensitive]);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.EqualTo("--safe"));
            Assert.That(result[1], Is.EqualTo(XerahSCloudArgumentRedactor.RedactedCallback));
            Assert.That(string.Join(' ', result), Does.Not.Contain("secret-code"));
            Assert.That(string.Join(' ', result), Does.Not.Contain("secret-state"));
        });
    }

    [Test]
    public async Task OAuthCoordinator_AcceptsOnceThenRejectsReplay()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var sessions = new MemorySessionStore();
        var exchange = new FakeTokenExchange(clock);
        var coordinator = new XerahSCloudOAuthCoordinator(CreateOptions(), exchange, sessions, clock);
        XerahSCloudOAuthAttempt attempt = coordinator.Begin();
        var callback = new Uri($"xerahs://oauth/callback?code=one-time-code&state={Uri.EscapeDataString(attempt.State)}");

        Task<XerahSCloudOAuthCompletion> resumed = coordinator.WaitForCompletionAsync(attempt.State);
        XerahSCloudOAuthCompletion first = await coordinator.CompleteAsync(callback);
        XerahSCloudOAuthCompletion replay = await coordinator.CompleteAsync(callback);
        XerahSCloudOAuthCompletion resumedResult = await resumed;

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(XerahSCloudOAuthCompletion.Accepted));
            Assert.That(resumedResult, Is.EqualTo(XerahSCloudOAuthCompletion.Accepted));
            Assert.That(replay, Is.EqualTo(XerahSCloudOAuthCompletion.UnknownOrReplayedState));
            Assert.That(exchange.LastVerifier, Is.EqualTo(attempt.CodeVerifier));
            Assert.That(exchange.LastNonce, Is.EqualTo(attempt.Nonce));
            Assert.That(sessions.Current?.OwnerSubject, Is.EqualTo("owner-a"));
            Assert.That(attempt.AuthorizationUri.Query, Does.Contain("code_challenge_method=S256"));
            Assert.That(attempt.AuthorizationUri.AbsolutePath, Is.EqualTo("/api/auth/oauth2/authorize"));
            Assert.That(attempt.AuthorizationUri.Query, Does.Contain("resource=https%3A%2F%2Fcloud.example.test%2Fapi%2Fv1"));
            Assert.That(attempt.AuthorizationUri.Query, Does.Contain("prompt=consent"));
            Assert.That(attempt.AuthorizationUri.Query, Does.Contain("offline_access"));
        });
    }

    [Test]
    public async Task ApiClient_UsesBearerAndStableIdempotencyKeyWithoutLiveNetwork()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"item\":{\"id\":\"gallery-1\",\"publishedAt\":\"2026-08-22T00:00:00Z\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var sessions = new MemorySessionStore
        {
            Current = new XerahSCloudSession("access", "refresh", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5))
        };
        var exchange = new FakeTokenExchange(new FakeClock(DateTimeOffset.UtcNow));
        var client = new XerahSCloudApiClient(new HttpClient(handler), sessions, exchange, CreateOptions());
        var request = new XerahSCloudPublishRequest(
            "54bec9ab-e292-45bf-abba-b378e45e8463",
            "https://cdn.example/capture.png",
            null,
            "screenshot",
            "capture.png",
            DateTimeOffset.UtcNow,
            "Example",
            "image/png");

        XerahSCloudPublishResponse response = await client.PublishAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo("gallery-1"));
            Assert.That(captured?.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(captured?.Headers.Authorization?.Parameter, Is.EqualTo("access"));
            Assert.That(captured?.Headers.GetValues("Idempotency-Key").Single(), Is.EqualTo(request.ClientItemId));
        });
    }

    [Test]
    public async Task TokenValidator_AcceptsSignedSupabaseAccessAndIdTokens()
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        using RSA rsa = RSA.Create(2048);
        RSAParameters publicKey = rsa.ExportParameters(includePrivateParameters: false);
        string jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kid = "key-1",
                    kty = "RSA",
                    alg = "RS256",
                    use = "sig",
                    n = Base64Url(publicKey.Modulus!),
                    e = Base64Url(publicKey.Exponent!)
                }
            }
        });
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jwks, Encoding.UTF8, "application/json")
        });
        var validator = new XerahSCloudTokenValidator(
            new HttpClient(handler),
            new FakeClock(now));
        string accessToken = CreateJwt(rsa, new
        {
            iss = "https://cloud.example.test",
            aud = new[] { "https://cloud.example.test/api/v1", "https://cloud.example.test/api/auth/oauth2/userinfo" },
            sub = "owner-a",
            client_id = "desktop-public-client",
            session_id = "session-a",
            aal = "aal2",
            exp = now.AddMinutes(10).ToUnixTimeSeconds(),
            nbf = now.AddMinutes(-1).ToUnixTimeSeconds()
        });
        string idToken = CreateJwt(rsa, new
        {
            iss = "https://cloud.example.test",
            aud = "desktop-public-client",
            sub = "owner-a",
            nonce = "expected-nonce",
            exp = now.AddMinutes(10).ToUnixTimeSeconds()
        });

        XerahSCloudSession session = await validator.ValidateAsync(
            accessToken,
            "refresh-rotated",
            idToken,
            expiresInSeconds: 600,
            expectedNonce: "expected-nonce",
            CreateOptions(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(session.OwnerSubject, Is.EqualTo("owner-a"));
            Assert.That(session.RefreshToken, Is.EqualTo("refresh-rotated"));
            Assert.That(session.ExpiresAt, Is.EqualTo(now.AddMinutes(10)));
        });
    }

    [Test]
    public void TokenValidator_RejectsNonceMismatch()
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        using RSA rsa = RSA.Create(2048);
        RSAParameters publicKey = rsa.ExportParameters(includePrivateParameters: false);
        string jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kid = "key-1",
                    kty = "RSA",
                    alg = "RS256",
                    n = Base64Url(publicKey.Modulus!),
                    e = Base64Url(publicKey.Exponent!)
                }
            }
        });
        var validator = new XerahSCloudTokenValidator(
            new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jwks, Encoding.UTF8, "application/json")
            })),
            new FakeClock(now));
        string accessToken = CreateJwt(rsa, new
        {
            iss = "https://cloud.example.test",
            aud = new[] { "https://cloud.example.test/api/v1", "https://cloud.example.test/api/auth/oauth2/userinfo" },
            sub = "owner-a",
            client_id = "desktop-public-client",
            session_id = "session-a",
            aal = "aal2",
            exp = now.AddMinutes(10).ToUnixTimeSeconds()
        });
        string idToken = CreateJwt(rsa, new
        {
            iss = "https://cloud.example.test",
            aud = "desktop-public-client",
            sub = "owner-a",
            nonce = "attacker-nonce",
            exp = now.AddMinutes(10).ToUnixTimeSeconds()
        });

        Assert.ThrowsAsync<XerahSCloudSecurityException>(async () => await validator.ValidateAsync(
            accessToken,
            "refresh",
            idToken,
            600,
            "expected-nonce",
            CreateOptions(),
            CancellationToken.None));
    }

    [Test]
    public void TokenValidator_RejectsAccessTokenLifetimeAboveOneHour()
    {
        var validator = new XerahSCloudTokenValidator(
            new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            new FakeClock(DateTimeOffset.UtcNow));

        XerahSCloudSecurityException? ex = Assert.ThrowsAsync<XerahSCloudSecurityException>(async () =>
            await validator.ValidateAsync(
                "header.payload.signature",
                "refresh",
                idToken: null,
                expiresInSeconds: 3601,
                expectedNonce: "nonce",
                CreateOptions(),
                CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("3601s"));
    }

    [Test]
    public async Task TokenValidator_AcceptsOneHourAccessTokenLifetime()
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        using RSA rsa = RSA.Create(2048);
        RSAParameters publicKey = rsa.ExportParameters(includePrivateParameters: false);
        string jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kid = "key-1",
                    kty = "RSA",
                    alg = "RS256",
                    n = Base64Url(publicKey.Modulus!),
                    e = Base64Url(publicKey.Exponent!)
                }
            }
        });
        var validator = new XerahSCloudTokenValidator(
            new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jwks, Encoding.UTF8, "application/json")
            })),
            new FakeClock(now));
        string accessToken = CreateJwt(rsa, new
        {
            iss = "https://cloud.example.test",
            aud = new[] { "https://cloud.example.test/api/v1", "https://cloud.example.test/api/auth/oauth2/userinfo" },
            sub = "owner-a",
            client_id = "desktop-public-client",
            session_id = "session-a",
            aal = "aal2",
            exp = now.AddHours(1).ToUnixTimeSeconds()
        });
        string idToken = CreateJwt(rsa, new
        {
            iss = "https://cloud.example.test",
            aud = "desktop-public-client",
            sub = "owner-a",
            nonce = "expected-nonce",
            exp = now.AddHours(1).ToUnixTimeSeconds()
        });

        XerahSCloudSession session = await validator.ValidateAsync(
            accessToken,
            "refresh-rotated",
            idToken,
            expiresInSeconds: 3600,
            expectedNonce: "expected-nonce",
            CreateOptions(),
            CancellationToken.None);

        Assert.That(session.OwnerSubject, Is.EqualTo("owner-a"));
    }

    [Test]
    public async Task ApiClient_RestoresRotatedRefreshCredentialAndRetriesUnauthorizedOnce()
    {
        int requestCount = 0;
        var bearerTokens = new List<string?>();
        var handler = new StubHttpHandler(request =>
        {
            requestCount++;
            bearerTokens.Add(request.Headers.Authorization?.Parameter);
            if (requestCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"item\":{\"id\":\"gallery-1\",\"publishedAt\":\"2026-08-22T00:00:00Z\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var store = new MemorySessionStore
        {
            Credential = ("owner-a", "refresh-original")
        };
        var exchange = new FakeTokenExchange(new FakeClock(DateTimeOffset.UtcNow));
        var client = new XerahSCloudApiClient(new HttpClient(handler), store, exchange, CreateOptions());
        var publish = new XerahSCloudPublishRequest(
            Guid.NewGuid().ToString("D"),
            "https://cdn.example/capture.png",
            null,
            "screenshot",
            "capture.png",
            DateTimeOffset.UtcNow,
            null,
            "image/png");

        await client.PublishAsync(publish);

        Assert.Multiple(() =>
        {
            Assert.That(exchange.RefreshCount, Is.EqualTo(2));
            Assert.That(requestCount, Is.EqualTo(2));
            Assert.That(bearerTokens, Is.EqualTo(new[] { "access-refreshed-1", "access-refreshed-2" }));
            Assert.That(store.Credential?.RefreshToken, Is.EqualTo("refresh-rotated-2"));
        });
    }

    [Test]
    public async Task ApiClient_GetAccountVerifiesAal2SummaryAndBuildsSameOriginUrls()
    {
        var handler = new StubHttpHandler(request =>
        {
            Assert.That(request.RequestUri?.AbsolutePath, Is.EqualTo("/api/v1/me"));
            Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("access"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"slug\":\"owner-name\",\"timeZone\":\"Australia/Perth\",\"strongAuth\":true," +
                    "\"trialStatus\":\"active\",\"trialEndsAt\":\"2026-08-29T00:00:00Z\"," +
                    "\"subscriptionStatus\":null,\"paidThrough\":null,\"canPublish\":true," +
                    "\"disputeSuspended\":false}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var store = new MemorySessionStore
        {
            Current = new XerahSCloudSession("access", "refresh", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5)),
            Credential = ("owner-a", "refresh")
        };
        var client = new XerahSCloudApiClient(
            new HttpClient(handler),
            store,
            new FakeTokenExchange(new FakeClock(DateTimeOffset.UtcNow)),
            CreateOptions());

        XerahSCloudAccountSummary account = await client.GetAccountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(account.Slug, Is.EqualTo("owner-name"));
            Assert.That(account.ProfileUrl, Is.EqualTo(new Uri("https://cloud.xerahs.com/owner-name/")));
            Assert.That(account.SettingsUrl, Is.EqualTo(new Uri("https://cloud.xerahs.com/settings")));
            Assert.That(account.CanPublish, Is.True);
        });
    }

    [Test]
    public void ApiClient_GetAccountRejectsSummaryWithoutStrongAuthentication()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"slug\":\"owner-name\",\"strongAuth\":false,\"canPublish\":false,\"disputeSuspended\":false}",
                Encoding.UTF8,
                "application/json")
        });
        var store = new MemorySessionStore
        {
            Current = new XerahSCloudSession("access", "refresh", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5)),
            Credential = ("owner-a", "refresh")
        };
        var client = new XerahSCloudApiClient(
            new HttpClient(handler),
            store,
            new FakeTokenExchange(new FakeClock(DateTimeOffset.UtcNow)),
            CreateOptions());

        Assert.ThrowsAsync<XerahSCloudSecurityException>(() => client.GetAccountAsync());
    }

    [Test]
    public async Task ApiClient_RestoreSessionClearsCredentialWhenRefreshIsRejected()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                "{\"error\":{\"code\":\"authentication_required\",\"message\":\"The session has expired.\"}}",
                Encoding.UTF8,
                "application/json")
        });
        var store = new MemorySessionStore
        {
            Current = new XerahSCloudSession("expired", "refresh", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5)),
            Credential = ("owner-a", "refresh")
        };
        var exchange = new FakeTokenExchange(new FakeClock(DateTimeOffset.UtcNow))
        {
            RefreshException = new XerahSCloudSecurityException("Refresh rejected.")
        };
        var client = new XerahSCloudApiClient(new HttpClient(handler), store, exchange, CreateOptions());

        bool restored = await client.RestoreSessionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.False);
            Assert.That(exchange.RefreshCount, Is.EqualTo(1));
            Assert.That(store.Current, Is.Null);
            Assert.That(store.Credential, Is.Null);
        });
    }

    [Test]
    public void ApiClient_SignOutClearsMemoryAndPersistedCredential()
    {
        var store = new MemorySessionStore
        {
            Current = new XerahSCloudSession("access", "refresh", "owner-a", DateTimeOffset.UtcNow.AddMinutes(5)),
            Credential = ("owner-a", "refresh")
        };
        var exchange = new FakeTokenExchange(new FakeClock(DateTimeOffset.UtcNow));
        var client = new XerahSCloudApiClient(
            new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            store,
            exchange,
            CreateOptions());

        client.SignOut();

        Assert.Multiple(() =>
        {
            Assert.That(store.Current, Is.Null);
            Assert.That(store.Credential, Is.Null);
            Assert.That(client.HasSessionCredential, Is.False);
        });
    }

    private static string CreateJwt(RSA rsa, object claims)
    {
        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            kid = "key-1",
            typ = "JWT"
        }));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        string signingInput = $"{header}.{payload}";
        byte[] signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Test]
    public void CloudOptions_UnsetEnvironmentUsesCloudPublicClient()
    {
        XerahSCloudOptions options = XerahSCloudOptions.FromValues(null, null, null, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(options.IsOAuthConfigured, Is.True);
            Assert.That(options.ApiBaseAddress, Is.EqualTo(XerahSCloudOptions.CloudApiBaseAddress));
            Assert.That(options.OAuthAuthority, Is.EqualTo(XerahSCloudOptions.CloudOAuthAuthority));
            Assert.That(options.OAuthClientId, Is.EqualTo(XerahSCloudOptions.CloudOAuthClientId));
        });
    }

    [Test]
    public void CloudOptions_ExplicitDisableKeepsDesktopLaunchGated()
    {
        XerahSCloudOptions options = XerahSCloudOptions.FromValues(null, null, null, null, "false");

        Assert.That(options.IsOAuthConfigured, Is.False);
        Assert.That(options.FeatureEnabled, Is.False);
    }

    private static XerahSCloudOptions CreateOptions() => new()
    {
        FeatureEnabled = true,
        OAuthAuthority = new Uri("https://cloud.example.test/"),
        OAuthClientId = "desktop-public-client"
    };

    private sealed class FakeSecretStore(bool isFallback) : ISecretStore, ISecretStoreInfo
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public string BackendName => "test";
        public string BackendDetails => "test";
        public bool IsFallback { get; } = isFallback;

        public string? GetSecret(string providerId, string secretKey, string name) =>
            Values.GetValueOrDefault($"{providerId}:{secretKey}:{name}");

        public void SetSecret(string providerId, string secretKey, string name, string value) =>
            Values[$"{providerId}:{secretKey}:{name}"] = value;

        public void DeleteSecret(string providerId, string secretKey, string name) =>
            Values.Remove($"{providerId}:{secretKey}:{name}");

        public bool HasSecret(string providerId, string secretKey, string name) =>
            Values.ContainsKey($"{providerId}:{secretKey}:{name}");
    }

    private sealed class MemorySessionStore : IXerahSCloudSessionStore
    {
        public XerahSCloudSession? Current { get; set; }
        public (string OwnerSubject, string RefreshToken)? Credential { get; set; }
        public void Accept(XerahSCloudSession session)
        {
            Current = session;
            Credential = (session.OwnerSubject, session.RefreshToken);
        }
        public (string OwnerSubject, string RefreshToken)? ReadRefreshCredential() => Credential;
        public void Clear()
        {
            Current = null;
            Credential = null;
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IXerahSCloudClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTokenExchange(FakeClock clock) : IXerahSCloudOAuthTokenExchange
    {
        public string? LastVerifier { get; private set; }
        public string? LastNonce { get; private set; }
        public int RefreshCount { get; private set; }
        public XerahSCloudSecurityException? RefreshException { get; init; }

        public Task<XerahSCloudSession> ExchangeAsync(
            string code,
            string codeVerifier,
            string expectedNonce,
            CancellationToken cancellationToken)
        {
            LastVerifier = codeVerifier;
            LastNonce = expectedNonce;
            return Task.FromResult(new XerahSCloudSession("access", "refresh", "owner-a", clock.UtcNow.AddMinutes(5)));
        }

        public Task<XerahSCloudSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            RefreshCount++;
            if (RefreshException != null)
            {
                throw RefreshException;
            }
            return Task.FromResult(new XerahSCloudSession(
                $"access-refreshed-{RefreshCount}",
                $"refresh-rotated-{RefreshCount}",
                "owner-a",
                clock.UtcNow.AddMinutes(5)));
        }
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
