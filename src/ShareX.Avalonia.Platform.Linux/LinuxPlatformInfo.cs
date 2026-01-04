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

using ShareX.Ava.Platform.Abstractions;
using System;
using System.Runtime.InteropServices;

namespace ShareX.Ava.Platform.Linux
{
    /// <summary>
    /// Linux platform information service
    /// </summary>
    public class LinuxPlatformInfo : IPlatformInfo
    {
        public PlatformType Platform => PlatformType.Linux;

        public string OSVersion => Environment.OSVersion.VersionString;

        public string Architecture => RuntimeInformation.OSArchitecture.ToString();

        public bool IsWindows => false;

        public bool IsLinux => true;

        public bool IsMacOS => false;

        public bool IsWindows10OrGreater => false;

        public bool IsWindows11OrGreater => false;

        public string RuntimeVersion => RuntimeInformation.FrameworkDescription;

        public bool IsElevated
        {
            get
            {
                try
                {
                    return geteuid() == 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        [DllImport("libc")]
        private static extern uint geteuid();
    }
}
