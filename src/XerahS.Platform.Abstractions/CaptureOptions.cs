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

using System.Threading;

namespace XerahS.Platform.Abstractions
{
    public class CaptureOptions
    {
        public bool UseModernCapture { get; set; } = true;
        public bool ShowCursor { get; set; } = true;
        /// <summary>
        /// For window captures: capture transparent regions of the window.
        /// This is the original ShareX CaptureTransparent setting.
        /// </summary>
        public bool CaptureTransparent { get; set; } = false;

        /// <summary>
        /// For region captures: use transparent overlay (live desktop visible) vs frozen screenshot background.
        /// True for RectangleTransparent workflow, false for other region capture workflows.
        /// </summary>
        public bool UseTransparentOverlay { get; set; } = false;

        public bool CaptureShadow { get; set; } = true;
        public bool CaptureClientArea { get; set; } = false;

        /// <summary>
        /// ID of the workflow triggering this capture
        /// </summary>
        public string? WorkflowId { get; set; }

        /// <summary>
        /// Workflow category (ScreenCapture, ScreenRecord, etc.) used for logging.
        /// </summary>
        public string? WorkflowCategory { get; set; }

        /// <summary>
        /// Optional delay (in seconds) before capture starts.
        /// </summary>
        public double CaptureStartDelaySeconds { get; set; } = 0;

        /// <summary>
        /// Cancellation token used during capture start delay.
        /// </summary>
        public CancellationToken CaptureStartDelayCancellationToken { get; set; } = CancellationToken.None;
    }
}
