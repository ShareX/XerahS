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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using XerahS.Assistant.Configuration;
using XerahS.Common;
using XerahS.Core;

namespace XerahS.Assistant.Providers;

/// <summary>
/// One Sign in with ChatGPT registration for this installation.
/// Tokens stay in the secret store. This record is the in-memory copy.
/// </summary>
internal sealed record ChatGptSession(
    string ClientId,
    string Subject,
    string Email,
    string AccessToken,
    string RefreshToken,
    string IdToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EarliestRefreshAt,
    IReadOnlyList<string> Scopes)
{
    public bool CanUsePlan =>
        Scopes.Contains(ChatGptOAuth.PlanScope, StringComparer.Ordinal) &&
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(RefreshToken);
}

public sealed class ChatGptSignInException : Exception
{
    public ChatGptSignInException(string message) : base(message) { }
}

/// <summary>
/// Settings entry points for Continue with ChatGPT. Tokens stay in the secret store.
/// </summary>
public static class ChatGptSignIn
{
    public static bool IsSignedIn => ChatGptSessionStore.TryGetUsable(out _);

    public static string Email
    {
        get
        {
            ChatGptSession? session = ChatGptSessionStore.Load();
            return session != null && session.CanUsePlan ? session.Email : string.Empty;
        }
    }

    public static async Task<string> SignInAsync(CancellationToken cancellationToken)
    {
        ChatGptSession session = await ChatGptOAuth.SignInAsync(cancellationToken);
        return session.Email;
    }

    public static void SignOut() => ChatGptSessionStore.Delete();
}

/// <summary>
/// OpenAI's open-source Sign in with ChatGPT flow. The app registers itself as
/// XerahS during the first sign-in and then calls the public Responses API with
/// the user's ChatGPT plan. There is no client secret.
/// </summary>
internal static class ChatGptOAuth
{
    public const string Issuer = "https://auth.openai.com";
    public const string Resource = "https://api.openai.com/v1";
    public const string AgentName = "XerahS";
    public const string PlanScope = "chatgpt.tokens.use.direct";
    public const string Scopes = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
    private const string CallbackPath = "/auth/callback";
    private static readonly Regex ClientIdPattern = new("^[A-Za-z0-9_-]{1,200}$", RegexOptions.CultureInvariant);

    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Challenge(string verifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    public static string BuildAuthorizeUrl(
        string redirectUri,
        string state,
        string nonce,
        string verifier,
        string hostId,
        ChatGptSession? previous)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = previous?.ClientId ?? "dynamic_agent_client",
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = Scopes,
            ["resource"] = Resource,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = Challenge(verifier),
            ["ext_agent_host_id"] = hostId
        };
        if (previous == null)
        {
            query["agent_name_hint"] = AgentName;
        }
        else if (!string.IsNullOrWhiteSpace(previous.Email))
        {
            query["login_hint"] = previous.Email;
        }

        return Issuer + "/api/accounts/authorize?" + string.Join("&",
            query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    public static ChatGptSession FromTokenResponse(
        string json,
        string clientId,
        string subject,
        string email,
        string idToken,
        string? previousRefresh,
        IReadOnlyList<string>? previousScopes,
        DateTimeOffset now)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string access = Required(root, "access_token");
        string tokenType = Required(root, "token_type");
        if (!string.Equals(tokenType, "bearer", StringComparison.OrdinalIgnoreCase))
        {
            throw new ChatGptSignInException("ChatGPT returned an unexpected token type.");
        }

        if (!root.TryGetProperty("expires_in", out JsonElement expiresElement) ||
            !expiresElement.TryGetInt32(out int expiresIn) || expiresIn <= 0)
        {
            throw new ChatGptSignInException("ChatGPT did not say when the sign-in expires.");
        }

        string refresh = root.TryGetProperty("refresh_token", out JsonElement refreshElement) &&
            refreshElement.ValueKind == JsonValueKind.String
            ? refreshElement.GetString() ?? string.Empty
            : previousRefresh ?? string.Empty;
        if (string.IsNullOrWhiteSpace(refresh))
        {
            throw new ChatGptSignInException("ChatGPT did not return a refresh token. Sign in again.");
        }

        IReadOnlyList<string> scopes = root.TryGetProperty("scope", out JsonElement scopeElement) &&
            scopeElement.ValueKind == JsonValueKind.String
            ? scopeElement.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : previousScopes ?? [];
        if (!scopes.Contains(PlanScope, StringComparer.Ordinal))
        {
            throw new ChatGptSignInException("ChatGPT plan use was not allowed. Approve it in the browser, or save an API key instead.");
        }

        return new ChatGptSession(
            clientId,
            subject,
            email,
            access,
            refresh,
            idToken,
            now.AddSeconds(expiresIn),
            ReadEarliestRefresh(root),
            scopes);
    }

