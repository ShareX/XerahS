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
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core.Uploaders;
using XerahS.OmaXerahs.Models;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.OmaXerahs.Services;

internal sealed class ImageDestinationInspection
{
    public bool Ready { get; init; }
    public UploaderInstance? Instance { get; init; }
    public IUploaderProvider? Provider { get; init; }
    public string SecretStoreBackend { get; init; } = "unknown";
    public bool SecretStoreFallback { get; init; }
    public int PluginsLoaded { get; init; }
}

internal static class UploadHost
{
    private static bool _bootstrapped;
    private static bool _pluginsLoaded;
    private static readonly object _lock = new();

    internal static string GetVersion()
    {
        string? version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.1.0";
        }

        int plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    internal static async Task EnsureBootstrappedAsync()
    {
        if (_bootstrapped)
        {
            return;
        }

        var result = await ShareXBootstrap.InitializeAsync(new BootstrapOptions
        {
            EnableLogging = true,
            ConsoleLogging = false,
            InitializeRecording = false,
            UIService = new HeadlessUIService(),
            ToastService = new HeadlessToastService()
        });

        if (!result.PlatformServicesInitialized)
        {
            throw new InvalidOperationException("Failed to initialize platform services.");
        }

        if (!result.ConfigurationLoaded)
        {
            throw new InvalidOperationException("Failed to load configuration.");
        }

        _bootstrapped = true;
    }

    internal static void EnsurePluginsLoaded()
    {
        lock (_lock)
        {
            if (_pluginsLoaded && ProviderCatalog.ArePluginsLoaded())
            {
                return;
            }

            ProviderContextManager.EnsureProviderContext();
            ProviderCatalog.InitializeBuiltInProviders();
            ProviderCatalog.LoadPlugins(PathsManager.GetPluginDirectories());
            _pluginsLoaded = true;
        }
    }

    internal static ImageDestinationInspection InspectImageDestination(string? imagePath = null)
    {
        EnsurePluginsLoaded();

        string backend = "unknown";
        bool fallback = false;
        var secrets = ProviderCatalog.GetProviderContext()?.Secrets;
        if (secrets is ISecretStoreInfo info)
        {
            backend = info.BackendName;
            fallback = info.IsFallback;
        }

        int pluginsLoaded = ProviderCatalog.GetAllPluginMetadata().Count;

        // Prefer Image destinations; a File destination must explicitly route the image type.
        var preferred = PreferDefault(GetUsableImageInstances(), UploaderCategory.Image)
            ?? PreferDefault(GetUsableFileInstances().Where(i => RoutesImages(i.FileTypeRouting, imagePath)).ToList(), UploaderCategory.File);

        return new ImageDestinationInspection
        {
            Ready = preferred != null,
            Instance = preferred,
            Provider = preferred == null ? null : ProviderCatalog.GetProvider(preferred.ProviderId),
            SecretStoreBackend = backend,
            SecretStoreFallback = fallback,
            PluginsLoaded = pluginsLoaded
        };
    }

    internal static List<UploaderInstance> GetUsableImageInstances() => GetUsableInstances(UploaderCategory.Image);

    internal static List<UploaderInstance> GetUsableFileInstances() => GetUsableInstances(UploaderCategory.File);

    /// <summary>Image destinations followed by File destinations: every instance an image upload can go to.</summary>
    internal static List<UploaderInstance> GetUsableImageUploadInstances() =>
        GetUsableImageInstances().Concat(GetUsableFileInstances()).ToList();

    private static List<UploaderInstance> GetUsableInstances(UploaderCategory category)
    {
        EnsurePluginsLoaded();
        return InstanceManager.Instance.GetInstancesByCategory(category)
            .Where(instance => IsUsableInstance(instance, category))
            .ToList();
    }

    private static readonly string[] ImageExtensions = ["png", "jpg", "jpeg", "gif", "webp", "bmp", "tif", "tiff", "ico"];

