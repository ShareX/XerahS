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

using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for the toast notification window
/// </summary>
public partial class ToastViewModel : ObservableObject, IDisposable
{
    private readonly ToastConfig _config;
    private readonly DispatcherTimer _durationTimer;
    private readonly DispatcherTimer _fadeTimer;
    private readonly int _fadeInterval = 50;
    private double _opacity = 1.0;
    private double _opacityDecrement;
    private bool _isDurationEnd;
    private bool _isMouseInside;
    private bool _isMenuOpen;
    private bool _disposed;

    public event EventHandler? CloseRequested;
    public event EventHandler<double>? OpacityChanged;

    [ObservableProperty]
    private Bitmap? _image;

    [ObservableProperty]
    private string? _text;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _url;

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private bool _hasUrl;

    // Commands for context menu (using ContextFlyout/MenuFlyout)
    public ICommand CopyImageToClipboardCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand CopyFilePathCommand { get; }
    public ICommand CopyUrlCommand { get; }

    public ToastViewModel(ToastConfig config)
    {
        _config = config;

        // Try to load image from path
        if (!string.IsNullOrEmpty(config.ImagePath) && File.Exists(config.ImagePath))
        {
            try
            {
                Image = new Bitmap(config.ImagePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load toast image");
            }
        }

        // Initialize display properties
        Text = config.Text;
        Title = config.Title;
        Url = config.URL;
        HasImage = Image != null;
        HasUrl = !string.IsNullOrEmpty(config.URL);

        // Initialize context menu commands
        CopyImageToClipboardCommand = new RelayCommand(CopyImageToClipboard);
        OpenFileCommand = new RelayCommand(OpenFile);
        CopyFilePathCommand = new RelayCommand(CopyFilePath);
        CopyUrlCommand = new RelayCommand(CopyUrl);

        // Calculate fade decrement
        if (config.FadeDuration > 0)
        {
            _opacityDecrement = (double)_fadeInterval / (config.FadeDuration * 1000);
        }

        // Setup duration timer
        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(config.Duration)
        };
        _durationTimer.Tick += OnDurationTick;

        // Setup fade timer
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_fadeInterval)
        };
        _fadeTimer.Tick += OnFadeTick;

        // Start duration timer if auto-hide is enabled
        if (config.AutoHide && config.Duration > 0)
        {
            _durationTimer.Start();
        }
    }

    public void OnMenuOpened()
    {
        _isMenuOpen = true;
        _fadeTimer.Stop();

        // Reset opacity
        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);
    }

    public void OnMenuClosed()
    {
        _isMenuOpen = false;
        CheckFade();
    }

    public void OnMouseEnter()
    {
        _isMouseInside = true;
        _fadeTimer.Stop();

        // Reset opacity
        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);
    }

    public void OnMouseLeave()
    {
        _isMouseInside = false;
        CheckFade();
    }

    public void ExecuteLeftClick()
    {
        ExecuteAction(_config.LeftClickAction);
    }

    public void ExecuteRightClick()
    {
        // Right click opens context menu, handled by view
    }

    public void ExecuteMiddleClick()
    {
        ExecuteAction(_config.MiddleClickAction);
    }

    private void OnDurationTick(object? sender, EventArgs e)
    {
        _durationTimer.Stop();
        _isDurationEnd = true;

        if (!_isMouseInside)
        {
            CheckFade();
        }
    }

    private void CheckFade()
    {
        if (_isDurationEnd && _config.AutoHide && !_isMouseInside && !_isMenuOpen)
        {
            StartFade();
        }
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        if (_opacity > _opacityDecrement)
        {
            _opacity -= _opacityDecrement;
            OpacityChanged?.Invoke(this, _opacity);
        }
        else
        {
            _fadeTimer.Stop();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StartFade()
    {
        if (_config.FadeDuration <= 0)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _opacity = 1.0;
            OpacityChanged?.Invoke(this, _opacity);
            _fadeTimer.Start();
        }
    }

    private void ExecuteAction(ToastClickAction action)
    {
        _durationTimer.Stop();
        _fadeTimer.Stop();

        switch (action)
        {
            case ToastClickAction.OpenFile:
                OpenFile();
                break;

            case ToastClickAction.OpenFolder:
                OpenFolder();
                break;

            case ToastClickAction.OpenUrl:
                OpenUrl();
                break;

            case ToastClickAction.CopyImageToClipboard:
                CopyImageToClipboard();
                break;

            case ToastClickAction.CopyFile:
                CopyFile();
                break;

            case ToastClickAction.CopyFilePath:
                CopyFilePath();
                break;

            case ToastClickAction.CopyUrl:
                CopyUrl();
                break;

            case ToastClickAction.AnnotateImage:
                AnnotateImage();
                break;

            case ToastClickAction.Upload:
                UploadFile();
                break;

            case ToastClickAction.PinToScreen:
                PinToScreen();
                break;

            case ToastClickAction.DeleteFile:
                DeleteFile();
                break;

            case ToastClickAction.CloseNotification:
            default:
                break;
        }

        // Close after action (unless it's a no-op close action that already closes)
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenFile()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_config.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to open file from toast");
            }
        }
    }

    private void OpenFolder()
    {
        if (!string.IsNullOrEmpty(_config.FilePath))
        {
            try
            {
                FileHelpers.OpenFolderWithFile(_config.FilePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to open folder from toast");
            }
        }
    }

    private void OpenUrl()
    {
        if (!string.IsNullOrEmpty(_config.URL))
        {
            try
            {
                URLHelpers.OpenURL(_config.URL);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to open URL from toast");
            }
        }
    }

    private void CopyImageToClipboard()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                using var bitmap = SKBitmap.Decode(_config.FilePath);
                if (bitmap != null)
                {
                    PlatformServices.Clipboard.SetImage(bitmap);
                    DebugHelper.WriteLine($"Copied image to clipboard: {_config.FilePath}");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy image to clipboard from toast");
            }
        }
    }

    private void CopyFile()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                PlatformServices.Clipboard.SetFileDropList(new[] { _config.FilePath });
                DebugHelper.WriteLine($"Copied file to clipboard: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy file to clipboard from toast");
            }
        }
    }

    private void CopyFilePath()
    {
        if (!string.IsNullOrEmpty(_config.FilePath))
        {
            try
            {
                PlatformServices.Clipboard.SetText(_config.FilePath);
                DebugHelper.WriteLine($"Copied file path to clipboard: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy file path from toast");
            }
        }
    }

    private void CopyUrl()
    {
        if (!string.IsNullOrEmpty(_config.URL))
        {
            try
            {
                PlatformServices.Clipboard.SetText(_config.URL);
                DebugHelper.WriteLine($"Copied URL to clipboard: {_config.URL}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy URL from toast");
            }
        }
    }

    private async void AnnotateImage()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && FileHelpers.IsImageFile(_config.FilePath))
        {
            try
            {
                // Load image and use UI service to open editor
                using var bitmap = SKBitmap.Decode(_config.FilePath);
                if (bitmap != null)
                {
                    await PlatformServices.UI.ShowEditorAsync(bitmap);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to annotate image from toast");
            }
        }
    }

    private void UploadFile()
    {
        if (!string.IsNullOrEmpty(_config.FilePath))
        {
            try
            {
                // TODO: Implement upload through TaskManager
                DebugHelper.WriteLine($"Upload requested for: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to upload file from toast");
            }
        }
    }

    private void PinToScreen()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && FileHelpers.IsImageFile(_config.FilePath))
        {
            try
            {
                // TODO: Implement pin to screen
                DebugHelper.WriteLine($"Pin to screen requested for: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to pin image from toast");
            }
        }
    }

    private void DeleteFile()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                // TODO: Add confirmation dialog
                File.Delete(_config.FilePath);
                DebugHelper.WriteLine($"Deleted file: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to delete file from toast");
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _durationTimer.Stop();
            _fadeTimer.Stop();
            _disposed = true;
        }
    }
}
