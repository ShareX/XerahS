#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using System;
using System.Runtime.InteropServices;

namespace ShareX.Avalonia.Platform.Abstractions
{
    /// <summary>
    /// Central service locator for platform services
    /// </summary>
    public static class PlatformServices
    {
        private static IPlatformInfo? _platformInfo;
        private static IScreenService? _screenService;
        private static IClipboardService? _clipboardService;
        private static IWindowService? _windowService;

        public static IPlatformInfo PlatformInfo
        {
            get => _platformInfo ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _platformInfo = value;
        }

        public static IScreenService Screen
        {
            get => _screenService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _screenService = value;
        }

        public static IClipboardService Clipboard
        {
            get => _clipboardService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _clipboardService = value;
        }

        public static IWindowService Window
        {
            get => _windowService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _windowService = value;
        }

        private static IScreenCaptureService? _screenCaptureService;
        public static IScreenCaptureService ScreenCapture
        {
            get => _screenCaptureService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _screenCaptureService = value;
        }

        /// <summary>
        /// Checks if platform services have been initialized
        /// </summary>
        public static bool IsInitialized =>
            _platformInfo != null && _screenService != null && _clipboardService != null && _windowService != null && _screenCaptureService != null;

        /// <summary>
        /// Initializes platform services with provided implementations
        /// </summary>
        public static void Initialize(
            IPlatformInfo platformInfo,
            IScreenService screenService,
            IClipboardService clipboardService,
            IWindowService windowService,
            IScreenCaptureService screenCaptureService)
        {
            _platformInfo = platformInfo ?? throw new ArgumentNullException(nameof(platformInfo));
            _screenService = screenService ?? throw new ArgumentNullException(nameof(screenService));
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
        }

        /// <summary>
        /// Resets all platform services (mainly for testing)
        /// </summary>
        public static void Reset()
        {
            _platformInfo = null;
            _screenService = null;
            _clipboardService = null;
            _windowService = null;
            _screenCaptureService = null;
        }
    }
}
