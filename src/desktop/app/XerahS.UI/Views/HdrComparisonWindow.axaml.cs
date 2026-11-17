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
using Avalonia.Interactivity;
using XerahS.Platform.Abstractions;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

/// <summary>
/// Compares HDR → SDR methods on a real photograph. On Windows the untouched HDR image is embedded
/// beside the tone-mapped rendering as a native scRGB surface, so both are visible at once without
/// raising a second window.
/// </summary>
public partial class HdrComparisonWindow : Window
{
    private IHdrSurface? _hdrSurface;

    public HdrComparisonWindow()
    {
        InitializeComponent();
        DataContext = new HdrComparisonViewModel();

        HdrHost.SizeChanged += (_, _) => UpdateSurfaceBounds();
        SizeChanged += (_, _) => UpdateSurfaceBounds();
    }

    private void OnNativeHdrToggled(object? sender, RoutedEventArgs e)
    {
        bool showNative = NativeHdrToggle.IsChecked == true;

        if (showNative && !EnsureSurface())
        {
            NativeHdrToggle.IsChecked = false;
            return;
        }

        HdrHost.IsVisible = showNative;
        Grid.SetColumn(SdrHost, showNative ? 1 : 0);
        Grid.SetColumnSpan(SdrHost, showNative ? 1 : 2);

        _hdrSurface?.SetVisible(showNative);
        if (showNative)
        {
            UpdateSurfaceBounds();
        }
    }

    private bool EnsureSurface()
    {
        if (_hdrSurface != null)
        {
            return true;
        }

        if (TryGetPlatformHandle() is not { } handle)
        {
            return false;
        }

        if (!HdrPhotoPreviewCache.TryGetSourcePixels(out byte[] pixels, out int width, out int height, out int rowBytes))
        {
            return false;
        }

        _hdrSurface = PlatformServices.HdrSurfaces?.Create(handle.Handle, pixels, width, height, rowBytes);
        return _hdrSurface != null;
    }

    /// <summary>
    /// Keeps the child HWND on top of its placeholder. Avalonia works in device-independent units,
    /// Win32 in physical pixels, hence the render scaling.
    /// </summary>
    private void UpdateSurfaceBounds()
    {
        if (_hdrSurface == null || !HdrHost.IsVisible)
        {
            return;
        }

        if (HdrHost.TranslatePoint(new Point(0, 0), this) is not { } origin)
        {
            return;
        }

        double areaWidth = HdrHost.Bounds.Width;
        double areaHeight = HdrHost.Bounds.Height;
        if (areaWidth <= 0 || areaHeight <= 0)
        {
            return;
        }

        // Keep the image's own aspect ratio inside the placeholder instead of stretching it.
        double aspect = 2.0;
        if (SdrImage.Source is Avalonia.Media.Imaging.Bitmap bitmap && bitmap.PixelSize.Height > 0)
        {
            aspect = (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;
        }

        double width = areaWidth;
        double height = width / aspect;
        if (height > areaHeight)
        {
            height = areaHeight;
            width = height * aspect;
        }

        double offsetX = origin.X + (areaWidth - width) / 2;
        double offsetY = origin.Y + (areaHeight - height) / 2;
        double scaling = RenderScaling;

        _hdrSurface.SetBounds(
            (int)Math.Round(offsetX * scaling),
            (int)Math.Round(offsetY * scaling),
            Math.Max(1, (int)Math.Round(width * scaling)),
            Math.Max(1, (int)Math.Round(height * scaling)));
    }

    protected override void OnClosed(EventArgs e)
    {
        _hdrSurface?.Dispose();
        _hdrSurface = null;
        base.OnClosed(e);
    }
}
