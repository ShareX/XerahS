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

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Service for system-level operations like file explorer, URL opening, etc.
    /// </summary>
    public interface ISystemService
    {
        /// <summary>
        /// Opens the file explorer with the specified file selected.
        /// </summary>
        /// <param name="filePath">The full path to the file.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ShowFileInExplorer(string filePath);

        /// <summary>
        /// Opens the specified URL in the default browser.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool OpenUrl(string url);

        /// <summary>
        /// Opens the specified file using the default application.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool OpenFile(string filePath);
    }
}