    public static (string Subject, string Email) ReadIdentity(string idToken, string jwksJson, string audience, string? nonce, DateTimeOffset now)
    {
        string[] parts = idToken.Split('.');
        if (parts.Length != 3)
        {
            throw new ChatGptSignInException("ChatGPT returned an identity that could not be checked.");
        }

        using JsonDocument header = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
        if (!header.RootElement.TryGetProperty("alg", out JsonElement alg) ||
            !string.Equals(alg.GetString(), "RS256", StringComparison.Ordinal) ||
            !header.RootElement.TryGetProperty("kid", out JsonElement kidElement) ||
            string.IsNullOrWhiteSpace(kidElement.GetString()))
        {
            throw new ChatGptSignInException("ChatGPT returned an identity that could not be checked.");
        }

        string kid = kidElement.GetString()!;
        if (!TryGetRsa(jwksJson, kid, out RSA? rsa) || rsa == null)
        {
            throw new ChatGptSignInException("ChatGPT identity verification is temporarily unavailable. Try again shortly.");
        }

        using (rsa)
        {
            byte[] data = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
            byte[] signature = Base64UrlDecode(parts[2]);
            if (!rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                throw new ChatGptSignInException("The ChatGPT identity could not be verified. Sign in again.");
            }
        }

        using JsonDocument payload = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
        JsonElement claims = payload.RootElement;
        if (!string.Equals(ClaimString(claims, "iss"), Issuer, StringComparison.Ordinal))
        {
            throw new ChatGptSignInException("The ChatGPT identity could not be verified. Sign in again.");
        }

        if (!AudienceMatches(claims, audience))
        {
            throw new ChatGptSignInException("The ChatGPT identity could not be verified. Sign in again.");
        }

        if (claims.TryGetProperty("azp", out JsonElement azp) &&
            (azp.ValueKind != JsonValueKind.String || !string.Equals(azp.GetString(), audience, StringComparison.Ordinal)))
        {
            throw new ChatGptSignInException("The ChatGPT identity could not be verified. Sign in again.");
        }

        if (!claims.TryGetProperty("exp", out JsonElement expElement) || !expElement.TryGetInt64(out long exp) ||
            !claims.TryGetProperty("iat", out JsonElement iatElement) || iatElement.ValueKind != JsonValueKind.Number ||
            DateTimeOffset.FromUnixTimeSeconds(exp) < now.AddSeconds(-5))
        {
            throw new ChatGptSignInException("The ChatGPT identity could not be verified. Sign in again.");
        }

        if (nonce != null && !string.Equals(claims.TryGetProperty("nonce", out JsonElement nonceElement) ? nonceElement.GetString() : null, nonce, StringComparison.Ordinal))
        {
            throw new ChatGptSignInException("The ChatGPT identity could not be verified. Sign in again.");
        }

        string subject = ClaimString(claims, "sub");

        string email = claims.TryGetProperty("email", out JsonElement emailElement) && emailElement.ValueKind == JsonValueKind.String
            ? emailElement.GetString() ?? string.Empty
            : string.Empty;
        return (subject, email);
    }

    public static bool TryReadResponsesStream(string body, out string text, out string? error)
    {
        var assembled = new StringBuilder();
        bool completed = false;
        error = null;
        foreach (string raw in body.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            string data = line["data:".Length..].Trim();
            if (data.Length == 0 || data == "[DONE]")
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(data);
            string? type = document.RootElement.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() : null;
            if (type == "response.output_text.delta" &&
                document.RootElement.TryGetProperty("delta", out JsonElement delta) &&
                delta.ValueKind == JsonValueKind.String)
            {
                assembled.Append(delta.GetString());
            }
            else if (type == "response.output_text.done" &&
                document.RootElement.TryGetProperty("text", out JsonElement done) &&
                done.ValueKind == JsonValueKind.String)
            {
                assembled.Clear();
                assembled.Append(done.GetString());
            }
            else if (type == "response.completed")
            {
                completed = true;
                if (assembled.Length == 0 &&
                    document.RootElement.TryGetProperty("response", out JsonElement response) &&
                    response.TryGetProperty("output_text", out JsonElement output) &&
                    output.ValueKind == JsonValueKind.String)
                {
                    assembled.Append(output.GetString());
                }
            }
            else if (type is "response.failed" or "error")
            {
                error = "ChatGPT could not complete the request.";
                text = string.Empty;
                return false;
            }
        }

        text = assembled.ToString();
        if (!completed)
        {
            error = "ChatGPT did not finish the response.";
            return false;
        }

        return true;
    }

