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
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using PixelFormat = Avalonia.Platform.PixelFormat;

namespace XerahS.UI.Views.RegionCapture
{
    /// <summary>
    /// Custom control for high-performance rendering of the region capture overlay using SkiaSharp.
    /// Replaces XAML shapes to avoid rendering artifacts on transparent windows.
    /// </summary>
    public class RegionCaptureCanvas : Control
    {
        private SKBitmap? _renderTarget;
        private SKRect _selection;
        private bool _isSelecting;
        private SKPoint _crosshairPosition;
        
        // Configuration
        private bool _useDarkening = true;
        private const byte DarkenOpacity = 128; // 50% opacity

        // Paints
        private readonly SKPaint _overlayPaint;
        private readonly SKPaint _selectionBorderWhite;
        private readonly SKPaint _selectionBorderBlack;
        private readonly SKPaint _crosshairPaint;

        public RegionCaptureCanvas()
        {
            // Initialize paints
            _overlayPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, DarkenOpacity),
                Style = SKPaintStyle.Fill
            };

            _selectionBorderWhite = new SKPaint
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = false // Sharp lines
            };

            _selectionBorderBlack = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0),
                IsAntialias = false
            };

            _crosshairPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 128), // White, 50% opacity
                StrokeWidth = 1,
                IsAntialias = false
            };
        }

        public void UpdateSelection(SKRect selection, bool isSelecting)
        {
            _selection = selection;
            _isSelecting = isSelecting;
            InvalidateVisual();
        }

        public void UpdateCursor(SKPoint position)
        {
            _crosshairPosition = position;
            InvalidateVisual();
        }
        
        public void SetDarkening(bool enable)
        {
            _useDarkening = enable;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            // Get current render scaling (DPI)
            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

            // Calculate physical pixel size
            int pixelWidth = (int)(Bounds.Width * scaling);
            int pixelHeight = (int)(Bounds.Height * scaling);
            
            if (pixelWidth <= 0 || pixelHeight <= 0) return;

            // Ensure render target matches physical window size
            if (_renderTarget == null || _renderTarget.Width != pixelWidth || _renderTarget.Height != pixelHeight)
            {
                _renderTarget?.Dispose();
                _renderTarget = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            }

            // Draw to internal Skia bitmap (Physical Pixels)
            using (var canvas = new SKCanvas(_renderTarget))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale((float)scaling);
                
                RenderContent(canvas, Bounds.Width, Bounds.Height);
            }

            // Blit to Avalonia DrawingContext
            // Create WriteableBitmap with matching physical size and DPI
            var writeableBitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96 * scaling, 96 * scaling), 
                PixelFormat.Bgra8888, 
                AlphaFormat.Premul);

            using (var frameBuffer = writeableBitmap.Lock())
            {
                var srcPtr = _renderTarget.GetPixels();
                var dstPtr = frameBuffer.Address;
                var size = pixelWidth * pixelHeight * 4;

                var buffer = new byte[size];
                Marshal.Copy(srcPtr, buffer, 0, size);
                Marshal.Copy(buffer, 0, dstPtr, size);
            }

            // Draw bitmap filling the logical bounds
            context.DrawImage(writeableBitmap, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }

        private void RenderContent(SKCanvas canvas, double width, double height)
        {
            // 1. Draw Overlay (Darkening)
            if (_useDarkening)
            {
                if (_isSelecting && !_selection.IsEmpty)
                {
                    // Draw complex path with hole
                    using (var path = new SKPath())
                    {
                        path.FillType = SKPathFillType.EvenOdd;
                        path.AddRect(new SKRect(0, 0, (float)width, (float)height));
                        path.AddRect(_selection);
                        canvas.DrawPath(path, _overlayPaint);
                    }
                }
                else
                {
                    // Full darkening
                    canvas.DrawRect(0, 0, (float)width, (float)height, _overlayPaint);
                }
            }

            // 2. Draw Crosshairs
            // Draw crosshairs unless we have a completed selection (optional, usually crosshairs persist or hide on selection)
            // Behaves like existing implementation: hide if not selecting (or handle externally)
            // For now, let's draw them if we have a position
            if (_crosshairPosition.X >= 0 && _crosshairPosition.Y >= 0)
            {
                canvas.DrawLine(0, _crosshairPosition.Y, (float)width, _crosshairPosition.Y, _crosshairPaint);
                canvas.DrawLine(_crosshairPosition.X, 0, _crosshairPosition.X, (float)height, _crosshairPaint);
            }

            // 3. Draw Selection Border
            if (_isSelecting && !_selection.IsEmpty)
            {
                // Draw white solid border
                canvas.DrawRect(_selection, _selectionBorderWhite);
                
                // Draw black dashed border
                canvas.DrawRect(_selection, _selectionBorderBlack);
            }
        }
        
        public void Cleanup()
        {
             _renderTarget?.Dispose();
             _renderTarget = null;
        }
    }
}
