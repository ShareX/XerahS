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

using System.ComponentModel;

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Platform-agnostic service for scroll simulation and scroll bar queries.
    /// Used by the scrolling capture manager to programmatically scroll windows.
    /// </summary>
    public interface IScrollingCaptureService
    {
        /// <summary>
        /// Whether scrolling capture is supported on this platform.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Scrolls the specified window using the given method and amount.
        /// </summary>
        /// <param name="windowHandle">Target window handle</param>
        /// <param name="method">Scroll method to use</param>
        /// <param name="amount">Number of scroll units</param>
        Task ScrollWindowAsync(IntPtr windowHandle, ScrollMethod method, int amount);

        /// <summary>
        /// Scrolls the specified window to the top of its content.
        /// </summary>
        /// <param name="windowHandle">Target window handle</param>
        Task ScrollToTopAsync(IntPtr windowHandle);

        /// <summary>
        /// Gets scroll bar position and range information for the specified window.
        /// </summary>
        /// <param name="windowHandle">Target window handle</param>
        /// <returns>Scroll bar info, or null if the window has no scrollbar</returns>
        ScrollBarInfo? GetScrollBarInfo(IntPtr windowHandle);
    }

    /// <summary>
    /// Scroll bar position and range information for a window.
    /// </summary>
    public record ScrollBarInfo(int Position, int MinRange, int MaxRange, int PageSize)
    {
        /// <summary>
        /// Whether the scroll bar is at the bottom of its range.
        /// </summary>
        public bool IsAtBottom => MaxRange <= Position + PageSize - 1;
    }

    /// <summary>
    /// Method used to scroll a window during scrolling capture.
    /// </summary>
    public enum ScrollMethod // Localized
    {
        [Description("Mouse wheel")]
        MouseWheel,
        [Description("Down arrow key")]
        DownArrow,
        [Description("Page down key")]
        PageDown,
        [Description("Scroll message")]
        ScrollMessage
    }

    /// <summary>
    /// Status of a scrolling capture operation.
    /// </summary>
    public enum ScrollingCaptureStatus
    {
        Failed,
        PartiallySuccessful,
        Successful
    }

    /// <summary>
    /// Result of a scrolling capture operation.
    /// </summary>
    public class ScrollingCaptureResult
    {
        public SkiaSharp.SKBitmap? Image { get; set; }
        public ScrollingCaptureStatus Status { get; set; }
        public int FramesCaptured { get; set; }
    }

    /// <summary>
    /// Progress data reported during a scrolling capture operation.
    /// </summary>
    public class ScrollingCaptureProgress
    {
        public int FramesCaptured { get; set; }
        public SkiaSharp.SKBitmap? LatestFrame { get; set; }
    }
}
