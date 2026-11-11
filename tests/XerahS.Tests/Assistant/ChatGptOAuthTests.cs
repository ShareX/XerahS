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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;

namespace XerahS.Tests.Assistant;

public sealed class ChatGptOAuthTests
{
    [Test]
    public void PkceChallengeIsTheSha256OfTheVerifier()
    {
        string verifier = ChatGptOAuth.CreateVerifier();
        Assert.That(verifier, Has.Length.EqualTo(43));
        Assert.That(verifier, Does.Not.Contain("+").And.Not.Contain("/").And.Not.Contain("="));

        string challenge = ChatGptOAuth.Challenge("verifier-value");
        string expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes("verifier-value")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.That(challenge, Is.EqualTo(expected));
    }

    [Test]
    public void AuthorizeUrlRegistersXerahSOnceAndOmitsTheHintOnReturn()
    {
        string first = ChatGptOAuth.BuildAuthorizeUrl(
            "http://127.0.0.1:9/auth/callback", "state", "nonce", "verifier-value", "urn:uuid:host", null);
        Assert.That(first, Does.StartWith("https://auth.openai.com/api/accounts/authorize?"));
        Assert.That(first, Does.Contain("client_id=dynamic_agent_client"));
        Assert.That(first, Does.Contain("agent_name_hint=XerahS"));
        Assert.That(first, Does.Contain("code_challenge_method=S256"));
        Assert.That(first, Does.Contain(Uri.EscapeDataString("https://api.openai.com/v1")));
        Assert.That(first, Does.Contain("redirect_uri=" + Uri.EscapeDataString("http://127.0.0.1:9/auth/callback")));
        Assert.That(first, Does.Not.Contain("id_token_hint"));

        var previous = new ChatGptSession(
            "oaiapp_test", "user-1", "a@example.com", "access", "refresh", "id",
            DateTimeOffset.UnixEpoch.AddHours(1), null, [ChatGptOAuth.PlanScope]);
        string again = ChatGptOAuth.BuildAuthorizeUrl(
            "http://127.0.0.1:9/auth/callback", "state", "nonce", "verifier-value", "urn:uuid:host", previous);
        Assert.That(again, Does.Contain("client_id=oaiapp_test"));
        Assert.That(again, Does.Not.Contain("agent_name_hint"));
        Assert.That(again, Does.Contain("login_hint=" + Uri.EscapeDataString("a@example.com")));
    }

    [Test]
    public void TokenResponseRequiresPlanScopeAndARefreshToken()
    {
        var now = DateTimeOffset.Parse("2026-11-11T00:00:00Z");
        ChatGptSession session = ChatGptOAuth.FromTokenResponse(
            """{"access_token":"at","token_type":"Bearer","expires_in":3600,"refresh_token":"rt","scope":"openid chatgpt.tokens.use.direct"}""",
            "oaiapp_test", "user-1", "a@example.com", "id-token", null, null, now);
        Assert.That(session.CanUsePlan, Is.True);
        Assert.That(session.ExpiresAt, Is.EqualTo(now.AddHours(1)));

        ChatGptSession kept = ChatGptOAuth.FromTokenResponse(
            """{"access_token":"at2","token_type":"bearer","expires_in":60,"scope":"chatgpt.tokens.use.direct"}""",
            "oaiapp_test", "user-1", "a@example.com", "id-token", "old-refresh", [ChatGptOAuth.PlanScope], now);
        Assert.That(kept.RefreshToken, Is.EqualTo("old-refresh"));

        Assert.Throws<ChatGptSignInException>(() => ChatGptOAuth.FromTokenResponse(
            """{"access_token":"at","token_type":"Bearer","expires_in":3600,"refresh_token":"rt","scope":"openid"}""",
            "oaiapp_test", "user-1", "a@example.com", "id-token", null, null, now));
        Assert.Throws<ChatGptSignInException>(() => ChatGptOAuth.FromTokenResponse(
            """{"access_token":"at","token_type":"Bearer","expires_in":3600,"scope":"chatgpt.tokens.use.direct"}""",
            "oaiapp_test", "user-1", "a@example.com", "id-token", null, null, now));
    }

    [Test]
    public void ReadIdentityAcceptsAMatchingRs256Token()
    {
        using RSA rsa = RSA.Create(2048);
        (string token, string jwks) = SignToken(rsa, """{"alg":"RS256","kid":"test-key"}""", Claims());
        (string subject, string email) = ChatGptOAuth.ReadIdentity(token, jwks, "oaiapp_test", "nonce-1", DateTimeOffset.UtcNow);
        Assert.That(subject, Is.EqualTo("user-1"));
        Assert.That(email, Is.EqualTo("a@example.com"));

        Assert.Throws<ChatGptSignInException>(() =>
            ChatGptOAuth.ReadIdentity(token, jwks, "oaiapp_test", "other-nonce", DateTimeOffset.UtcNow));
        (string mismatched, string sameJwks) = SignToken(rsa, """{"alg":"RS256","kid":"test-key"}""", Claims(azp: "other-app"));
        Assert.Throws<ChatGptSignInException>(() =>
            ChatGptOAuth.ReadIdentity(mismatched, sameJwks, "oaiapp_test", "nonce-1", DateTimeOffset.UtcNow));
    }

