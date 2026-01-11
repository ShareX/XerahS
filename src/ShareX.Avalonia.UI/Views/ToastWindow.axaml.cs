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
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using ContentAlignment = System.Drawing.ContentAlignment;
using Size = System.Drawing.Size;

namespace XerahS.UI.Views;

/// <summary>
/// Toast notification window
/// </summary>
public partial class ToastWindow : Window
{
    private ToastViewModel? _viewModel;
    private ToastConfig? _config;
    private bool _isDragging;
    private Avalonia.Point _dragStart;
    private Border? _urlOverlay;

    public ToastWindow()
    {
        InitializeComponent();

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _urlOverlay = this.FindControl<Border>("UrlOverlay");
    }

    public void Initialize(ToastConfig config)
    {
        _config = config;

        // Set window size
        Width = config.Size.Width;
        Height = config.Size.Height;

        // Position window based on placement
        PositionWindow(config.Placement, config.Offset, config.Size);

        // Create and bind ViewModel
        _viewModel = new ToastViewModel(config);
        DataContext = _viewModel;

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.OpacityChanged += OnOpacityChanged;
    }

    private void PositionWindow(ContentAlignment placement, int offset, Size size)
    {
        // Get primary screen working area
        var screen = Screens.Primary;
        if (screen == null) return;

        var workingArea = screen.WorkingArea;
        double x = 0, y = 0;

        switch (placement)
        {
            case ContentAlignment.TopLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + offset;
                break;

            case ContentAlignment.TopCenter:
                x = workingArea.X + (workingArea.Width - size.Width) / 2;
                y = workingArea.Y + offset;
                break;

            case ContentAlignment.TopRight:
                x = workingArea.X + workingArea.Width - size.Width - offset;
                y = workingArea.Y + offset;
                break;

            case ContentAlignment.MiddleLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + (workingArea.Height - size.Height) / 2;
                break;

            case ContentAlignment.MiddleCenter:
                x = workingArea.X + (workingArea.Width - size.Width) / 2;
                y = workingArea.Y + (workingArea.Height - size.Height) / 2;
                break;

            case ContentAlignment.MiddleRight:
                x = workingArea.X + workingArea.Width - size.Width - offset;
                y = workingArea.Y + (workingArea.Height - size.Height) / 2;
                break;

            case ContentAlignment.BottomLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + workingArea.Height - size.Height - offset;
                break;

            case ContentAlignment.BottomCenter:
                x = workingArea.X + (workingArea.Width - size.Width) / 2;
                y = workingArea.Y + workingArea.Height - size.Height - offset;
                break;

            case ContentAlignment.BottomRight:
            default:
                x = workingArea.X + workingArea.Width - size.Width - offset;
                y = workingArea.Y + workingArea.Height - size.Height - offset;
                break;
        }

        Position = new PixelPoint((int)x, (int)y);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            _dragStart = point.Position;
            _isDragging = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        // Only process click if we weren't dragging significantly
        if (_isDragging)
        {
            var distance = Math.Sqrt(
                Math.Pow(point.Position.X - _dragStart.X, 2) +
                Math.Pow(point.Position.Y - _dragStart.Y, 2));

            if (distance < 20) // Click threshold
            {
                if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
                {
                    _viewModel?.ExecuteLeftClick();
                }
                else if (point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased)
                {
                    _viewModel?.ExecuteMiddleClick();
                }
            }
        }

        _isDragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _config == null) return;

        var point = e.GetCurrentPoint(this);
        var distance = Math.Sqrt(
            Math.Pow(point.Position.X - _dragStart.X, 2) +
            Math.Pow(point.Position.Y - _dragStart.Y, 2));

        // Start drag-and-drop if dragged far enough
        if (distance > 20 && !string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            _isDragging = false;

            var dataObject = new DataObject();
            dataObject.Set(DataFormats.Files, new[] { _config.FilePath });

            // Start drag operation
            DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _viewModel?.OnMouseEnter();

        // Show URL overlay if there's a URL
        if (_urlOverlay != null && _viewModel?.HasUrl == true)
        {
            _urlOverlay.Opacity = 1;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _viewModel?.OnMouseLeave();

        // Hide URL overlay
        if (_urlOverlay != null)
        {
            _urlOverlay.Opacity = 0;
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        _viewModel?.OnMenuOpened();
    }

    private void OnFlyoutClosed(object? sender, EventArgs e)
    {
        _viewModel?.OnMenuClosed();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnOpacityChanged(object? sender, double opacity)
    {
        Opacity = opacity;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.OpacityChanged -= OnOpacityChanged;
            _viewModel.Dispose();
        }

        base.OnClosed(e);
    }
}
