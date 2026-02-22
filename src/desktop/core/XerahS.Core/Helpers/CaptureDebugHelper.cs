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

namespace XerahS.Core.Helpers
{
    /// <summary>
    /// Helper class for generating comprehensive diagnostic logs for Region Capture.
    /// Delegates to platform-specific service implementation.
    /// </summary>
    public static class CaptureDebugHelper
    {
        /// <summary>
        /// Writes a comprehensive diagnostic report for Region Capture troubleshooting.
        /// </summary>
        /// <param name="personalFolder">The base folder (PathsManager.PersonalFolder) for output</param>
        /// <returns>Full path of the written log file, or empty string on failure</returns>
        public static string WriteRegionCaptureDiagnostics(string personalFolder)
        {
            try
            {
                return PlatformServices.Diagnostic.WriteRegionCaptureDiagnostics(personalFolder);
            }
            catch
            {
                // Core-level fallback if service fails or isn't initialized
                return string.Empty;
            }
        }
    }
}
