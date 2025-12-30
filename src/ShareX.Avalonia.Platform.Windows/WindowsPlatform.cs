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

using ShareX.Avalonia.Platform.Abstractions;

namespace ShareX.Avalonia.Platform.Windows
{
    /// <summary>
    /// Initializes Windows platform services
    /// </summary>
    public static class WindowsPlatform
    {
        /// <summary>
        /// Initializes all Windows platform services
        /// </summary>
        public static void Initialize()
        {
            // TODO: Implement Windows-specific screen capture service
            IScreenCaptureService screenCaptureService = null!;
            
            PlatformServices.Initialize(
                platformInfo: new WindowsPlatformInfo(),
                screenService: new WindowsScreenService(),
                clipboardService: new WindowsClipboardService(),
                windowService: new WindowsWindowService(),
                screenCaptureService: screenCaptureService
            );
        }
    }
}
