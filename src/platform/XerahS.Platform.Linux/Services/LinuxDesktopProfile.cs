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

using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.OmaSnap;

namespace XerahS.Platform.Linux.Services;

/// <summary>Environment facts behind <see cref="LinuxDesktopProfile"/>; detected without running any process.</summary>
internal sealed record LinuxDesktopFacts(
    bool IsWayland,
    bool IsHyprland,
    bool IsOmarchy,
    bool IsSandboxed,
    string? Desktop)
{
    public static LinuxDesktopFacts Detect(Func<string, string?> getEnvironment, Func<string, bool> directoryExists, Func<string, bool> fileExists)
    {
        bool isWayland =
            string.Equals(getEnvironment("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(getEnvironment("WAYLAND_DISPLAY"));
        bool isHyprland = !string.IsNullOrWhiteSpace(getEnvironment("HYPRLAND_INSTANCE_SIGNATURE"));
        bool isOmarchy = !string.IsNullOrWhiteSpace(getEnvironment("OMARCHY_PATH")) || directoryExists("/usr/share/omarchy");
        bool isSandboxed =
            !string.IsNullOrEmpty(getEnvironment("FLATPAK_ID")) || fileExists("/.flatpak-info") ||
            !string.IsNullOrEmpty(getEnvironment("SNAP"));
        string? desktop = getEnvironment("XDG_CURRENT_DESKTOP");
        return new LinuxDesktopFacts(isWayland, isHyprland, isOmarchy, isSandboxed, desktop);
    }

    public static LinuxDesktopFacts DetectCurrent() =>
        Detect(Environment.GetEnvironmentVariable, Directory.Exists, File.Exists);
}

/// <summary>
/// Single source of truth for Omarchy, Hyprland and OmaSnap availability on Linux (XIP0088).
/// Distro names never decide OmaSnap use: the capability probe does. The probe runs at most once
/// per profile, off the UI thread, and only on non-sandboxed Hyprland Wayland sessions with an
/// OmaSnap binary present, so every other system never starts a process for it.
/// </summary>
public sealed class LinuxDesktopProfile : IDesktopEnvironmentProfile
{
    private static readonly Lazy<LinuxDesktopProfile> CurrentProfile = new(() => new LinuxDesktopProfile(
        LinuxDesktopFacts.DetectCurrent(),
        OmaSnapLocator.Resolve,
        (path, token) => new OmaSnapClient(path).ProbeAsync(token)));

    private readonly Func<string?> _locateOmaSnap;
    private readonly Func<string, CancellationToken, Task<OmaSnapCapabilities>> _probe;
    private readonly Lock _probeLock = new();
    private Task<OmaSnapCapabilities>? _probeTask;

    internal LinuxDesktopProfile(
        LinuxDesktopFacts facts,
        Func<string?> locateOmaSnap,
        Func<string, CancellationToken, Task<OmaSnapCapabilities>> probe)
    {
        Facts = facts;
        _locateOmaSnap = locateOmaSnap;
        _probe = probe;
    }

    public static LinuxDesktopProfile Current => CurrentProfile.Value;

    /// <summary>
    /// Applies the development override for the OmaSnap binary from settings. Call before platform
    /// initialization; a changed value drops the cached probe so the next capture re-probes.
    /// </summary>
    public static void ConfigureOmaSnapPathOverride(string? path)
    {
        string? normalized = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        if (string.Equals(OmaSnapLocator.SettingsOverridePath, normalized, StringComparison.Ordinal))
        {
            return;
        }

        OmaSnapLocator.SettingsOverridePath = normalized;
        if (CurrentProfile.IsValueCreated)
        {
            CurrentProfile.Value.ResetOmaSnapProbe();
        }
    }

    internal LinuxDesktopFacts Facts { get; }

    public bool IsOmarchy => Facts.IsOmarchy;
    public bool IsHyprland => Facts.IsHyprland;
    public bool IsWayland => Facts.IsWayland;
    public bool IsSandboxed => Facts.IsSandboxed;

    /// <summary>Probe result, or null while the probe has not completed.</summary>
    internal OmaSnapCapabilities? OmaSnapCapabilities { get; private set; }

    /// <summary>The binary the probe accepted or rejected, or null when none was found.</summary>
    internal string? OmaSnapPath { get; private set; }

    /// <summary>Hyprland and a usable OmaSnap with host mode. False until the probe completes.</summary>
    public bool IsOmarchyLike => IsHyprland && OmaSnapCapabilities?.IsUsable == true;

    /// <summary>Runs the probe once (single flight) and caches the result.</summary>
    internal Task<OmaSnapCapabilities> EnsureOmaSnapProbedAsync(CancellationToken cancellationToken = default)
    {
        lock (_probeLock)
        {
            _probeTask ??= ProbeCoreAsync(cancellationToken);
            return _probeTask;
        }
    }

    /// <summary>Completes the probe if needed and reports whether OmaSnap host mode is usable.</summary>
    public async Task<bool> IsOmaSnapUsableAsync(CancellationToken cancellationToken = default)
    {
        OmaSnapCapabilities capabilities = await EnsureOmaSnapProbedAsync(cancellationToken).ConfigureAwait(false);
        return IsHyprland && capabilities.IsUsable;
    }

    /// <summary>Probe JSON and summary for diagnostics.</summary>
    public async Task<(string Summary, string? ProbeJson)> DescribeOmaSnapAsync(CancellationToken cancellationToken = default)
    {
        OmaSnapCapabilities capabilities = await EnsureOmaSnapProbedAsync(cancellationToken).ConfigureAwait(false);
        return (capabilities.Describe(), capabilities.RawJson);
    }

    /// <summary>Forgets the cached probe, e.g. after the OmaSnap path override changed.</summary>
    internal void ResetOmaSnapProbe()
    {
        lock (_probeLock)
        {
            _probeTask = null;
            OmaSnapCapabilities = null;
            OmaSnapPath = null;
        }
    }

    /// <summary>Starts the probe in the background so capture-time decisions are instant.</summary>
    public void StartBackgroundProbe()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureOmaSnapProbedAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxDesktopProfile: OmaSnap probe failed ({ex.Message}).");
            }
        });
    }

    public string Describe()
    {
        string omaSnap = OmaSnapCapabilities?.Describe() ?? (_probeTask == null ? "OmaSnap not probed" : "OmaSnap probe pending");
        return $"Wayland={IsWayland}, Hyprland={IsHyprland}, Omarchy={IsOmarchy}, Sandboxed={IsSandboxed}, " +
            $"OmarchyLike={IsOmarchyLike}, {omaSnap}{(OmaSnapPath != null ? $" ({OmaSnapPath})" : string.Empty)}";
    }

    private async Task<OmaSnapCapabilities> ProbeCoreAsync(CancellationToken cancellationToken)
    {
        OmaSnapCapabilities result;
        string? path = null;

        if (IsSandboxed)
        {
            result = OmaSnapCapabilities.Unusable("sandboxed session (Flatpak/Snap)");
        }
        else if (!IsWayland)
        {
            result = OmaSnapCapabilities.Unusable("not a Wayland session");
        }
        else if (!IsHyprland)
        {
            result = OmaSnapCapabilities.Unusable("not a Hyprland session");
        }
        else
        {
            path = _locateOmaSnap();
            result = path == null
                ? OmaSnapCapabilities.Unusable("OmaSnap is not installed")
                : await _probe(path, cancellationToken).ConfigureAwait(false);
        }

        OmaSnapPath = path;
        OmaSnapCapabilities = result;

        // A failed probe is a silent fallback plus one diagnostic line, never an error-log entry.
        DebugHelper.WriteLine($"LinuxDesktopProfile: {result.Describe()}{(path != null ? $" [{path}]" : string.Empty)}.");
        return result;
    }
}