    public static async Task<ChatGptSession> SignInAsync(CancellationToken cancellationToken)
    {
        string hostId = EnsureHostId();
        ChatGptSession? previous = ChatGptSessionStore.Load();
        string state = CreateVerifier();
        string nonce = CreateVerifier();
        string verifier = CreateVerifier();

        using HttpListener listener = new();
        int port = BindCallback(listener);
        string redirect = $"http://127.0.0.1:{port}{CallbackPath}";
        string url = BuildAuthorizeUrl(redirect, state, nonce, verifier, hostId, previous);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            throw new ChatGptSignInException("The browser could not be opened. Check the default browser and try again.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            throw new ChatGptSignInException(cancellationToken.IsCancellationRequested
                ? "Sign-in was cancelled."
                : "ChatGPT sign-in timed out. Try again.");
        }

        string query = context.Request.Url?.Query ?? string.Empty;
        WriteCallbackPage(context.Response);
        Dictionary<string, List<string>> parameters = ParseQuery(query);
        if (!parameters.TryGetValue("state", out List<string>? states) || states.Count != 1 || !FixedEquals(states[0], state))
        {
            throw new ChatGptSignInException("The ChatGPT sign-in did not match this attempt. Try again.");
        }

        if (parameters.TryGetValue("error", out List<string>? errors) && !string.IsNullOrEmpty(errors.FirstOrDefault()))
        {
            throw new ChatGptSignInException(errors[0] == "access_denied"
                ? "ChatGPT sign-in was cancelled."
                : "ChatGPT could not complete sign-in.");
        }

        string? code = parameters.TryGetValue("code", out List<string>? codes) && codes.Count == 1 ? codes[0] : null;
        string? returnedClient = null;
        if (parameters.TryGetValue("client_id", out List<string>? clients))
        {
            if (clients.Count != 1)
            {
                throw new ChatGptSignInException("ChatGPT did not finish registering XerahS. Try signing in again.");
            }

            returnedClient = clients[0];
        }

