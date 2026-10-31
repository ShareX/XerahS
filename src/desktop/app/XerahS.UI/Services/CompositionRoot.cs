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
using XerahS.Bootstrap;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.UI.Services
{
    /// <summary>
    /// Single composition root: builds the DI container from the shared desktop host path
    /// and layers UI-specific services on top.
    /// </summary>
    public static class CompositionRoot
    {
        /// <summary>
        /// Builds the UI service provider from explicit host services captured at the
        /// application boundary.
        /// </summary>
        public static IServiceProvider BuildServiceProvider(
            IUIService uiService,
            IToastService toastService,
            IImageEncoderService imageEncoderService)
        {
            if (!PlatformServices.IsInitialized)
            {
                throw new InvalidOperationException("Platform services must be initialized before UI composition.");
            }

            DesktopHostServices hostServices = DesktopHostServices.FromCurrentProcess(
                uiService,
                toastService,
                imageEncoderService);

            Core.Services.Privacy.PrivacyServices.AnalysisProviderFactory = XerahS.Assistant.Services.AssistantPrivacyAnalysisProvider.CreateActive;
            Core.Services.Privacy.PrivacyServices.ReviewService = new AvaloniaPrivacyReviewService(uiService);

            return DesktopHostComposition.CreateServiceProvider(hostServices, services =>
            {
                services.AddSingleton<IViewDialogService, AvaloniaDialogService>();
                services.AddSingleton<IDialogService, AvaloniaDialogServiceAdapter>();
                services.AddSingleton<ILifecycleService, AvaloniaLifecycleService>();
                services.AddSingleton<IUiViewModelFactory, UiViewModelFactory>();
            });
        }
    }
}
