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
using System.Runtime.InteropServices;

namespace XerahS.UI.Services
{
    public class PlatformInfoService : IPlatformInfo
    {
        public PlatformType Platform
        {
            get
            {
                if (OperatingSystem.IsWindows()) return PlatformType.Windows;
                if (OperatingSystem.IsLinux()) return PlatformType.Linux;
                if (OperatingSystem.IsMacOS()) return PlatformType.MacOS;
                return PlatformType.Unknown;
            }
        }

        public string OSVersion => Environment.OSVersion.Version.ToString();
        public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
        public bool IsWindows => OperatingSystem.IsWindows();
        public bool IsLinux => OperatingSystem.IsLinux();
        public bool IsMacOS => OperatingSystem.IsMacOS();

        public bool IsWindows10OrGreater
        {
            get
            {
                if (!IsWindows) return false;
                return Environment.OSVersion.Version.Major >= 10;
            }
        }

        public bool IsWindows11OrGreater
        {
            get
            {
                if (!IsWindows) return false;
                // Windows 11 is version 10.0 with build >= 22000
                return Environment.OSVersion.Version.Major >= 10 &&
                       Environment.OSVersion.Version.Build >= 22000;
            }
        }

        public string RuntimeVersion => Environment.Version.ToString();

        public bool IsElevated
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    // TODO: Implement proper elevation check for Windows
                    return false;
                }
                // TODO: Implement for other platforms
                return false;
            }
        }
    }
}