        string clientId = returnedClient ?? previous?.ClientId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code) ||
            !ClientIdPattern.IsMatch(clientId) ||
            clientId == "dynamic_agent_client" ||
            (previous != null && returnedClient != null && previous.ClientId != returnedClient))
        {
            throw new ChatGptSignInException("ChatGPT did not finish registering XerahS. Try signing in again.");
        }

        using HttpClient http = HttpClientFactory.Create(allowAutoRedirect: false, infiniteTimeout: false);
        string tokenJson = await PostFormAsync(http, await TokenEndpointAsync(http, timeout.Token), new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["resource"] = Resource
        }, timeout.Token);

        using JsonDocument token = JsonDocument.Parse(tokenJson);
        string idToken = token.RootElement.TryGetProperty("id_token", out JsonElement idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ChatGptSignInException("ChatGPT did not return a verifiable identity. Sign in again.");
        }

        (string subject, string email) = ReadIdentity(idToken, await JwksAsync(http, timeout.Token), clientId, nonce, DateTimeOffset.UtcNow);
        if (previous != null && !string.Equals(previous.Subject, subject, StringComparison.Ordinal))
        {
            throw new ChatGptSignInException("This sign-in returned a different ChatGPT account. Sign out first, then sign in to the other account.");
        }

        ChatGptSession session = FromTokenResponse(tokenJson, clientId, subject, email, idToken, previous?.RefreshToken, previous?.Scopes, DateTimeOffset.UtcNow);
        ChatGptSessionStore.Save(session);
        return session;
    }

    public static async Task<ChatGptSession?> EnsureFreshAsync(CancellationToken cancellationToken)
    {
        if (!ChatGptSessionStore.TryGetUsable(out ChatGptSession session))
        {
            return null;
        }

        if (session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return session;
        }

        if (session.EarliestRefreshAt is DateTimeOffset earliest && earliest > DateTimeOffset.UtcNow)
        {
            return null;
        }

        using HttpClient http = HttpClientFactory.Create(allowAutoRedirect: false, infiniteTimeout: false);
        string tokenJson = await PostFormAsync(http, await TokenEndpointAsync(http, cancellationToken), new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = session.ClientId,
            ["refresh_token"] = session.RefreshToken,
            ["resource"] = Resource
        }, cancellationToken);

        using JsonDocument token = JsonDocument.Parse(tokenJson);
        string idToken = token.RootElement.TryGetProperty("id_token", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString() ?? session.IdToken
            : session.IdToken;
        (string subject, string email) = string.Equals(idToken, session.IdToken, StringComparison.Ordinal)
            ? (session.Subject, session.Email)
            : ReadIdentity(idToken, await JwksAsync(http, cancellationToken), session.ClientId, nonce: null, DateTimeOffset.UtcNow);
        if (!string.Equals(subject, session.Subject, StringComparison.Ordinal))
        {
            ChatGptSessionStore.Delete();
            return null;
        }

        ChatGptSession refreshed = FromTokenResponse(
            tokenJson, session.ClientId, subject,
            string.IsNullOrWhiteSpace(email) ? session.Email : email,
            idToken, session.RefreshToken, session.Scopes, DateTimeOffset.UtcNow);
        ChatGptSessionStore.Save(refreshed);
        return refreshed;
    }

    private static string EnsureHostId()
    {
        if (!string.IsNullOrWhiteSpace(SettingsManager.Settings.AssistantChatGptHostId))
        {
            return SettingsManager.Settings.AssistantChatGptHostId;
        }

        string hostId = "urn:uuid:" + Guid.NewGuid().ToString("D");
        SettingsManager.Settings.AssistantChatGptHostId = hostId;
        SettingsManager.SaveApplicationConfig();
        return hostId;
    }

    private static int BindCallback(HttpListener listener)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
            try
            {
                listener.Prefixes.Add($"http://127.0.0.1:{port}{CallbackPath}/");
                listener.Start();
                return port;
            }
            catch (HttpListenerException)
            {
                listener.Prefixes.Clear();
            }
        }

        throw new ChatGptSignInException("The sign-in port is unavailable. Close another copy of XerahS and try again.");
    }

    private static void WriteCallbackPage(HttpListenerResponse response)
    {
        byte[] body = Encoding.UTF8.GetBytes(
            "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Return to XerahS</title>" +
            "<body style=\"font:17px system-ui;max-width:32rem;margin:18vh auto\"><h1>Return to XerahS</h1>" +
            "<p>XerahS will finish checking the ChatGPT connection. You can close this tab.</p></body></html>");
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.ContentLength64 = body.Length;
        response.OutputStream.Write(body);
        response.OutputStream.Close();
    }

    private static async Task<string> TokenEndpointAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(Issuer + "/.well-known/openid-configuration", cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ChatGptSignInException("ChatGPT sign-in configuration could not be verified. Try again shortly.");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        if (!string.Equals(root.GetProperty("issuer").GetString(), Issuer, StringComparison.Ordinal) ||
            !IsIssuerUrl(root, "authorization_endpoint") ||
            !IsIssuerUrl(root, "token_endpoint") ||
            !IsIssuerUrl(root, "jwks_uri"))
        {
            throw new ChatGptSignInException("ChatGPT sign-in configuration could not be verified. Try again shortly.");
        }

        return root.GetProperty("token_endpoint").GetString()!;
    }

    private static async Task<string> JwksAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using HttpResponseMessage discovery = await http.GetAsync(Issuer + "/.well-known/openid-configuration", cancellationToken);
        using JsonDocument document = JsonDocument.Parse(await discovery.Content.ReadAsStringAsync(cancellationToken));
        string jwksUri = document.RootElement.GetProperty("jwks_uri").GetString() ?? string.Empty;
        if (!IsOnIssuer(jwksUri))
        {
            throw new ChatGptSignInException("ChatGPT identity verification is temporarily unavailable. Try again shortly.");
        }

        return await http.GetStringAsync(jwksUri, cancellationToken);
    }

    private static async Task<string> PostFormAsync(HttpClient http, string url, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(fields);
        using HttpResponseMessage response = await http.PostAsync(url, content, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ChatGptSignInException("ChatGPT could not complete the request. Try again.");
        }

        return body;
    }

    private static bool IsIssuerUrl(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && IsOnIssuer(value.GetString());

    private static bool IsOnIssuer(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) &&
        string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(parsed.Host, "auth.openai.com", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ReadEarliestRefresh(JsonElement root)
    {
        if (!root.TryGetProperty("earliest_refresh_at", out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long unix))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string ClaimString(JsonElement claims, string name)
    {
        if (!claims.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ChatGptSignInException("The ChatGPT identity could not be verified. Sign in again.");
        }

        return value.GetString()!;
    }

    private static Dictionary<string, List<string>> ParseQuery(string query)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string trimmed = query.TrimStart('?');
        if (trimmed.Length == 0)
        {
            return result;
        }

        foreach (string part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = part.IndexOf('=');
            string key = Uri.UnescapeDataString(separator < 0 ? part : part[..separator]);
            string value = separator < 0 ? string.Empty : Uri.UnescapeDataString(part[(separator + 1)..]);
            if (!result.TryGetValue(key, out List<string>? values))
            {
                values = [];
                result[key] = values;
            }

            values.Add(value);
        }

        return result;
    }

    private static string Required(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ChatGptSignInException("ChatGPT returned incomplete credentials. Sign in again.");
        }

        return value.GetString()!;
    }

    private static bool AudienceMatches(JsonElement claims, string audience)
    {
        if (!claims.TryGetProperty("aud", out JsonElement aud))
        {
            return false;
        }

        if (aud.ValueKind == JsonValueKind.String)
        {
            return string.Equals(aud.GetString(), audience, StringComparison.Ordinal);
        }

        return aud.ValueKind == JsonValueKind.Array && aud.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String && string.Equals(item.GetString(), audience, StringComparison.Ordinal));
    }

    private static bool TryGetRsa(string jwksJson, string kid, out RSA? rsa)
    {
        rsa = null;
        using JsonDocument document = JsonDocument.Parse(jwksJson);
        foreach (JsonElement key in document.RootElement.GetProperty("keys").EnumerateArray())
        {
            if (!string.Equals(key.GetProperty("kid").GetString(), kid, StringComparison.Ordinal) ||
                !string.Equals(key.GetProperty("kty").GetString(), "RSA", StringComparison.Ordinal))
            {
                continue;
            }

            var parameters = new RSAParameters
            {
                Modulus = Base64UrlDecode(key.GetProperty("n").GetString()!),
                Exponent = Base64UrlDecode(key.GetProperty("e").GetString()!)
            };
            rsa = RSA.Create();
            rsa.ImportParameters(parameters);
            return true;
        }

        return false;
    }

    private static bool FixedEquals(string? left, string right)
    {
        byte[] a = Encoding.UTF8.GetBytes(left ?? string.Empty);
        byte[] b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };
        return Convert.FromBase64String(padded);
    }
}

