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

using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Mobile;

public static class MobilePlatform
{
    public static void Initialize(PlatformType platformType = PlatformType.Android)
    {
        PlatformServices.Initialize(
            platformInfo: new MobilePlatformInfo(platformType),
            screenService: new MobileScreenService(),
            clipboardService: new MobileClipboardService(),
            windowService: new MobileWindowService(),
            screenCaptureService: new MobileScreenCaptureService(),
            hotkeyService: new MobileHotkeyService(),
            inputService: new MobileInputService(),
            fontService: new MobileFontService(),
            startupService: new UnsupportedStartupService(),
            systemService: new MobileSystemService(),
            diagnosticService: new MobileDiagnosticService(),
            notificationService: new MobileNotificationService(),
            watchFolderDaemonService: new UnsupportedWatchFolderDaemonService());
    }
}
