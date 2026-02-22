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

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Configuration options for ShareX bootstrap initialization.
    /// </summary>
    public class BootstrapOptions
    {
        /// <summary>
        /// Enable logging initialization. Default is true.
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// Custom log file path. If null, uses default location.
        /// </summary>
        public string? LogPath { get; set; }

        /// <summary>
        /// Initialize recording services synchronously.
        /// UI should set this to false and handle async initialization separately.
        /// CLI should set this to true to ensure recording is ready before accepting commands.
        /// </summary>
        public bool InitializeRecording { get; set; } = true;

        /// <summary>
        /// Custom screen capture service to use instead of platform default.
        /// </summary>
        public IScreenCaptureService? ScreenCaptureService { get; set; }

        /// <summary>
        /// UI service implementation (required for workflows that interact with UI).
        /// </summary>
        public IUIService? UIService { get; set; }

        /// <summary>
        /// Toast notification service implementation.
        /// </summary>
        public IToastService? ToastService { get; set; }
    }
}
