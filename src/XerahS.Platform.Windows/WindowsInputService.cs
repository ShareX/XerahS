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
    /// Windows-specific implementation of IInputService using Win32 API
    /// </summary>
    public class WindowsInputService : IInputService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <inheritdoc/>
        public Point GetCursorPosition()
        {
            if (GetCursorPos(out POINT p))
            {
                return new Point(p.X, p.Y);
            }
            return Point.Empty;
        }
    }
}
