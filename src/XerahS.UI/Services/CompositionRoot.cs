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
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.UI.Services
{
    /// <summary>
    /// Single composition root: builds the DI container from platform and app services
    /// and sets <see cref="PlatformServices.RootProvider"/> for constructor injection and migration.
    /// Call after platform init and after UI/Toast/ImageEncoder are registered.
    /// </summary>
    public static class CompositionRoot
    {
        /// <summary>
        /// Builds the service provider from current <see cref="PlatformServices"/> state
        /// and sets it as the root provider. No-op if platform services are not initialized.
        /// </summary>
        public static void BuildAndSetRootProvider()
        {
            if (!PlatformServices.IsInitialized)
            {
                return;
            }

            var map = new Dictionary<Type, object>();

            void Register<T>(T instance) where T : class
            {
                if (instance != null)
                {
                    map[typeof(T)] = instance;
                }
            }

            // Required platform services (from Initialize)
            Register(PlatformServices.PlatformInfo);
            Register(PlatformServices.Screen);
            Register(PlatformServices.Clipboard);
            Register(PlatformServices.Window);
            Register(PlatformServices.Input);
            Register(PlatformServices.Fonts);
            Register(PlatformServices.Hotkey);
            Register(PlatformServices.ScreenCapture);
            Register(PlatformServices.Startup);
            Register(PlatformServices.WatchFolderDaemon);
            Register(PlatformServices.System);
            Register(PlatformServices.Diagnostic);

            // Optional platform services
            if (PlatformServices.GetShellIntegrationIfAvailable() is { } shellIntegration)
            {
                Register(shellIntegration);
            }

            if (PlatformServices.GetNotificationIfAvailable() is { } notification)
            {
                Register(notification);
            }

            if (PlatformServices.IsThemeServiceInitialized)
            {
                Register(PlatformServices.Theme);
            }

            if (PlatformServices.ScrollingCapture is { } scrollingCapture)
            {
                Register(scrollingCapture);
            }

            if (PlatformServices.Ocr is { } ocr)
            {
                Register(ocr);
            }

            // App services (registered in OnFrameworkInitializationCompleted before this is called)
            if (PlatformServices.IsToastServiceInitialized)
            {
                Register(PlatformServices.Toast);
            }

            try
            {
                Register(PlatformServices.UI);
            }
            catch (InvalidOperationException)
            {
                // UI not registered (e.g. headless/bootstrap)
            }

            try
            {
                Register(PlatformServices.ImageEncoder);
            }
            catch (InvalidOperationException)
            {
                // ImageEncoder not registered
            }

            IServiceProvider provider = new RootServiceProvider(map);
            PlatformServices.SetRootProvider(provider);
        }
    }

    /// <summary>
    /// Minimal service provider for the composition root; resolves from a type-to-instance map.
    /// </summary>
    internal sealed class RootServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _map;

        internal RootServiceProvider(Dictionary<Type, object> map)
        {
            _map = map;
        }

        public object? GetService(Type serviceType)
        {
            return _map.TryGetValue(serviceType, out var instance) ? instance : null;
        }
    }
}
