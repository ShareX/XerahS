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

using XerahS.Platform.Abstractions;
using System.Runtime.InteropServices;

namespace XerahS.Platform.Windows
{
    /// <summary>
    /// Windows platform information service
    /// </summary>
    public class WindowsPlatformInfo : IPlatformInfo
    {
        public PlatformType Platform => PlatformType.Windows;

        public string OSVersion => Environment.OSVersion.VersionString;

        public string Architecture => RuntimeInformation.OSArchitecture.ToString();

        public bool IsWindows => true;

        public bool IsLinux => false;

        public bool IsMacOS => false;

        public bool IsWindows10OrGreater
        {
            get
            {
                var version = Environment.OSVersion.Version;
                return version.Major >= 10;
            }
        }

        public bool IsWindows11OrGreater
        {
            get
            {
                var version = Environment.OSVersion.Version;
                return version.Major >= 10 && version.Build >= 22000;
            }
        }

        public string RuntimeVersion => RuntimeInformation.FrameworkDescription;

        public bool IsElevated
        {
            get
            {
                try
                {
                    using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                    {
                        var principal = new System.Security.Principal.WindowsPrincipal(identity);
                        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                    }
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
