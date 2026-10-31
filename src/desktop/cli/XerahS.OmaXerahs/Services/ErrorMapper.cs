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

using System.Net.Http;
using XerahS.OmaXerahs.Models;
using XerahS.Uploaders;

namespace XerahS.OmaXerahs.Services;

internal static class ErrorMapper
{
    internal static (string Code, string Message) FromException(Exception ex, bool timeoutRequested = false)
    {
        if (ex is XerahS.Core.Automation.AutomationException automation)
        {
            return (automation.Code, automation.Message);
        }

        if (ex is TimeoutException)
        {
            return (CliErrorCodes.Timeout, ex.Message);
        }

        if (ex is OperationCanceledException)
        {
            return timeoutRequested
                ? (CliErrorCodes.Timeout, "Upload timed out after 5 minutes.")
                : (CliErrorCodes.Cancelled, ex.Message);
        }

        if (ex is HttpRequestException)
        {
            return (CliErrorCodes.Network, ex.Message);
        }

        if (ex is UnauthorizedAccessException)
        {
            return (CliErrorCodes.Auth, ex.Message);
        }

        if (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return (CliErrorCodes.InvalidPath, ex.Message);
        }

        string text = ex.Message ?? string.Empty;
        string code = Classify(text, timeoutRequested);
        return (code, string.IsNullOrWhiteSpace(text) ? ex.GetType().Name : text);
    }

    internal static (string Code, string Message) FromUploadResult(UploadResult? result)
    {
        string message = result?.Errors?.ToString() ?? result?.Response ?? "Upload failed.";
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Upload failed.";
        }

        return (Classify(message, timeoutRequested: false), message);
    }

    internal static string Classify(string message, bool timeoutRequested)
    {
        if (timeoutRequested || ContainsAny(message, "timed out", "timeout", "lock wait"))
        {
            return CliErrorCodes.Timeout;
        }

        if (ContainsAny(message, "cancel"))
        {
            return CliErrorCodes.Cancelled;
        }

        if (ContainsAny(message, "unauthorized", "forbidden", "oauth", "401", "403", "authentication", "not authenticated"))
        {
            return CliErrorCodes.Auth;
        }

        if (ContainsAny(message, "secret store", "libsecret", "secret-tool", "keychain", "dpapi"))
        {
            return CliErrorCodes.SecretStore;
        }

        if (ContainsAny(message, "no uploader", "not configured", "no usable", "not ready"))
        {
            return CliErrorCodes.NotReady;
        }

        if (ContainsAny(message, "network", "connection", "name or service not known", "socket", "dns", "http request", "ssl", "tls"))
        {
            return CliErrorCodes.Network;
        }

        if (ContainsAny(message, "incompatible", "platform"))
        {
            return CliErrorCodes.Incompatible;
        }

        if (ContainsAny(message, "file not found", "may have been moved", "no such file"))
        {
            return CliErrorCodes.InvalidPath;
        }

        return CliErrorCodes.Provider;
    }

    internal static bool IsHttpUrl(string? url)
    {
        return url != null &&
               (url.StartsWith("http://", StringComparison.Ordinal) ||
                url.StartsWith("https://", StringComparison.Ordinal));
    }

    private static bool ContainsAny(string message, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (message.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
