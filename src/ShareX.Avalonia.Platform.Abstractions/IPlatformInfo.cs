#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Platform information service
    /// </summary>
    public interface IPlatformInfo
    {
        /// <summary>
        /// Gets the current platform type
        /// </summary>
        PlatformType Platform { get; }

        /// <summary>
        /// Gets the OS version string
        /// </summary>
        string OSVersion { get; }

        /// <summary>
        /// Gets the OS architecture (x64, x86, ARM64, etc.)
        /// </summary>
        string Architecture { get; }

        /// <summary>
        /// Checks if running on Windows
        /// </summary>
        bool IsWindows { get; }

        /// <summary>
        /// Checks if running on Linux
        /// </summary>
        bool IsLinux { get; }

        /// <summary>
        /// Checks if running on macOS
        /// </summary>
        bool IsMacOS { get; }

        /// <summary>
        /// Checks if running on Windows 10 or greater
        /// </summary>
        bool IsWindows10OrGreater { get; }

        /// <summary>
        /// Checks if running on Windows 11 or greater
        /// </summary>
        bool IsWindows11OrGreater { get; }

        /// <summary>
        /// Gets the .NET runtime version
        /// </summary>
        string RuntimeVersion { get; }

        /// <summary>
        /// Checks if running as administrator/root
        /// </summary>
        bool IsElevated { get; }
    }

    /// <summary>
    /// Platform types
    /// </summary>
    public enum PlatformType
    {
        Unknown,
        Windows,
        Linux,
        MacOS,
        FreeBSD
    }
}
