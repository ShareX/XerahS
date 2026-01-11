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

using System.Drawing;

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Platform-agnostic window management service
    /// </summary>
    public interface IWindowService
    {
        /// <summary>
        /// Gets the handle of the foreground window
        /// </summary>
        IntPtr GetForegroundWindow();

        /// <summary>
        /// Sets the foreground window
        /// </summary>
        bool SetForegroundWindow(IntPtr handle);

        /// <summary>
        /// Gets the window title
        /// </summary>
        string GetWindowText(IntPtr handle);

        /// <summary>
        /// Gets the window class name
        /// </summary>
        string GetWindowClassName(IntPtr handle);

        /// <summary>
        /// Gets the window bounds
        /// </summary>
        Rectangle GetWindowBounds(IntPtr handle);

        /// <summary>
        /// Gets the window client area bounds
        /// </summary>
        Rectangle GetWindowClientBounds(IntPtr handle);

        /// <summary>
        /// Checks if a window is visible
        /// </summary>
        bool IsWindowVisible(IntPtr handle);

        /// <summary>
        /// Checks if a window is maximized
        /// </summary>
        bool IsWindowMaximized(IntPtr handle);

        /// <summary>
        /// Checks if a window is minimized
        /// </summary>
        bool IsWindowMinimized(IntPtr handle);

        /// <summary>
        /// Shows or hides a window
        /// </summary>
        bool ShowWindow(IntPtr handle, int cmdShow);

        /// <summary>
        /// Sets the window position and size
        /// </summary>
        bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags);

        /// <summary>
        /// Gets all top-level windows
        /// </summary>
        WindowInfo[] GetAllWindows();

        /// <summary>
        /// Gets the process ID of the window
        /// </summary>
        uint GetWindowProcessId(IntPtr handle);

        /// <summary>
        /// Searches for a window by title. First tries exact match, then partial/contains match.
        /// </summary>
        /// <param name="windowTitle">Title or partial title to search for</param>
        /// <returns>Window handle if found, IntPtr.Zero otherwise</returns>
        IntPtr SearchWindow(string windowTitle);

        /// <summary>
        /// Activates a window, bringing it to the foreground. Uses robust activation techniques.
        /// </summary>
        /// <param name="handle">Window handle to activate</param>
        /// <returns>True if window was successfully activated</returns>
        bool ActivateWindow(IntPtr handle);
    }

    /// <summary>
    /// Information about a window
    /// </summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public uint ProcessId { get; set; }
        public bool IsVisible { get; set; }
        public bool IsMaximized { get; set; }
        public bool IsMinimized { get; set; }
    }
}
