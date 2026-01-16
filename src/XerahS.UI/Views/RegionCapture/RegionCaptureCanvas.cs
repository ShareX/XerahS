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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp; // SKRect, SKPoint used for API compatibility with existing callers

namespace XerahS.UI.Views.RegionCapture
{
    /// <summary>
    /// Custom control for high-performance rendering of the region capture overlay.
    /// Uses direct Avalonia DrawingContext rendering with CombinedGeometry for XOR-style cutout.
    /// This approach eliminates per-frame bitmap allocations for smooth mouse tracking.
    /// </summary>
    public class RegionCaptureCanvas : Control
    {
        private SKRect _selection;
        private bool _isSelecting;
        private SKPoint _crosshairPosition;

        // Configuration
        private bool _useDarkening = true;
        private const byte DarkenOpacity = 128; // 50% opacity

        // Static brushes and pens for performance (no allocation per frame)
        private static readonly IBrush DimBrush = new SolidColorBrush(Color.FromArgb(DarkenOpacity, 0, 0, 0));
        private static readonly IPen SelectionPenWhite = new Pen(Brushes.White, 1);
        private static readonly IPen SelectionPenBlack = new Pen(Brushes.Black, 1)
        {
            DashStyle = new DashStyle(new double[] { 4, 4 }, 0)
        };
        private static readonly IPen CrosshairPen = new Pen(new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)), 1);

        public RegionCaptureCanvas()
        {
            // Control setup - no per-instance allocations needed
            IsHitTestVisible = false;
        }

        public void UpdateSelection(SKRect selection, bool isSelecting)
        {
            if (_selection == selection && _isSelecting == isSelecting)
                return; // Avoid redundant invalidation

            _selection = selection;
            _isSelecting = isSelecting;
            InvalidateVisual();
        }

        public void UpdateCursor(SKPoint position)
        {
            if (_crosshairPosition == position)
                return; // Avoid redundant invalidation

            _crosshairPosition = position;
            InvalidateVisual();
        }

        public void SetDarkening(bool enable)
        {
            if (_useDarkening == enable)
                return; // Avoid redundant invalidation

            _useDarkening = enable;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // 1. Draw Overlay (Darkening) with XOR-style cutout
            if (_useDarkening)
            {
                if (_isSelecting && !_selection.IsEmpty)
                {
                    // Use CombinedGeometry for efficient XOR-style rendering
                    // This draws the dim overlay everywhere EXCEPT the selection
                    var selectionRect = new Rect(_selection.Left, _selection.Top, _selection.Width, _selection.Height);

                    var outerGeometry = new RectangleGeometry(bounds);
                    var innerGeometry = new RectangleGeometry(selectionRect);
                    var combinedGeometry = new CombinedGeometry(
                        GeometryCombineMode.Exclude,
                        outerGeometry,
                        innerGeometry);

                    context.DrawGeometry(DimBrush, null, combinedGeometry);
                }
                else
                {
                    // Full darkening - no selection
                    context.DrawRectangle(DimBrush, null, bounds);
                }
            }

            // 2. Draw Crosshairs
            if (_crosshairPosition.X >= 0 && _crosshairPosition.Y >= 0)
            {
                // Horizontal line
                context.DrawLine(CrosshairPen,
                    new Point(0, _crosshairPosition.Y),
                    new Point(bounds.Width, _crosshairPosition.Y));

                // Vertical line
                context.DrawLine(CrosshairPen,
                    new Point(_crosshairPosition.X, 0),
                    new Point(_crosshairPosition.X, bounds.Height));
            }

            // 3. Draw Selection Border
            if (_isSelecting && !_selection.IsEmpty)
            {
                var selectionRect = new Rect(_selection.Left, _selection.Top, _selection.Width, _selection.Height);

                // Draw white solid border first
                context.DrawRectangle(null, SelectionPenWhite, selectionRect);

                // Draw black dashed border on top
                context.DrawRectangle(null, SelectionPenBlack, selectionRect);
            }
        }

        public void Cleanup()
        {
            // No resources to dispose - static brushes are shared
        }
    }
}
