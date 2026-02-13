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

using System.Drawing;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Mobile;

public class MobileWindowService : IWindowService
{
    public IntPtr GetForegroundWindow() => IntPtr.Zero;
    public bool SetForegroundWindow(IntPtr handle) => false;
    public string GetWindowText(IntPtr handle) => string.Empty;
    public string GetWindowClassName(IntPtr handle) => string.Empty;
    public Rectangle GetWindowBounds(IntPtr handle) => Rectangle.Empty;
    public Rectangle GetWindowClientBounds(IntPtr handle) => Rectangle.Empty;
    public bool IsWindowVisible(IntPtr handle) => false;
    public bool IsWindowMaximized(IntPtr handle) => false;
    public bool IsWindowMinimized(IntPtr handle) => false;
    public bool ShowWindow(IntPtr handle, int cmdShow) => false;
    public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags) => false;
    public WindowInfo[] GetAllWindows() => [];
    public uint GetWindowProcessId(IntPtr handle) => 0;
    public IntPtr SearchWindow(string windowTitle) => IntPtr.Zero;
    public bool ActivateWindow(IntPtr handle) => false;
    public bool SetWindowClickThrough(IntPtr handle) => false;
}