internal static class ChatGptSessionStore
{
    public static bool TryGetUsable(out ChatGptSession session)
    {
        ChatGptSession? loaded = Load();
        if (loaded != null && loaded.CanUsePlan)
        {
            session = loaded;
            return true;
        }

        session = null!;
        return false;
    }

    public static ChatGptSession? Load()
    {
        string? json = AssistantProviderSecrets.GetNamedSecret("openai", AssistantProviderSecrets.ChatGptSessionName);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            Stored? stored = JsonSerializer.Deserialize<Stored>(json);
            if (stored == null || string.IsNullOrWhiteSpace(stored.ClientId) || string.IsNullOrWhiteSpace(stored.AccessToken))
            {
                return null;
            }

            return new ChatGptSession(
                stored.ClientId,
                stored.Subject ?? string.Empty,
                stored.Email ?? string.Empty,
                stored.AccessToken,
                stored.RefreshToken ?? string.Empty,
                stored.IdToken ?? string.Empty,
                stored.ExpiresAt,
                stored.EarliestRefreshAt,
                stored.Scopes ?? []);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Save(ChatGptSession session)
    {
        string json = JsonSerializer.Serialize(new Stored
        {
            ClientId = session.ClientId,
            Subject = session.Subject,
            Email = session.Email,
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            IdToken = session.IdToken,
            ExpiresAt = session.ExpiresAt,
            EarliestRefreshAt = session.EarliestRefreshAt,
            Scopes = session.Scopes.ToArray()
        });
        AssistantProviderSecrets.SetNamedSecret("openai", AssistantProviderSecrets.ChatGptSessionName, json);
    }

    public static void Delete() =>
        AssistantProviderSecrets.DeleteNamedSecret("openai", AssistantProviderSecrets.ChatGptSessionName);

    private sealed class Stored
    {
        public string ClientId { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string? Email { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public string? IdToken { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? EarliestRefreshAt { get; set; }
        public string[]? Scopes { get; set; }
    }
}
