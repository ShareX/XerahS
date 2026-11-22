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

using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace XerahS.UI.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public string AvaloniaVersion { get; }

    public string? AvaloniaCommit { get; }

    public string AvaloniaRepositoryUrl { get; }

    public bool IsCustomAvaloniaFork { get; }

    public string AvaloniaSummary { get; }

    public string AvaloniaDisplayText { get; }

    public string WindowingBackend { get; }

    public string SkiaSharpVersion { get; }

    public string DotNetRuntime { get; }

    public IReadOnlyList<LoadedAssemblyInfoViewModel> XerahSAssemblies { get; }

    public IReadOnlyList<LoadedAssemblyInfoViewModel> SystemAssemblies { get; }

    public IReadOnlyList<LoadedAssemblyInfoViewModel> ThirdPartyAssemblies { get; }

    public int LoadedAssemblyCount => XerahSAssemblyCount + SystemAssemblyCount + ThirdPartyAssemblyCount;

    public int XerahSAssemblyCount => XerahSAssemblies.Count;

    public int SystemAssemblyCount => SystemAssemblies.Count;

    public int ThirdPartyAssemblyCount => ThirdPartyAssemblies.Count;

    public AboutViewModel()
    {
        Assembly avaloniaAssembly = typeof(global::Avalonia.Application).Assembly;
        Assembly skiaAssembly = typeof(SkiaSharp.SKBitmap).Assembly;

        AvaloniaVersion = FormatAssemblyVersion(avaloniaAssembly);
        AvaloniaCommit = ExtractShortCommit(avaloniaAssembly);
        string? repoUrl = GetRepositoryUrl(avaloniaAssembly);

        IsCustomAvaloniaFork =
            AvaloniaVersion.Contains("-sharex", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(repoUrl) && repoUrl.Contains("ShareX/Avalonia", StringComparison.OrdinalIgnoreCase));

        AvaloniaRepositoryUrl = !string.IsNullOrEmpty(repoUrl)
            ? repoUrl
            : (IsCustomAvaloniaFork ? "https://github.com/ShareX/Avalonia" : "https://github.com/AvaloniaUI/Avalonia");

        AvaloniaSummary = IsCustomAvaloniaFork
            ? $"Avalonia {AvaloniaVersion} (ShareX Fork)"
            : $"Avalonia {AvaloniaVersion}";

        AvaloniaDisplayText = BuildAvaloniaDisplayText(AvaloniaVersion, AvaloniaCommit, IsCustomAvaloniaFork);
        WindowingBackend = DetectWindowingBackend();
        SkiaSharpVersion = FormatAssemblyVersion(skiaAssembly);
        DotNetRuntime = RuntimeInformation.FrameworkDescription;

        var groupedAssemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(CreateAssemblyInfo)
            .ToArray();

        XerahSAssemblies = groupedAssemblies
            .Where(info => info.Group == LoadedAssemblyGroup.XerahS)
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SystemAssemblies = groupedAssemblies
            .Where(info => info.Group == LoadedAssemblyGroup.System)
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ThirdPartyAssemblies = groupedAssemblies
            .Where(info => info.Group == LoadedAssemblyGroup.ThirdParty)
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static LoadedAssemblyInfoViewModel CreateAssemblyInfo(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        var name = assemblyName.Name ?? "(Unknown)";

        return new LoadedAssemblyInfoViewModel(
            name,
            ClassifyAssembly(name),
            FormatAssemblyVersion(assembly));
    }

    private static string FormatAssemblyVersion(Assembly assembly)
    {
        string? infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return FormatAssemblyVersion(infoVersion, assembly.GetName().Version);
    }

    internal static string FormatAssemblyVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            int plusIndex = informationalVersion.IndexOf('+');
            string cleanVersion = (plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion).Trim();
            if (!string.IsNullOrEmpty(cleanVersion))
            {
                return cleanVersion;
            }
        }

        return FormatVersion(assemblyVersion);
    }

    private static string? ExtractShortCommit(Assembly assembly)
    {
        string? infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(infoVersion))
        {
            return null;
        }

        int plusIndex = infoVersion.IndexOf('+');
        if (plusIndex < 0 || plusIndex + 1 >= infoVersion.Length)
        {
            return null;
        }

        string metadata = infoVersion[(plusIndex + 1)..].Trim();
        int dotIndex = metadata.IndexOf('.');
        if (dotIndex > 0)
        {
            metadata = metadata[..dotIndex];
        }

        if (metadata.Length == 0)
        {
            return null;
        }

        return metadata.Length > 8 ? metadata[..8] : metadata;
    }

    private static string? GetRepositoryUrl(Assembly assembly)
    {
        string? url = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => string.Equals(attr.Key, "RepositoryUrl", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        url = url.Trim().TrimEnd('/');
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            url = url[..^4];
        }

        return url;
    }

    private static string BuildAvaloniaDisplayText(string version, string? commit, bool isCustomFork)
    {
        if (isCustomFork)
        {
            return !string.IsNullOrEmpty(commit)
                ? $"Avalonia {version} (ShareX Fork · {commit})"
                : $"Avalonia {version} (ShareX Fork)";
        }

        return !string.IsNullOrEmpty(commit)
            ? $"Avalonia {version} ({commit})"
            : $"Avalonia {version}";
    }

    private static string DetectWindowingBackend()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Win32";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS (AppKit)";
        }

        if (OperatingSystem.IsLinux())
        {
            var mainWindow = (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow != null)
            {
                if (WaylandProperties.IsWaylandBackend(mainWindow))
                {
                    return WaylandProperties.IsLayerShellSupported(mainWindow)
                        ? "Wayland (zwlr_layer_shell_v1)"
                        : "Wayland";
                }

                string? descriptor = mainWindow.TryGetPlatformHandle()?.HandleDescriptor;
                if (string.Equals(descriptor, "XID", StringComparison.OrdinalIgnoreCase))
                {
                    return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                        ? "X11 (XWayland)"
                        : "X11";
                }
            }

            if (AppDomain.CurrentDomain.GetAssemblies().Any(a =>
                    string.Equals(a.GetName().Name, "Avalonia.Headless", StringComparison.OrdinalIgnoreCase)))
            {
                return "Headless";
            }

            bool isWaylandEnv =
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
                string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

            if (isWaylandEnv)
            {
                string? disableWayland = Environment.GetEnvironmentVariable("XERAHS_DISABLE_WAYLAND");
                bool waylandDisabled =
                    string.Equals(disableWayland, "1", StringComparison.Ordinal) ||
                    string.Equals(disableWayland, "true", StringComparison.OrdinalIgnoreCase);

                return waylandDisabled ? "X11 (XWayland)" : "Wayland";
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            {
                return "X11";
            }

            return "Linux";
        }

        return RuntimeInformation.OSDescription;
    }

    private static LoadedAssemblyGroup ClassifyAssembly(string assemblyName)
    {
        if (assemblyName.StartsWith("XerahS", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("ShareX.", StringComparison.OrdinalIgnoreCase))
        {
            return LoadedAssemblyGroup.XerahS;
        }

        if (assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase))
        {
            return LoadedAssemblyGroup.System;
        }

        return LoadedAssemblyGroup.ThirdParty;
    }

    private static string FormatVersion(Version? version)
    {
        if (version == null)
        {
            return "Unknown";
        }

        return version.Revision > 0
            ? $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}.{version.Revision}"
            : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}

public enum LoadedAssemblyGroup
{
    ThirdParty,
    XerahS,
    System
}

public sealed class LoadedAssemblyInfoViewModel
{
    public string Name { get; }

    public LoadedAssemblyGroup Group { get; }

    public string Version { get; }

    public LoadedAssemblyInfoViewModel(string name, LoadedAssemblyGroup group, string version)
    {
        Name = name;
        Group = group;
        Version = version;
    }
}
