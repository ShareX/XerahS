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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using XerahS.RegionCapture.Services;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.RegionCapture.UI;

/// <summary>
/// Region-capture magnifier HUD: nearest-neighbor pixel preview, physical-pixel
/// grid, optional square/circle chrome, and pointer info.
/// </summary>
public sealed class MagnifierControl : StackPanel
{
    private readonly Grid _view;
    private readonly Ellipse _circleOuter;
    private readonly Border _squareOuter;
    private readonly Grid _content;
    private readonly Image _image;
    private readonly MagnifierPixelGrid _pixelGrid;
    private readonly Ellipse _circleInner;
    private readonly Border _squareInner;
    private readonly Border _infoPanel;
    private readonly TextBlock _infoText;
    private string? _infoFormat;
    private WriteableBitmap? _bitmap;
    private int _pixelCount = MagnifierLayout.DefaultPixelCount;
    private bool _useSquare;
    private Color _centerPixelColor = Colors.Transparent;

    public MagnifierControl()
    {
        Spacing = 10;
        IsHitTestVisible = false;

        _circleOuter = new Ellipse
        {
            Width = MagnifierLayout.OuterSize,
            Height = MagnifierLayout.OuterSize,
            Fill = Brushes.White
        };
        _squareOuter = new Border
        {
            Width = MagnifierLayout.OuterSize,
            Height = MagnifierLayout.OuterSize,
            Background = Brushes.White,
            IsVisible = false
        };
        _image = new Image
        {
            Width = MagnifierLayout.MagnifierSize,
            Height = MagnifierLayout.MagnifierSize,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.None);

        _pixelGrid = new MagnifierPixelGrid
        {
            AccentBrush = new SolidColorBrush(Color.FromUInt32(0xFF00AEFF)),
            IsHitTestVisible = false
        };
        RenderOptions.SetEdgeMode(_pixelGrid, EdgeMode.Aliased);

        _content = new Grid
        {
            Width = MagnifierLayout.MagnifierSize,
            Height = MagnifierLayout.MagnifierSize,
            Clip = new EllipseGeometry(new Rect(0, 0, MagnifierLayout.MagnifierSize, MagnifierLayout.MagnifierSize))
        };
        _content.Children.Add(_image);
        _content.Children.Add(_pixelGrid);

        _circleInner = new Ellipse
        {
            Width = MagnifierLayout.MagnifierSize,
            Height = MagnifierLayout.MagnifierSize,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        _squareInner = new Border
        {
            Width = MagnifierLayout.MagnifierSize,
            Height = MagnifierLayout.MagnifierSize,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            IsVisible = false,
            IsHitTestVisible = false
        };
        RenderOptions.SetEdgeMode(_squareInner, EdgeMode.Aliased);

        _view = new Grid
        {
            Width = MagnifierLayout.OuterSize,
            Height = MagnifierLayout.OuterSize
        };
        _view.Children.Add(_circleOuter);
        _view.Children.Add(_squareOuter);
        _view.Children.Add(_content);
        _view.Children.Add(_circleInner);
        _view.Children.Add(_squareInner);

        _infoText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.White
        };
        _infoPanel = new Border
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Padding = new Thickness(7, 4),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30)),
            Child = _infoText
        };

        Children.Add(_view);
        Children.Add(_infoPanel);

        RecreateBitmap(_pixelCount);
    }

    internal MagnifierPixelGrid PixelGridForTests => _pixelGrid;
    internal bool UsesSquareShapeForTests => _useSquare;
    internal int PixelCountForTests => _pixelCount;
    internal bool MagnifierViewVisibleForTests => _view.IsVisible;
    internal bool InfoVisibleForTests => _infoPanel.IsVisible;
    internal string InfoTextForTests => _infoText.Text ?? string.Empty;
    internal Color CenterPixelColorForTests => _centerPixelColor;

    public void ApplyShape(bool useSquare)
    {
        _useSquare = useSquare;
        _circleOuter.IsVisible = !useSquare;
        _circleInner.IsVisible = !useSquare;
        _squareOuter.IsVisible = useSquare;
        _squareInner.IsVisible = useSquare;
        _content.Clip = useSquare
            ? null
            : new EllipseGeometry(new Rect(0, 0, MagnifierLayout.MagnifierSize, MagnifierLayout.MagnifierSize));
    }

    public void SetAccentBrush(IBrush brush) => _pixelGrid.AccentBrush = brush;

    /// <summary>Uses a pixel-info token format ($x, $y, $r, $hex, $n, ...) for the HUD text; null restores the default.</summary>
    public void SetInfoFormat(string? format) => _infoFormat = string.IsNullOrWhiteSpace(format) ? null : format;

    internal static string FormatInfo(string? format, PixelPoint physicalCursor, Color color)
    {
        if (format is null)
        {
            return $"X: {physicalCursor.X:F0} Y: {physicalCursor.Y:F0}\n#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        return XerahS.Common.CodeMenuEntryPixelInfo.Parse(
            format,
            System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B),
            new System.Drawing.Point((int)Math.Round(physicalCursor.X), (int)Math.Round(physicalCursor.Y)));
    }

    public void SetHudVisibility(bool showMagnifier, bool showInfo)
    {
        _view.IsVisible = showMagnifier;
        _infoPanel.IsVisible = showInfo;
        IsVisible = showMagnifier || showInfo;
    }

    public void SetPixelCount(int count)
    {
        if (_pixelCount == count && _bitmap is not null)
        {
            _pixelGrid.PixelCount = count;
            return;
        }

        RecreateBitmap(count);
    }

    public void PositionNearPointer(Point pointer, Size viewport, double renderScale)
    {
        double width = Bounds.Width > 0 ? Bounds.Width : 170;
        double height = Bounds.Height > 0 ? Bounds.Height : 205;
        double x = pointer.X + MagnifierLayout.PointerOffset;
        double y = pointer.Y + MagnifierLayout.PointerOffset;

        if (x + width > viewport.Width)
        {
            x = pointer.X - width - MagnifierLayout.PointerOffset;
        }

        if (y + height > viewport.Height)
        {
            y = pointer.Y - height - MagnifierLayout.PointerOffset;
        }

        double scale = double.IsFinite(renderScale) && renderScale > 0 ? Math.Max(1, renderScale) : 1;
        double targetX = Math.Clamp(x, 0, Math.Max(0, viewport.Width - width));
        double targetY = Math.Clamp(y, 0, Math.Max(0, viewport.Height - height));
        targetX = Math.Round(targetX * scale) / scale;
        targetY = Math.Round(targetY * scale) / scale;

        Canvas.SetLeft(this, targetX);
        Canvas.SetTop(this, targetY);
        _pixelGrid.InvalidateVisual();
    }

    public unsafe void UpdateFromBackground(PixelPoint physicalCursor, SKBitmap? background, PixelRect virtualBounds)
    {
        EnsureBitmap();
        if (_bitmap is null)
        {
            return;
        }

        int count = _bitmap.PixelSize.Width;
        int radius = count / 2;

        using ILockedFramebuffer framebuffer = _bitmap.Lock();
        byte* destination = (byte*)framebuffer.Address;

        if (background is null || background.Width <= 0 || background.Height <= 0)
        {
            int length = framebuffer.RowBytes * count;
            new Span<byte>(destination, length).Clear();
            _centerPixelColor = Colors.Transparent;
            _infoText.Text = $"X: {physicalCursor.X:F0} Y: {physicalCursor.Y:F0}";
            _image.InvalidateVisual();
            return;
        }

        int sourceWidth = background.Width;
        int sourceHeight = background.Height;
        var (centerX, centerY) = BackgroundBitmapMapper.MapPointToBitmap(
            physicalCursor,
            virtualBounds,
            sourceWidth,
            sourceHeight);

        for (int y = 0; y < count; y++)
        {
            byte* row = destination + y * framebuffer.RowBytes;
            int sourceY = Math.Clamp(centerY + y - radius, 0, sourceHeight - 1);

            for (int x = 0; x < count; x++)
            {
                int sourceX = Math.Clamp(centerX + x - radius, 0, sourceWidth - 1);
                SKColor color = background.GetPixel(sourceX, sourceY);
                int offset = x * 4;
                row[offset] = color.Blue;
                row[offset + 1] = color.Green;
                row[offset + 2] = color.Red;
                row[offset + 3] = color.Alpha;

                if (x == radius && y == radius)
                {
                    _centerPixelColor = Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
                }
            }
        }

        _infoText.Text = FormatInfo(_infoFormat, physicalCursor, _centerPixelColor);
        _image.InvalidateVisual();
    }

    internal static Point CalculatePosition(Point pointer, Size viewport, Size hudSize, double renderScale)
    {
        double width = hudSize.Width > 0 ? hudSize.Width : 170;
        double height = hudSize.Height > 0 ? hudSize.Height : 205;
        double x = pointer.X + MagnifierLayout.PointerOffset;
        double y = pointer.Y + MagnifierLayout.PointerOffset;

        if (x + width > viewport.Width)
        {
            x = pointer.X - width - MagnifierLayout.PointerOffset;
        }

        if (y + height > viewport.Height)
        {
            y = pointer.Y - height - MagnifierLayout.PointerOffset;
        }

        double scale = double.IsFinite(renderScale) && renderScale > 0 ? Math.Max(1, renderScale) : 1;
        double targetX = Math.Clamp(x, 0, Math.Max(0, viewport.Width - width));
        double targetY = Math.Clamp(y, 0, Math.Max(0, viewport.Height - height));
        return new Point(Math.Round(targetX * scale) / scale, Math.Round(targetY * scale) / scale);
    }

    private void EnsureBitmap()
    {
        if (_bitmap?.PixelSize == new PixelSize(_pixelCount, _pixelCount))
        {
            return;
        }

        RecreateBitmap(_pixelCount);
    }

    private void RecreateBitmap(int count)
    {
        _pixelCount = count;
        if (_bitmap?.PixelSize == new PixelSize(count, count))
        {
            _pixelGrid.PixelCount = count;
            return;
        }

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(
            new PixelSize(count, count),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        _image.Source = _bitmap;
        _pixelGrid.PixelCount = count;
    }
}