    internal static bool RoutesImages(FileTypeScope? routing, string? imagePath = null)
    {
        if (routing == null) return false;
        if (routing.AllFileTypes) return true;
        var extensions = routing.FileExtensions ?? [];
        if (imagePath != null)
        {
            string extension = Path.GetExtension(imagePath).TrimStart('.');
            return ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) &&
                extensions.Any(ext => string.Equals(ext?.Trim().TrimStart('.'), extension, StringComparison.OrdinalIgnoreCase));
        }
        return extensions.Any(ext => ImageExtensions.Contains(ext?.Trim().TrimStart('.'), StringComparer.OrdinalIgnoreCase));
    }

    internal static bool IsUsableImageInstance(UploaderInstance instance) => IsUsableInstance(instance, UploaderCategory.Image);

    private static bool IsUsableInstance(UploaderInstance instance, UploaderCategory category)
    {
        if (instance.Category != category || !instance.IsAvailable)
        {
            return false;
        }

        if (InstanceManager.IsAutoProvider(instance.ProviderId))
        {
            return false;
        }

        var provider = ProviderCatalog.GetProvider(instance.ProviderId);
        if (provider == null)
        {
            return false;
        }

        try
        {
            return provider.ValidateSettings(instance.SettingsJson);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"ValidateSettings failed for {category} instance");
            return false;
        }
    }

    private static UploaderInstance? PreferDefault(List<UploaderInstance> usable, UploaderCategory category)
    {
        if (usable.Count == 0)
        {
            return null;
        }

        var preferred = usable.FirstOrDefault(i =>
            InstanceManager.Instance.IsDefaultInstance(category, i.InstanceId));
        return preferred ?? usable.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
    }

    /// <summary>
    /// Test-only surface that lifts the routing decisions in
    /// <see cref="IsUsableImageInstance"/>, <see cref="PreferDefault(List{UploaderInstance}, UploaderCategory)"/>,
    /// and the host-name match used by
    /// <c>UploadCommand.ResolveUploadedInstance</c> into pure functions that can be
    /// exercised without touching the static <see cref="InstanceManager"/> or
    /// <see cref="ProviderCatalog"/> singletons.
    /// </summary>
    internal static class TestAccessor
    {
        /// <summary>
        /// Pure mirror of <see cref="IsUsableImageInstance"/>. The caller supplies
        /// the answers to the static lookups (<paramref name="isAutoProvider"/>,
        /// <paramref name="providerExists"/>, <paramref name="validateSettings"/>)
        /// so tests can exercise the predicate without populating the catalog.
        /// </summary>
        internal static bool IsUsableImageInstance(
            UploaderInstance instance,
            bool isAutoProvider,
            bool providerExists,
            bool validateSettings) =>
            IsUsableInstance(instance, UploaderCategory.Image, isAutoProvider, providerExists, validateSettings);

        /// <summary>
        /// Pure mirror of the usability check for <paramref name="category"/> (Image, or File for the
        /// fallback the app uses when no Image destination exists).
        /// </summary>
        internal static bool IsUsableInstance(
            UploaderInstance instance,
            UploaderCategory category,
            bool isAutoProvider,
            bool providerExists,
            bool validateSettings)
        {
            if (instance.Category != category || !instance.IsAvailable)
            {
                return false;
            }

            if (isAutoProvider)
            {
                return false;
            }

            if (!providerExists)
            {
                return false;
            }

            return validateSettings;
        }

        /// <summary>
        /// Pure mirror of the host-name match in
        /// <c>UploadCommand.ResolveUploadedInstance</c>: case-insensitive
        /// <see cref="UploaderInstance.DisplayName"/> match. Returns the first hit
        /// or <c>null</c> when <paramref name="host"/> is null/empty/whitespace
        /// or no instance matches.
        /// </summary>
        internal static UploaderInstance? RouteByDisplayName(
            IReadOnlyList<UploaderInstance> instances,
            string? host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            foreach (var instance in instances)
            {
                if (string.Equals(instance.DisplayName, host, StringComparison.OrdinalIgnoreCase))
                {
                    return instance;
                }
            }

            return null;
        }

        /// <summary>
        /// Pure mirror of <see cref="PreferDefault(List{UploaderInstance}, UploaderCategory)"/>:
        /// returns the instance whose <see cref="UploaderInstance.InstanceId"/> is
        /// the default for the Image category. Falls back to the most recently
        /// created instance when no default is configured.
        /// </summary>
        internal static UploaderInstance? PreferDefault(
            IReadOnlyList<UploaderInstance> usable,
            string? defaultInstanceId)
        {
            if (usable.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(defaultInstanceId))
            {
                foreach (var instance in usable)
                {
                    if (string.Equals(instance.InstanceId, defaultInstanceId, StringComparison.Ordinal))
                    {
                        return instance;
                    }
                }
            }

            return usable.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
        }
    }

    internal static DoctorResponse CreateDoctorResponse(ImageDestinationInspection inspection)
    {
        string version = GetVersion();
        return new DoctorResponse
        {
            SchemaVersion = 1,
            Ok = inspection.Ready,
            Cli = new DoctorCliInfo { Name = "omaxerahs", Version = version },
            Image = new DoctorImageInfo
            {
                Ready = inspection.Ready,
                ProviderId = inspection.Instance?.ProviderId,
                InstanceId = inspection.Instance?.InstanceId,
                DisplayName = inspection.Instance?.DisplayName,
                Category = inspection.Instance?.Category.ToString()
            },
            SecretStore = new DoctorSecretStoreInfo
            {
                Backend = inspection.SecretStoreBackend,
                Fallback = inspection.SecretStoreFallback
            },
            Plugins = new DoctorPluginsInfo { Loaded = inspection.PluginsLoaded },
            History = ProbeHistoryDatabase()
        };
    }

    /// <summary>
    /// Opens an in-memory SQLite connection. A single-file publish that cannot load the native
    /// e_sqlite3 provider fails here (TypeInitializationException) instead of silently losing
    /// history entries after an upload (XIP0088 Phase 0 item 3).
    /// </summary>
    internal static DoctorHistoryInfo ProbeHistoryDatabase()
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT sqlite_version();";
            _ = command.ExecuteScalar();
            return new DoctorHistoryInfo { Ok = true };
        }
        catch (Exception ex)
        {
            Exception root = ex;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            return new DoctorHistoryInfo { Ok = false, Error = $"{root.GetType().Name}: {root.Message}" };
        }
    }
}
