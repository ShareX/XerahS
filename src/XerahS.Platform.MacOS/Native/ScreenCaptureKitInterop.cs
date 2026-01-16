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

using System.Runtime.InteropServices;

namespace XerahS.Platform.MacOS.Native
{
    /// <summary>
    /// P/Invoke bindings for the native ScreenCaptureKit bridge library.
    /// </summary>
    internal static partial class ScreenCaptureKitInterop
    {
        private const string LibraryName = "libscreencapturekit_bridge";

        // Error codes from native library
        public const int SUCCESS = 0;
        public const int ERROR_NOT_AVAILABLE = -1;
        public const int ERROR_PERMISSION_DENIED = -2;
        public const int ERROR_CAPTURE_FAILED = -3;
        public const int ERROR_ENCODING_FAILED = -4;

        /// <summary>
        /// Check if ScreenCaptureKit is available on this system.
        /// </summary>
        /// <returns>1 if available (macOS 12.3+), 0 otherwise</returns>
        [LibraryImport(LibraryName, EntryPoint = "sck_is_available")]
        public static partial int IsAvailable();

        /// <summary>
        /// Capture the entire screen as PNG data.
        /// </summary>
        [LibraryImport(LibraryName, EntryPoint = "sck_capture_fullscreen")]
        public static partial int CaptureFullscreen(out IntPtr outData, out int outLength);

        /// <summary>
        /// Capture a rectangular region of the screen as PNG data.
        /// </summary>
        [LibraryImport(LibraryName, EntryPoint = "sck_capture_rect")]
        public static partial int CaptureRect(float x, float y, float w, float h, out IntPtr outData, out int outLength);

        /// <summary>
        /// Capture a specific window by window ID as PNG data.
        /// </summary>
        [LibraryImport(LibraryName, EntryPoint = "sck_capture_window")]
        public static partial int CaptureWindow(uint windowId, out IntPtr outData, out int outLength);

        /// <summary>
        /// Free a buffer allocated by capture functions.
        /// </summary>
        [LibraryImport(LibraryName, EntryPoint = "sck_free_buffer")]
        public static partial void FreeBuffer(IntPtr data);

        /// <summary>
        /// Check if the native library is loaded and available.
        /// </summary>
        public static bool TryLoad()
        {
            try
            {
                return IsAvailable() >= 0; // Returns 0 or 1 if library loaded, throws if library not found
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Get error message for a result code.
        /// </summary>
        public static string GetErrorMessage(int resultCode)
        {
            return resultCode switch
            {
                SUCCESS => "Success",
                ERROR_NOT_AVAILABLE => "ScreenCaptureKit not available (requires macOS 12.3+)",
                ERROR_PERMISSION_DENIED => "Screen recording permission denied",
                ERROR_CAPTURE_FAILED => "Screen capture failed",
                ERROR_ENCODING_FAILED => "PNG encoding failed",
                _ => $"Unknown error: {resultCode}"
            };
        }
    }
}
