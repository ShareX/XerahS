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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XerahS.Core.Cloud;
using XerahS.Uploaders.PluginSystem;
using XerahS.Core.Uploaders;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Extension methods for registering XerahS desktop host services into an IServiceCollection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers platform capabilities from instances explicitly owned by the host.
        /// </summary>
        public static IServiceCollection AddXerahSPlatformServices(
            this IServiceCollection services,
            DesktopPlatformServices platform)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(platform);

            services.TryAddSingleton(platform.PlatformInfo);
            services.TryAddSingleton(platform.Screen);
            services.TryAddSingleton(platform.Clipboard);
            services.TryAddSingleton(platform.ClipboardMonitor);
            services.TryAddSingleton(platform.Window);
            services.TryAddSingleton(platform.Input);
            services.TryAddSingleton(platform.Fonts);
            services.TryAddSingleton(platform.Hotkey);
            services.TryAddSingleton(platform.ScreenCapture);
            services.TryAddSingleton(platform.Startup);
            services.TryAddSingleton(platform.System);
            services.TryAddSingleton(platform.Diagnostic);
            services.TryAddSingleton(platform.WatchFolderDaemon);

            TryAddOptionalSingleton(services, platform.ShellIntegration);
            TryAddOptionalSingleton(services, platform.Notification);
            TryAddOptionalSingleton(services, platform.Theme);
            TryAddOptionalSingleton(services, platform.ScrollingCapture);
            TryAddOptionalSingleton(services, platform.Ocr);
            TryAddOptionalSingleton(services, platform.UI);
            TryAddOptionalSingleton(services, platform.Toast);
            TryAddOptionalSingleton(services, platform.ImageEncoder);

            return services;
        }

        /// <summary>
        /// Registers application managers and their host-facing adapters.
        /// </summary>
        public static IServiceCollection AddXerahSApplicationServices(
            this IServiceCollection services,
            DesktopApplicationServices application)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(application);

            services.TryAddSingleton(application.TaskManager);
            services.TryAddSingleton<ITaskManager>(application.TaskManager);
            services.TryAddSingleton<IDesktopTaskManager, DesktopTaskManagerAdapter>();

            services.TryAddSingleton(application.ScreenRecordingManager);
            services.TryAddSingleton<IScreenRecordingManager>(application.ScreenRecordingManager);
            services.TryAddSingleton<IScreenRecordingCoordinator, ScreenRecordingCoordinatorAdapter>();

            services.TryAddSingleton(application.WatchFolderManager);
            services.TryAddSingleton<IWatchFolderDaemonController, WatchFolderDaemonControllerAdapter>();
            services.TryAddSingleton<IProviderCatalog>(_ => ProviderCatalogService.Shared);

            return services;
        }

        /// <summary>
        /// Registers the canonical composition shared by desktop, CLI, daemon, and MCP hosts.
        /// </summary>
        public static IServiceCollection AddXerahSDesktopHostServices(
            this IServiceCollection services,
            DesktopHostServices hostServices)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(hostServices);

            services
                .AddXerahSPlatformServices(hostServices.Platform)
                .AddXerahSApplicationServices(hostServices.Application);
            return services.AddXerahSCloudServices();
        }

        /// <summary>
        /// Compatibility shim for callers that still use the process-wide registries.
        /// </summary>
        public static IServiceCollection AddXerahSDesktopHostServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (PlatformServices.IsInitialized)
            {
                return services.AddXerahSDesktopHostServices(DesktopHostServices.FromCurrentProcess());
            }

            AddLegacyPlatformServiceAccessors(services);
            services.AddXerahSApplicationServices(DesktopApplicationServices.FromCurrentProcess());
            return services.AddXerahSCloudServices();
        }

        /// <summary>
        /// Compatibility shim for the former all-in-one platform registration method.
        /// </summary>
        public static IServiceCollection AddXerahSPlatformServices(this IServiceCollection services) =>
            services.AddXerahSDesktopHostServices();

        private static void TryAddOptionalSingleton<TService>(
            IServiceCollection services,
            TService? instance)
            where TService : class
        {
            if (instance != null)
            {
                services.TryAddSingleton(instance);
            }
        }

        private static void AddLegacyPlatformServiceAccessors(IServiceCollection services)
        {
            services.TryAddSingleton(_ => PlatformServices.PlatformInfo);
            services.TryAddSingleton(_ => PlatformServices.Screen);
            services.TryAddSingleton(_ => PlatformServices.Clipboard);
            services.TryAddSingleton(_ => PlatformServices.ClipboardMonitor);
            services.TryAddSingleton(_ => PlatformServices.Window);
            services.TryAddSingleton(_ => PlatformServices.Input);
            services.TryAddSingleton(_ => PlatformServices.Fonts);
            services.TryAddSingleton(_ => PlatformServices.Hotkey);
            services.TryAddSingleton(_ => PlatformServices.ScreenCapture);
            services.TryAddSingleton(_ => PlatformServices.Startup);
            services.TryAddSingleton(_ => PlatformServices.System);
            services.TryAddSingleton(_ => PlatformServices.Diagnostic);
            services.TryAddSingleton(_ => PlatformServices.WatchFolderDaemon);

            TryAddOptionalSingleton(services, PlatformServices.GetShellIntegrationIfAvailable());
            TryAddOptionalSingleton(services, PlatformServices.GetNotificationIfAvailable());
        }

        private static IServiceCollection AddXerahSCloudServices(this IServiceCollection services)
        {
            services.TryAddSingleton(_ => XerahSCloudOptions.FromEnvironment());
            services.TryAddSingleton<ISecretStore>(_ => ProviderContextManager.EnsureProviderContext().Secrets);
            services.TryAddSingleton<IXerahSCloudSessionStore, XerahSCloudSessionStore>();
            services.TryAddSingleton<IXerahSCloudClock, SystemXerahSCloudClock>();
            services.TryAddSingleton<IXerahSCloudTokenValidator>(sp =>
                new XerahSCloudTokenValidator(httpClient: null, sp.GetRequiredService<IXerahSCloudClock>()));
            services.TryAddSingleton<IXerahSCloudOAuthTokenExchange>(sp =>
                new XerahSCloudOAuthTokenExchange(
                    httpClient: null,
                    sp.GetRequiredService<XerahSCloudOptions>(),
                    sp.GetRequiredService<IXerahSCloudTokenValidator>()));
            services.TryAddSingleton<IXerahSCloudOAuthCoordinator, XerahSCloudOAuthCoordinator>();
            services.TryAddSingleton<IXerahSCloudClient>(sp =>
                new XerahSCloudApiClient(
                    httpClient: null,
                    sp.GetRequiredService<IXerahSCloudSessionStore>(),
                    sp.GetRequiredService<IXerahSCloudOAuthTokenExchange>(),
                    sp.GetRequiredService<XerahSCloudOptions>()));
            return services;
        }
    }
}
