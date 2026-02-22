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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class PinnedImageWindow : Window
{
    private PinnedImageViewModel? _viewModel;
    private PinToScreenOptions? _options;
    private bool _isDragging;
    private Avalonia.Point _dragStartPoint;
    private PixelPoint _windowStartPosition;

    public PinnedImageWindow()
    {
        InitializeComponent();
        DoubleTapped += OnDoubleTapped;
    }

    public void Initialize(PinnedImageViewModel viewModel, PixelPoint? location, PinToScreenOptions options)
    {
        _viewModel = viewModel;
        _options = options;
        DataContext = viewModel;

        Topmost = options.TopMost;

        RenderOptions.SetBitmapInterpolationMode(PinnedImage, viewModel.InterpolationMode);

        viewModel.CloseRequested += (_, _) => Close();
        viewModel.SizeChanged += (_, _) => UpdateWindowSize();

        UpdateWindowSize();

        if (location.HasValue)
        {
            var pos = location.Value;
            if (options.Border)
            {
                pos = new PixelPoint(pos.X - options.BorderSize, pos.Y - options.BorderSize);
            }
            Position = pos;
        }
        else
        {
            PositionByPlacement(options.Placement, options.PlacementOffset);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _windowStartPosition = Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isDragging)
        {
            var currentPos = e.GetPosition(this);
            var deltaX = currentPos.X - _dragStartPoint.X;
            var deltaY = currentPos.Y - _dragStartPoint.Y;
            Position = new PixelPoint(
                _windowStartPosition.X + (int)deltaX,
                _windowStartPosition.Y + (int)deltaY);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        _viewModel?.ToggleMinimize();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (_viewModel == null || _options == null) return;

        var step = e.Delta.Y > 0 ? _options.ScaleStep : -_options.ScaleStep;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _viewModel.AdjustOpacity(step);
        }
        else
        {
            var centerBefore = _options.KeepCenterLocation
                ? new PixelPoint(Position.X + (int)(Width / 2), Position.Y + (int)(Height / 2))
                : (PixelPoint?)null;

            _viewModel.ScaleBy(step);

            if (centerBefore.HasValue)
            {
                Position = new PixelPoint(
                    centerBefore.Value.X - (int)(Width / 2),
                    centerBefore.Value.Y - (int)(Height / 2));
            }
        }

        e.Handled = true;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        HoverToolbar.IsVisible = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HoverToolbar.IsVisible = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_viewModel == null) return;

        int nudge = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;

        switch (e.Key)
        {
            case Key.Left:
                Position = new PixelPoint(Position.X - nudge, Position.Y);
                e.Handled = true;
                break;
            case Key.Right:
                Position = new PixelPoint(Position.X + nudge, Position.Y);
                e.Handled = true;
                break;
            case Key.Up:
                Position = new PixelPoint(Position.X, Position.Y - nudge);
                e.Handled = true;
                break;
            case Key.Down:
                Position = new PixelPoint(Position.X, Position.Y + nudge);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _viewModel.CopyImageCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= null;
            _viewModel.SizeChanged -= null;
            _viewModel.Dispose();
            _viewModel = null;
        }
    }

    private void UpdateWindowSize()
    {
        if (_viewModel == null) return;

        var border = _viewModel.BorderSize * 2;
        Width = _viewModel.ScaledWidth + border;
        Height = _viewModel.ScaledHeight + border;
    }

    private void PositionByPlacement(ContentPlacement placement, int offset)
    {
        var screen = Screens.Primary;
        if (screen == null) return;

        var workingArea = screen.WorkingArea;
        var w = (int)Width;
        var h = (int)Height;
        double x = 0, y = 0;

        switch (placement)
        {
            case ContentPlacement.TopLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + offset;
                break;
            case ContentPlacement.TopCenter:
                x = workingArea.X + (workingArea.Width - w) / 2;
                y = workingArea.Y + offset;
                break;
            case ContentPlacement.TopRight:
                x = workingArea.X + workingArea.Width - w - offset;
                y = workingArea.Y + offset;
                break;
            case ContentPlacement.MiddleLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + (workingArea.Height - h) / 2;
                break;
            case ContentPlacement.MiddleCenter:
                x = workingArea.X + (workingArea.Width - w) / 2;
                y = workingArea.Y + (workingArea.Height - h) / 2;
                break;
            case ContentPlacement.MiddleRight:
                x = workingArea.X + workingArea.Width - w - offset;
                y = workingArea.Y + (workingArea.Height - h) / 2;
                break;
            case ContentPlacement.BottomLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + workingArea.Height - h - offset;
                break;
            case ContentPlacement.BottomCenter:
                x = workingArea.X + (workingArea.Width - w) / 2;
                y = workingArea.Y + workingArea.Height - h - offset;
                break;
            case ContentPlacement.BottomRight:
            default:
                x = workingArea.X + workingArea.Width - w - offset;
                y = workingArea.Y + workingArea.Height - h - offset;
                break;
        }

        Position = new PixelPoint((int)x, (int)y);
    }
}
