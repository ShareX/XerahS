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

using XerahS.Services.Abstractions;

namespace XerahS.Platform.Abstractions
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
        private static IInputService? _inputService;
        private static IFontService? _fontService;
        private static IShellIntegrationService? _shellIntegrationService;

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

        public static IInputService Input
        {
            get => _inputService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _inputService = value;
        }

        public static IFontService Fonts
        {
            get => _fontService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _fontService = value;
        }

        public static IShellIntegrationService ShellIntegration
        {
            get => _shellIntegrationService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _shellIntegrationService = value;
        }

        private static IHotkeyService? _hotkeyService;
        public static IHotkeyService Hotkey
        {
            get => _hotkeyService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _hotkeyService = value;
        }

        private static IScreenCaptureService? _screenCaptureService;
        public static IScreenCaptureService ScreenCapture
        {
            get => _screenCaptureService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _screenCaptureService = value;
        }

        private static INotificationService? _notificationService;
        public static INotificationService Notification
        {
            get => _notificationService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _notificationService = value;
        }

        private static IUIService? _uiService;
        public static IUIService UI
        {
            get => _uiService ?? throw new InvalidOperationException("UI service not initialized. Call RegisterUIService() first.");
            private set => _uiService = value;
        }

        private static IToastService? _toastService;
        public static IToastService Toast
        {
            get => _toastService ?? throw new InvalidOperationException("Toast service not initialized. Call RegisterToastService() first.");
            private set => _toastService = value;
        }

        private static IImageEncoderService? _imageEncoderService;
        public static IImageEncoderService ImageEncoder
        {
            get => _imageEncoderService ?? throw new InvalidOperationException("ImageEncoder service not initialized. Call RegisterImageEncoderService() first.");
            private set => _imageEncoderService = value;
        }

        /// <summary>
        /// Checks if toast service has been initialized
        /// </summary>
        public static bool IsToastServiceInitialized => _toastService != null;

        private static IStartupService? _startupService;
        public static IStartupService Startup
        {
            get => _startupService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _startupService = value;
        }

        private static IWatchFolderDaemonService? _watchFolderDaemonService;
        public static IWatchFolderDaemonService WatchFolderDaemon
        {
            get => _watchFolderDaemonService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _watchFolderDaemonService = value;
        }

        /// <summary>
        /// Checks if platform services have been initialized
        /// </summary>
        public static bool IsInitialized =>
            _platformInfo != null && _screenService != null && _clipboardService != null && _windowService != null && _screenCaptureService != null && _hotkeyService != null && _inputService != null && _fontService != null && _startupService != null;

        private static ISystemService? _systemService;
        public static ISystemService System
        {
            get => _systemService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _systemService = value;
        }

        private static IDiagnosticService? _diagnosticService;
        public static IDiagnosticService Diagnostic
        {
            get => _diagnosticService ?? throw new InvalidOperationException("Platform services not initialized. Call Initialize() first.");
            set => _diagnosticService = value;
        }

        private static IThemeService? _themeService;
        public static IThemeService Theme
        {
            get => _themeService ?? throw new InvalidOperationException("Theme service not initialized.");
            set => _themeService = value;
        }

        /// <summary>
        /// Checks if theme service has been initialized
        /// </summary>
        public static bool IsThemeServiceInitialized => _themeService != null;

        private static IScrollingCaptureService? _scrollingCaptureService;

        /// <summary>
        /// Optional scrolling capture service for scroll simulation and scroll bar queries.
        /// Null on platforms that do not support scrolling capture.
        /// </summary>
        public static IScrollingCaptureService? ScrollingCapture
        {
            get => _scrollingCaptureService;
            set => _scrollingCaptureService = value;
        }

        private static IOcrService? _ocrService;

        /// <summary>
        /// Optional OCR service for text recognition.
        /// Windows uses native Windows.Media.Ocr, other platforms may use Tesseract.
        /// </summary>
        public static IOcrService? Ocr
        {
            get => _ocrService;
            set => _ocrService = value;
        }

        /// <summary>
        /// Initializes platform services with provided implementations
        /// </summary>
        public static void Initialize(
            IPlatformInfo platformInfo,
            IScreenService screenService,
            IClipboardService clipboardService,
            IWindowService windowService,
            IScreenCaptureService screenCaptureService,
            IHotkeyService hotkeyService,
            IInputService inputService,
            IFontService fontService,
            IStartupService startupService,
            ISystemService systemService,
            IDiagnosticService diagnosticService,
            IShellIntegrationService? shellIntegrationService = null,
            INotificationService? notificationService = null,
            IWatchFolderDaemonService? watchFolderDaemonService = null)
        {
            _platformInfo = platformInfo ?? throw new ArgumentNullException(nameof(platformInfo));
            _screenService = screenService ?? throw new ArgumentNullException(nameof(screenService));
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
            _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
            _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
            _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
            _fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _diagnosticService = diagnosticService ?? throw new ArgumentNullException(nameof(diagnosticService));
            _shellIntegrationService = shellIntegrationService;  // Optional - null means shell integration not available
            _notificationService = notificationService;  // Optional - null means no native notifications
            _watchFolderDaemonService = watchFolderDaemonService ?? new UnsupportedWatchFolderDaemonService();
        }


        public static void RegisterUIService(IUIService uiService)
        {
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        }

        /// <summary>
        /// Registers the toast notification service
        /// </summary>
        public static void RegisterToastService(IToastService toastService)
        {
            _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        }

        public static void RegisterImageEncoderService(IImageEncoderService imageEncoderService)
        {
            _imageEncoderService = imageEncoderService ?? throw new ArgumentNullException(nameof(imageEncoderService));
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
            _inputService = null;
            _hotkeyService = null;
            _fontService = null;
            _notificationService = null;
            _toastService = null;
            _systemService = null;
            _startupService = null;
            _watchFolderDaemonService = null;
            _diagnosticService = null;
            _shellIntegrationService = null;
            _themeService = null;
            _scrollingCaptureService = null;
            _ocrService = null;
        }
    }
}