    [Test]
    public void ResponsesStreamAssemblesDeltasAndRejectsAnUnfinishedBody()
    {
        string body =
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"hel\"}\n" +
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"lo\"}\n" +
            "data: {\"type\":\"response.completed\"}\n";
        Assert.That(ChatGptOAuth.TryReadResponsesStream(body, out string text, out string? error), Is.True);
        Assert.That(text, Is.EqualTo("hello"));
        Assert.That(error, Is.Null);

        Assert.That(ChatGptOAuth.TryReadResponsesStream(
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"hel\"}\n", out _, out error), Is.False);
        Assert.That(error, Is.EqualTo("ChatGPT did not finish the response."));

        Assert.That(ChatGptOAuth.TryReadResponsesStream(
            "data: {\"type\":\"response.failed\"}\n", out _, out error), Is.False);
        Assert.That(error, Is.EqualTo("ChatGPT could not complete the request."));
    }

    [Test]
    public async Task GrokRequestsLowReasoningAndAnApiKeyDoesNotStream()
    {
        string grok = await RequestBody("grok", stream: false);
        Assert.That(grok, Does.Contain("\"reasoning\":{\"effort\":\"low\"}"));
        Assert.That(grok, Does.Not.Contain("\"stream\""));

        string openai = await RequestBody("openai", stream: false);
        Assert.That(openai, Does.Not.Contain("\"stream\""));
        Assert.That(openai, Does.Not.Contain("reasoning"));
        Assert.That(openai, Does.Contain("\"store\":false"));
    }

    [Test]
    public async Task ChatGptPlanUsageStreamsAndUsesTheRefreshedToken()
    {
        var metadata = AssistantProviderCatalog.Find("openai")!;
        var handler = new RecordingHandler(
            """
            data: {"type":"response.output_text.delta","delta":"hel"}
            data: {"type":"response.output_text.delta","delta":"lo"}
            data: {"type":"response.completed"}
            """);
        using var client = new HttpClient(handler);
        var provider = new HttpAssistantModelProvider(
            new(metadata, "gpt-5.4", "https://api.openai.com/v1", "stale-token", true),
            client,
            _ => Task.FromResult<string?>("fresh-token"));

        AssistantModelResult result = await provider.CompleteAsync(
            new("openai", "gpt-5.4", [new(AssistantModelMessageRole.User, "ok")], [], AssistantPrivacyScope.CloudText, false, 80),
            CancellationToken.None);

        Assert.That(result.Kind, Is.EqualTo(AssistantModelResultKind.Text));
        Assert.That(result.Text, Is.EqualTo("hello"));
        Assert.That(handler.Body, Does.Contain("\"stream\":true").And.Contain("\"store\":false"));
        Assert.That(handler.Authorization, Is.EqualTo("Bearer fresh-token"));
    }

    private static async Task<string> RequestBody(string providerId, bool stream)
    {
        var metadata = AssistantProviderCatalog.Find(providerId)!;
        var provider = new HttpAssistantModelProvider(new(metadata, metadata.DefaultModelId, metadata.DefaultBaseUrl, "synthetic-key", stream));
        using HttpRequestMessage message = provider.BuildRequest(new(
            providerId, metadata.DefaultModelId, [new(AssistantModelMessageRole.User, "ok")], [],
            AssistantPrivacyScope.CloudText, false, 80));
        return await message.Content!.ReadAsStringAsync();
    }

    private static string Claims(string azp = "oaiapp_test")
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.Serialize(new
        {
            iss = "https://auth.openai.com",
            aud = "oaiapp_test",
            exp = now + 600,
            iat = now,
            sub = "user-1",
            nonce = "nonce-1",
            email = "a@example.com",
            azp
        });
    }

    private static (string Token, string Jwks) SignToken(RSA rsa, string headerJson, string payloadJson)
    {
        RSAParameters parameters = rsa.ExportParameters(false);
        string n = Base64Url(parameters.Modulus!);
        string e = Base64Url(parameters.Exponent!);
        string header = Base64Url(Encoding.UTF8.GetBytes(headerJson));
        string payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        byte[] signature = rsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return (header + "." + payload + "." + Base64Url(signature),
            "{\"keys\":[{\"kty\":\"RSA\",\"kid\":\"test-key\",\"n\":\"" + n + "\",\"e\":\"" + e + "\"}]}");
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _body;
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }

        public RecordingHandler(string body) => _body = body;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "text/event-stream")
            };
        }
    }
}
