#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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

using System.Runtime.InteropServices;

namespace ShareX.Avalonia.Common
{
    public static class NativeMethods
    {
        [DllImport("winmm.dll", EntryPoint = "timeGetDevCaps")]
        public static extern uint TimeGetDevCaps(ref TimeCaps ptc, uint cbtc);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint uPeriod);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        public const int CURSOR_SHOWING = 0x00000001;
        public const int DI_NORMAL = 0x0003;

        public static System.Drawing.Icon? GetFileIcon(string filePath, bool isSmallIcon)
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                 return null;
            }

            try {
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = (uint)(SHGFI.Icon | (isSmallIcon ? SHGFI.SmallIcon : SHGFI.LargeIcon));

                SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

                if (shfi.hIcon == IntPtr.Zero) return null;

                using (var tempIcon = System.Drawing.Icon.FromHandle(shfi.hIcon))
                {
                     var icon = (System.Drawing.Icon)tempIcon.Clone();
                     DestroyIcon(shfi.hIcon);
                     return icon;
                }
            } 
            catch { return null; }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TimeCaps
    {
        public uint wPeriodMin;
        public uint wPeriodMax;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [Flags]
    public enum SHGFI : uint
    {
        Icon = 0x000000100,
        DisplayName = 0x000000200,
        TypeName = 0x000000400,
        Attributes = 0x000000800,
        IconLocation = 0x000001000,
        ExeType = 0x000002000,
        SysIconIndex = 0x000004000,
        LinkOverlay = 0x000008000,
        Selected = 0x000010000,
        Attr_Specified = 0x000020000,
        LargeIcon = 0x000000000,
        SmallIcon = 0x000000001,
        OpenIcon = 0x000000002,
        ShellIconSize = 0x000000004,
        PIDL = 0x000000008,
        UseFileAttributes = 0x000000010
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public System.Drawing.Point ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }
}
