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

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Linux.Capture;

namespace XerahS.Platform.Linux.Capture.Portal;

/// <summary>
/// XDG Desktop Portal screenshot capture. Handles D-Bus connection,
/// request/response, fallback and diagnostics.
/// </summary>
internal static class PortalScreenCapture
{
    public const uint PortalResponseSuccess = 0;
    public const uint PortalResponseCancelled = 1;
    public const uint PortalResponseFailed = 2;

    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    /// <summary>
    /// Timeout for portal response. Prevents indefinite hangs when the backend doesn't respond.
    /// </summary>
    private static readonly TimeSpan PortalResponseTimeout = TimeSpan.FromSeconds(30);

    private static int _portalDiagnosticsLogged;

    /// <summary>
    /// Attempts a portal screenshot. Returns (bitmap, response code).
    /// </summary>
    public static async Task<(SKBitmap? bitmap, uint response)> CaptureAsync(bool forceInteractive, bool allowInteractiveFallback = true)
    {
        try
        {
            LogPortalDiagnosticsOnce();

            using var connection = new Connection(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);

            var portal = connection.CreateProxy<IScreenshotPortal>(PortalBusName, PortalObjectPath);

            var options = new Dictionary<string, object>
            {
                ["modal"] = false,
                ["interactive"] = forceInteractive,
                ["handle_token"] = $"xerahs_{Guid.NewGuid():N}"
            };

            var (bitmap, response) = await TryPortalScreenshotAsync(connection, portal, options).ConfigureAwait(false);
            if (bitmap != null)
            {
                return (bitmap, PortalResponseSuccess);
            }

            if (allowInteractiveFallback && !forceInteractive && response == PortalResponseFailed)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Portal non-interactive capture failed; retrying interactive.");
                options["interactive"] = true;
                options["modal"] = true;
                var (interactiveBitmap, interactiveResponse) = await TryPortalScreenshotAsync(connection, portal, options).ConfigureAwait(false);
                if (interactiveBitmap != null)
                {
                    return (interactiveBitmap, PortalResponseSuccess);
                }

                response = interactiveResponse;
            }

            DebugHelper.WriteLine($"LinuxScreenCaptureService: Portal screenshot cancelled or failed (response={response})");
            return (null, response);
        }
        catch (DBusException ex)
        {
            DebugHelper.WriteException(ex, "LinuxScreenCaptureService: Portal D-Bus error");
            return (null, PortalResponseFailed);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "LinuxScreenCaptureService: Portal capture failed");
            return (null, PortalResponseFailed);
        }
    }

    private static async Task<(SKBitmap? bitmap, uint response)> TryPortalScreenshotAsync(
        Connection connection,
        IScreenshotPortal portal,
        IDictionary<string, object> options)
    {
        var requestStartUtc = DateTime.UtcNow;
        using var monitor = PortalBusMonitor.TryStart("LinuxScreenCaptureService");
        var requestPath = await portal.ScreenshotAsync(string.Empty, options).ConfigureAwait(false);
        var request = connection.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
        using var cts = new CancellationTokenSource(PortalResponseTimeout);
        (uint response, IDictionary<string, object> results) result;
        try
        {
            result = await request.WaitForResponseAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Portal screenshot timed out after {PortalResponseTimeout.TotalSeconds}s (no Response signal received). Falling through to next capture provider.");
            return (null, PortalResponseFailed);
        }

        var (response, results) = result;
        string? uriStr = null;

        if (results != null && results.TryGetResult("uri", out uriStr) && !string.IsNullOrWhiteSpace(uriStr))
        {
            var previewUri = new Uri(uriStr);
            if (previewUri.IsFile && !string.IsNullOrEmpty(previewUri.LocalPath) && File.Exists(previewUri.LocalPath))
            {
                using var previewStream = File.OpenRead(previewUri.LocalPath);
                var previewBitmap = SKBitmap.Decode(previewStream);
                try { File.Delete(previewUri.LocalPath); } catch { }
                return (previewBitmap, 0);
            }
        }

        if (response != 0)
        {
            var fallbackBitmap = await PortalScreenshotFallback
                .TryFindScreenshotAsync(requestStartUtc, TimeSpan.FromSeconds(2), "LinuxScreenCaptureService")
                .ConfigureAwait(false);
            if (fallbackBitmap != null)
            {
                return (fallbackBitmap, 0);
            }

            LogPortalEnvironment();
            DebugHelper.WriteLine("LinuxScreenCaptureService: Portal request options:");
            DebugHelper.WriteLine($"  - interactive: {(options.TryGetValue("interactive", out var interactive) ? interactive : "unset")}");
            DebugHelper.WriteLine($"  - modal: {(options.TryGetValue("modal", out var modal) ? modal : "unset")}");
            DebugHelper.WriteLine($"  - handle_token: {(options.TryGetValue("handle_token", out var token) ? token : "unset")}");
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Portal request failed with response {response}");
            if (results != null)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: Portal response results:");
                foreach (var kvp in results)
                {
                    var valueStr = "null";
                    try
                    {
                        if (kvp.Value != null)
                        {
                            var unwrapped = UnwrapVariant(kvp.Value);
                            valueStr = unwrapped?.ToString() ?? "null";
                        }
                    }
                    catch (Exception ex)
                    {
                        valueStr = $"Error reading value: {ex.Message}";
                    }
                    DebugHelper.WriteLine($"  - {kvp.Key}: {valueStr}");
                }
            }
            return (null, response);
        }

        if (results == null || !results.TryGetResult("uri", out uriStr) || string.IsNullOrWhiteSpace(uriStr))
        {
            var fallbackBitmap = await PortalScreenshotFallback
                .TryFindScreenshotAsync(requestStartUtc, TimeSpan.FromSeconds(2), "LinuxScreenCaptureService")
                .ConfigureAwait(false);
            if (fallbackBitmap != null)
            {
                return (fallbackBitmap, 0);
            }
            DebugHelper.WriteLine("LinuxScreenCaptureService: Portal screenshot missing URI in response");
            return (null, response);
        }

        var uri = new Uri(uriStr);
        if (!uri.IsFile || string.IsNullOrEmpty(uri.LocalPath) || !File.Exists(uri.LocalPath))
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Portal screenshot file not found: {uriStr}");
            return (null, response);
        }

        using var stream = File.OpenRead(uri.LocalPath);
        var bitmap = SKBitmap.Decode(stream);
        try { File.Delete(uri.LocalPath); } catch { }
        return (bitmap, response);
    }

    private static void LogPortalEnvironment()
    {
        DebugHelper.WriteLine("LinuxScreenCaptureService: Portal environment:");
        DebugHelper.WriteLine($"  - XDG_SESSION_TYPE: {Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unset"}");
        DebugHelper.WriteLine($"  - XDG_CURRENT_DESKTOP: {Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unset"}");
        DebugHelper.WriteLine($"  - XDG_SESSION_DESKTOP: {Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ?? "unset"}");
    }

    private static void LogPortalDiagnosticsOnce()
    {
        if (Interlocked.Exchange(ref _portalDiagnosticsLogged, 1) != 0)
        {
            return;
        }
        var runningBackends = GetRunningPortalBackends();
        var routingHint = GetPortalRoutingHint();
        var portalsConfigSummary = GetPortalsConfigSummary();
        DebugHelper.WriteLine("LinuxScreenCaptureService: XDG portal backend diagnostics:");
        DebugHelper.WriteLine($"  - Running backends: {runningBackends}");
        DebugHelper.WriteLine($"  - Routing hint (from desktop session): {routingHint}");
        DebugHelper.WriteLine($"  - portals.conf: {portalsConfigSummary}");
        DebugHelper.WriteLine("  - Note: Portal UI is provided by the selected backend and can differ across desktop environments.");
    }

    private static string GetRunningPortalBackends()
    {
        var running = new List<string>();
        TryAddRunningBackend(running, "xdg-desktop-portal-kde", "kde");
        TryAddRunningBackend(running, "xdg-desktop-portal-gnome", "gnome");
        TryAddRunningBackend(running, "xdg-desktop-portal-gtk", "gtk");
        TryAddRunningBackend(running, "xdg-desktop-portal-wlr", "wlr");
        TryAddRunningBackend(running, "xdg-desktop-portal-hyprland", "hyprland");
        TryAddRunningBackend(running, "xdg-desktop-portal-lxqt", "lxqt");
        return running.Count > 0 ? string.Join(", ", running) : "none detected";
    }

    private static void TryAddRunningBackend(List<string> running, string processName, string label)
    {
        try
        {
            if (Process.GetProcessesByName(processName).Length > 0)
            {
                running.Add(label);
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }
    }

    private static string GetPortalRoutingHint()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ??
                      Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ??
                      string.Empty;
        var normalized = desktop.ToUpperInvariant();
        if (normalized.Contains("KDE") || normalized.Contains("PLASMA")) return "kde";
        if (normalized.Contains("GNOME")) return "gnome";
        if (normalized.Contains("HYPRLAND")) return "hyprland/wlr";
        if (normalized.Contains("SWAY")) return "wlr";
        return "unknown";
    }

    private static string GetPortalsConfigSummary()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            configHome = string.IsNullOrWhiteSpace(userProfile) ? null : Path.Combine(userProfile, ".config");
        }
        var userConfigPath = string.IsNullOrWhiteSpace(configHome)
            ? string.Empty
            : Path.Combine(configHome, "xdg-desktop-portal", "portals.conf");
        var systemConfigPath = "/etc/xdg-desktop-portal/portals.conf";
        var userConfigState = string.IsNullOrWhiteSpace(userConfigPath)
            ? "user=unresolved"
            : $"user={(File.Exists(userConfigPath) ? "present" : "missing")}";
        var systemConfigState = $"system={(File.Exists(systemConfigPath) ? "present" : "missing")}";
        return $"{userConfigState}, {systemConfigState}";
    }

    private static object UnwrapVariant(object value)
    {
        var current = value;
        while (current != null)
        {
            var type = current.GetType();
            var typeName = type.FullName;
            if (typeName != "Tmds.DBus.Protocol.Variant" &&
                typeName != "Tmds.DBus.Protocol.VariantValue" &&
                typeName != "Tmds.DBus.Variant")
            {
                break;
            }
            var valueProp = type.GetProperty("Value");
            var unwrapped = valueProp?.GetValue(current);
            if (unwrapped == null) break;
            current = unwrapped;
        }
        return current ?? value;
    }
}
