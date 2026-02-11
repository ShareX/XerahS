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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.Services;

namespace XerahS.UI.ViewModels;

public partial class PinnedImageViewModel : ViewModelBase, IDisposable
{
    private readonly SKBitmap _sourceBitmap;
    private readonly PinToScreenOptions _options;
    private readonly int _originalWidth;
    private readonly int _originalHeight;
    private bool _isMinimized;
    private int _preMinimizeScale;
    private bool _disposed;

    public event EventHandler? CloseRequested;
    public event EventHandler? SizeChanged;

    [ObservableProperty]
    private Bitmap? _displayImage;

    [ObservableProperty]
    private double _scaledWidth;

    [ObservableProperty]
    private double _scaledHeight;

    [ObservableProperty]
    private int _scale;

    [ObservableProperty]
    private double _imageOpacity;

    [ObservableProperty]
    private string _scaleText = "100%";

    [ObservableProperty]
    private IBrush _backgroundBrush;

    [ObservableProperty]
    private IBrush _borderBrush;

    [ObservableProperty]
    private Thickness _borderThickness;

    [ObservableProperty]
    private BitmapInterpolationMode _interpolationMode;

    [ObservableProperty]
    private bool _showShadow;

    public int BorderSize => _options.Border ? _options.BorderSize : 0;

    public PinnedImageViewModel(SKBitmap bitmap, PinToScreenOptions options)
    {
        _sourceBitmap = bitmap;
        _options = options;
        _originalWidth = bitmap.Width;
        _originalHeight = bitmap.Height;

        _scale = options.InitialScale;
        _imageOpacity = options.InitialOpacity / 100.0;
        _scaleText = $"{_scale}%";
        _showShadow = options.Shadow;

        _interpolationMode = options.HighQualityScale
            ? BitmapInterpolationMode.HighQuality
            : BitmapInterpolationMode.LowQuality;

        // Convert System.Drawing.Color to Avalonia brushes
        var bgColor = options.BackgroundColor;
        _backgroundBrush = new SolidColorBrush(
            Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));

        if (options.Border)
        {
            var bc = options.BorderColor;
            _borderBrush = new SolidColorBrush(
                Color.FromArgb(bc.A, bc.R, bc.G, bc.B));
            _borderThickness = new Thickness(options.BorderSize);
        }
        else
        {
            _borderBrush = Brushes.Transparent;
            _borderThickness = new Thickness(0);
        }

        // Convert SKBitmap to Avalonia Bitmap
        _displayImage = ConvertToAvaloniaBitmap(bitmap);

        UpdateScaledDimensions();
    }

    public void ScaleBy(int step)
    {
        if (_isMinimized) return;

        Scale = Math.Clamp(Scale + step, 10, 500);
        UpdateScaledDimensions();
        SizeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AdjustOpacity(int stepPercent)
    {
        ImageOpacity = Math.Clamp(ImageOpacity + stepPercent / 100.0, 0.1, 1.0);
    }

    public void ToggleMinimize()
    {
        if (_isMinimized)
        {
            _isMinimized = false;
            Scale = _preMinimizeScale;
            ImageOpacity = _options.InitialOpacity / 100.0;
        }
        else
        {
            _isMinimized = true;
            _preMinimizeScale = Scale;

            // Calculate scale to fit MinimizeSize
            var minW = _options.MinimizeSize.Width;
            var minH = _options.MinimizeSize.Height;
            var scaleW = (int)(minW * 100.0 / _originalWidth);
            var scaleH = (int)(minH * 100.0 / _originalHeight);
            Scale = Math.Max(Math.Min(scaleW, scaleH), 10);

            if (ImageOpacity < 1.0)
            {
                ImageOpacity = 1.0;
            }
        }

        UpdateScaledDimensions();
        SizeChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CopyImage()
    {
        try
        {
            PlatformServices.Clipboard.SetImage(_sourceBitmap);
            DebugHelper.WriteLine("Pinned image copied to clipboard");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to copy pinned image");
        }
    }

    [RelayCommand]
    private void ResetScale()
    {
        if (_isMinimized) return;

        Scale = 100;
        ImageOpacity = 1.0;
        UpdateScaledDimensions();
        SizeChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CloseAll()
    {
        PinToScreenManager.CloseAll();
    }

    private void UpdateScaledDimensions()
    {
        ScaledWidth = _originalWidth * Scale / 100.0;
        ScaledHeight = _originalHeight * Scale / 100.0;
        ScaleText = $"{Scale}%";
    }

    private static Bitmap ConvertToAvaloniaBitmap(SKBitmap skBitmap)
    {
        using var data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisplayImage?.Dispose();
        DisplayImage = null;
        _sourceBitmap.Dispose();
    }
}
