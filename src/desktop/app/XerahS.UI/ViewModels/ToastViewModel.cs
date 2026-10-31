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
using ShareX.ImageEditor.Presentation.Theming;
using SkiaSharp;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.History;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for the toast notification window
/// </summary>
public partial class ToastViewModel : ObservableObject, IDisposable
{
    private readonly ToastConfig _config;
    private readonly IDesktopTaskManager? _taskManager;
    private readonly HistoryViewModel? _historyViewModel;
    private readonly HistoryItem? _historyItem;
    private readonly DispatcherTimer _durationTimer;
    private readonly DispatcherTimer _fadeTimer;
    private readonly int _fadeInterval = 50;
    private double _opacity = 1.0;
    private double _opacityDecrement;
    private bool _isDurationEnd;
    private bool _isMouseInside;
    private bool _isHovered;
    private readonly DispatcherTimer _leaveGraceTimer;
    private readonly DispatcherTimer _deferredCloseTimer;
    private bool _isMenuOpen;
    private bool _isFileDragActive;
    private bool _disposed;

    public event EventHandler? CloseRequested;
    public event EventHandler<double>? OpacityChanged;

    /// <summary>
    /// Raised when the hover state used by the view (hover toolbar, output chip, header
    /// overlay) changes. This is the single hover signal for the toast: the fade/close logic
    /// and every hover visual follow it, so they can never disagree.
    /// </summary>
    public event EventHandler<bool>? HoverChanged;

    public bool IsHovered => _isHovered;

    /// <summary>
    /// How long a pointer-leave is held back before it counts, on platforms where
    /// <see cref="UseLeaveGrace"/> is on.
    /// </summary>
    internal static readonly TimeSpan DefaultLeaveGrace = TimeSpan.FromMilliseconds(250);

    // On Linux, Avalonia talks X11 (XWayland under Hyprland and other Wayland compositors).
    // X11 crossing events there are not as dependable as Win32's WM_MOUSELEAVE: a leave can
    // be reported while the pointer is still over the toast (e.g. our own tooltip/menu popup
    // windows mapping, or the compositor moving pointer focus as it re-focuses/restacks the
    // window), and the matching enter only comes back on the next pointer motion. On Windows a
    // false leave was harmless because the fade it started was cancelled by the next enter;
    // on Linux the toast closed outright. So on Linux a leave is debounced, and a pending
    // close can still be cancelled by hovering again.
    internal bool UseLeaveGrace { get; set; } = OperatingSystem.IsLinux();

    // On Linux the compositor can override per-window Opacity, so there is no visible fade.
    // Instead the toast stays fully visible for FadeDuration and then closes; hovering again
    // during that time cancels the close, just as hovering during the fade does on Windows.
    internal bool UseDeferredClose { get; set; } = OperatingSystem.IsLinux();

    internal TimeSpan LeaveGrace
    {
        get => _leaveGraceTimer.Interval;
        set => _leaveGraceTimer.Interval = value;
    }

    internal bool IsMouseInside => _isMouseInside;
    internal bool IsLeavePending => _leaveGraceTimer.IsEnabled;
    internal bool IsDeferredCloseRunning => _deferredCloseTimer.IsEnabled;

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

    [ObservableProperty]
    private string? _headerText;

    [ObservableProperty]
    private bool _hasHeaderText;

    [ObservableProperty]
    private string? _errorDetails;

    [ObservableProperty]
    private bool _hasErrors;

    /// <summary>
    /// Buttons shown in the hover toolbar, filtered to actions that can run for this toast.
    /// </summary>
    public IReadOnlyList<ToastActionButtonViewModel> ActionButtons { get; }
    public bool HasActionButtons => ActionButtons.Count > 0;
    public double ActionButtonSize { get; }
    public double ActionButtonIconSize => ActionButtonSize / 2d;
    public Avalonia.CornerRadius ActionButtonCornerRadius => new(Math.Clamp(ActionButtonSize / 4d, 4, 16));
    public Avalonia.Thickness TextContentMargin
    {
        get
        {
            double bottom = HasActionButtons ? ActionButtonSize + 20 : 12;
            if (HasOutputChip)
            {
                // Keep text clear of the output chip in the bottom-right corner.
                bottom = Math.Max(bottom, 12 + OutputChipReservedHeight);
            }

            return new Avalonia.Thickness(12, 12, 12, bottom);
        }
    }

    internal const double OutputChipReservedHeight = 30;
    internal const int OutputChipLabelMinWidth = 180;

    public ToastOutputKind OutputKind => _config.OutputKind;
    public bool HasOutputChip => OutputKind != ToastOutputKind.None;
    public string OutputChipIcon => GetOutputChipIcon(OutputKind);
    public string OutputChipLabel => GetOutputChipLabel(OutputKind);
    public bool ShowOutputChipLabel => _config.Size.Width >= OutputChipLabelMinWidth;
    public bool IsOutputChipWarning => OutputKind == ToastOutputKind.UploadFailedLocal;
    public string OutputChipAccessibleName => $"Output: {OutputChipLabel}";

    internal static string GetOutputChipIcon(ToastOutputKind kind) => kind switch
    {
        ToastOutputKind.Uploaded => LucideIcons.link,
        ToastOutputKind.Local => LucideIcons.hard_drive,
        ToastOutputKind.UploadFailedLocal => LucideIcons.cloud_off,
        ToastOutputKind.ClipboardOnly => LucideIcons.clipboard,
        _ => string.Empty
    };

    internal static string GetOutputChipLabel(ToastOutputKind kind) => kind switch
    {
        ToastOutputKind.Uploaded => "Online",
        ToastOutputKind.Local => "Local",
        ToastOutputKind.UploadFailedLocal => "Not uploaded",
        ToastOutputKind.ClipboardOnly => "Clipboard",
        _ => string.Empty
    };

    /// <summary>
    /// Hover header text: the link or local path, prefixed so it says what it is.
    /// </summary>
    internal static string? FormatHeaderText(ToastOutputKind kind, string? url, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            return kind == ToastOutputKind.None ? url : $"Link: {url}";
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return kind switch
        {
            ToastOutputKind.UploadFailedLocal => $"Upload failed, saved to: {filePath}",
            ToastOutputKind.Local => $"Saved to: {filePath}",
            _ => filePath
        };
    }

    // Commands for context menu (shared with History - same MenuFlyout)
    public ICommand EditImageCommand { get; }
    public ICommand CopyImageToClipboardCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand UploadItemCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand CopyFilePathCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand CopyMarkdownImageCommand { get; }
    public ICommand CopyErrorsCommand { get; }
    public ICommand OpenURLCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand UnpublishCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand ResizeImageCommand { get; }
    public bool CanCopyImage => !string.IsNullOrWhiteSpace(_config.FilePath) && File.Exists(_config.FilePath) && FileHelpers.IsImageFile(_config.FilePath);
    internal string? FilePath => _config.FilePath;
    internal bool HasExistingFile => !string.IsNullOrWhiteSpace(_config.FilePath) && File.Exists(_config.FilePath);

    public ToastViewModel(
        ToastConfig config,
        IDesktopTaskManager? taskManager = null,
        HistoryViewModel? historyViewModel = null,
        HistoryItem? historyItem = null)
    {
        _config = config;
        _taskManager = taskManager;
        _historyViewModel = historyViewModel;
        _historyItem = historyItem;

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
        HeaderText = FormatHeaderText(config.OutputKind, config.URL, config.FilePath);
        HasHeaderText = !string.IsNullOrWhiteSpace(HeaderText);
        ErrorDetails = config.ErrorDetails;
        HasErrors = !string.IsNullOrWhiteSpace(config.ErrorDetails);

        // Initialize context menu commands (same set as History for shared ContextFlyout)
        EditImageCommand = new RelayCommand(AnnotateMedia);
        CopyImageToClipboardCommand = new RelayCommand(CopyImageToClipboard);
        OpenFileCommand = new RelayCommand(OpenFile);
        UploadItemCommand = new AsyncRelayCommand(UploadFileAsync);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        CopyFilePathCommand = new RelayCommand(CopyFilePath);
        ResizeImageCommand = new RelayCommand(() =>
        {
            if (CanCopyImage && FilePath != null)
            {
                XerahS.UI.Services.MediaToolsToolService.OpenImageBatchTool(XerahS.Media.ImageBatchOperation.Resize, [FilePath], owner: null);
            }
        });
        CopyUrlCommand = new RelayCommand(CopyUrl);
        CopyMarkdownImageCommand = new RelayCommand(CopyMarkdownImage);
        CopyErrorsCommand = new RelayCommand(CopyErrors);
        OpenURLCommand = new RelayCommand(OpenUrl);
        PublishCommand = _historyViewModel != null && _historyItem != null
            ? new AsyncRelayCommand(() => _historyViewModel.PublishItemCommand.ExecuteAsync(_historyItem))
            : new AsyncRelayCommand(DisabledCloudActionAsync, () => false);
        UnpublishCommand = _historyViewModel != null && _historyItem != null
            ? new AsyncRelayCommand(() => _historyViewModel.UnpublishItemCommand.ExecuteAsync(_historyItem))
            : new AsyncRelayCommand(DisabledCloudActionAsync, () => false);
        DeleteItemCommand = new RelayCommand(DeleteFile);

        ActionButtonSize = Math.Clamp(config.ActionButtonSize, 16, 128);
        ActionButtons = CreateActionButtons(config);

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

        _leaveGraceTimer = new DispatcherTimer { Interval = DefaultLeaveGrace };
        _leaveGraceTimer.Tick += OnLeaveGraceTick;

        _deferredCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(config.FadeDuration, 0.05f))
        };
        _deferredCloseTimer.Tick += OnDeferredCloseTick;

        // Start duration timer if auto-hide is enabled. A zero display duration means the
        // toast should begin fading immediately instead of staying visible forever.
        switch (GetAutoHideStartMode(config))
        {
            case ToastAutoHideStartMode.WaitForDuration:
                _durationTimer.Start();
                break;
            case ToastAutoHideStartMode.StartFade:
                _isDurationEnd = true;
                CheckFade();
                break;
        }
    }

    private List<ToastActionButtonViewModel> CreateActionButtons(ToastConfig config)
    {
        var buttons = new List<ToastActionButtonViewModel>();

        foreach (var action in (config.ActionButtons ?? []).Distinct())
        {
            if (CanExecuteAction(action, config))
            {
                var command = new RelayCommand(() => ExecuteAction(action));
                buttons.Add(new ToastActionButtonViewModel(action, GetActionLabel(action), GetActionIcon(action), command));
            }
        }

        return buttons;
    }

    internal static bool CanExecuteAction(ToastClickAction action, ToastConfig config)
    {
        bool hasFile = !string.IsNullOrWhiteSpace(config.FilePath);
        bool hasImageFile = hasFile && FileHelpers.IsImageFile(config.FilePath!);
        bool hasMediaFile = hasImageFile || (hasFile && FileHelpers.IsVideoFile(config.FilePath!));
        bool hasUrl = !string.IsNullOrWhiteSpace(config.URL);

        return action switch
        {
            ToastClickAction.CopyImageToClipboard or ToastClickAction.PinToScreen => hasImageFile,
            ToastClickAction.AnnotateMedia => hasMediaFile,
            ToastClickAction.CopyFile or ToastClickAction.CopyFilePath or ToastClickAction.OpenFile or
                ToastClickAction.OpenFolder or ToastClickAction.Upload or ToastClickAction.DeleteFile => hasFile,
            ToastClickAction.CopyUrl or ToastClickAction.OpenUrl => hasUrl,
            ToastClickAction.CloseNotification => true,
            _ => false
        };
    }

    internal static string GetActionLabel(ToastClickAction action) => action switch
    {
        ToastClickAction.AnnotateMedia => "Annotate",
        ToastClickAction.CopyImageToClipboard => "Copy image",
        ToastClickAction.CopyFile => "Copy file",
        ToastClickAction.CopyFilePath => "Copy file path",
        ToastClickAction.CopyUrl => "Copy URL",
        ToastClickAction.OpenFile => "Open file",
        ToastClickAction.OpenFolder => "Open folder",
        ToastClickAction.OpenUrl => "Open URL",
        ToastClickAction.Upload => "Upload",
        ToastClickAction.PinToScreen => "Pin to screen",
        ToastClickAction.DeleteFile => "Delete file",
        _ => "Close"
    };

    internal static string GetActionIcon(ToastClickAction action) => action switch
    {
        ToastClickAction.AnnotateMedia => LucideIcons.pen_line,
        ToastClickAction.CopyImageToClipboard => LucideIcons.copy,
        ToastClickAction.CopyFile => LucideIcons.files,
        ToastClickAction.CopyFilePath => LucideIcons.clipboard,
        ToastClickAction.CopyUrl => LucideIcons.link,
        ToastClickAction.OpenFile => LucideIcons.external_link,
        ToastClickAction.OpenFolder => LucideIcons.folder_open,
        ToastClickAction.OpenUrl => LucideIcons.external_link,
        ToastClickAction.Upload => LucideIcons.upload,
        ToastClickAction.PinToScreen => LucideIcons.pin,
        ToastClickAction.DeleteFile => LucideIcons.trash_2,
        _ => LucideIcons.x
    };

    private static Task DisabledCloudActionAsync() => Task.CompletedTask;

    internal bool CanPublishHistoryItem =>
        _historyItem != null && HistoryPublishMetadata.CanPublish(_historyItem, _historyViewModel?.CurrentCloudOwnerSubject);

    internal bool CanUnpublishHistoryItem =>
        _historyItem != null && HistoryPublishMetadata.CanUnpublish(_historyItem, _historyViewModel?.CurrentCloudOwnerSubject);

    internal static ToastAutoHideStartMode GetAutoHideStartMode(ToastConfig config)
    {
        if (!config.AutoHide)
        {
            return ToastAutoHideStartMode.None;
        }

        return config.Duration > 0
            ? ToastAutoHideStartMode.WaitForDuration
            : ToastAutoHideStartMode.StartFade;
    }

    internal enum ToastAutoHideStartMode
    {
        None,
        WaitForDuration,
        StartFade
    }

    public void OnMenuOpened()
    {
        _isMenuOpen = true;
        _fadeTimer.Stop();
        _deferredCloseTimer.Stop();

        // Reset opacity
        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);
    }

    public void OnMenuClosed()
    {
        _isMenuOpen = false;

        // When the user dismisses the menu, resume fade immediately regardless of duration state.
        // CheckFade() gates on _isDurationEnd which is only set when the duration timer fires.
        // If the user opens the context menu during the visible-duration window (before _isDurationEnd
        // is set), CheckFade() does nothing on close and the toast appears stuck.
        if (_config.AutoHide && !_isMouseInside)
        {
            StartFade();
        }
    }

    public void OnMouseEnter()
    {
        bool wasPending = _leaveGraceTimer.IsEnabled;
        _leaveGraceTimer.Stop();
        _deferredCloseTimer.Stop();

        if (!_isMouseInside)
        {
            DebugHelper.WriteLine("Toast hover: pointer entered");
        }
        else if (wasPending)
        {
            DebugHelper.WriteLine("Toast hover: pointer back inside, ignoring the pending leave");
        }

        _isMouseInside = true;
        _fadeTimer.Stop();

        // Reset opacity
        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);

        SetHovered(true);
    }

    public void OnMouseLeave()
    {
        if (UseLeaveGrace && _isMouseInside)
        {
            DebugHelper.WriteLine($"Toast hover: pointer left, confirming in {(int)_leaveGraceTimer.Interval.TotalMilliseconds} ms");
            _leaveGraceTimer.Stop();
            _leaveGraceTimer.Start();
            return;
        }

        CommitMouseLeave();
    }

    /// <summary>
    /// Pointer motion over the toast proves the pointer is inside, even if the platform lost
    /// the enter event or reported a leave that was not real.
    /// </summary>
    public void OnPointerMovedInside()
    {
        if (!_isMouseInside || _leaveGraceTimer.IsEnabled)
        {
            OnMouseEnter();
        }
    }

    private void OnLeaveGraceTick(object? sender, EventArgs e)
    {
        _leaveGraceTimer.Stop();
        CommitMouseLeave();
    }

    private void CommitMouseLeave()
    {
        _leaveGraceTimer.Stop();
        if (_isMouseInside)
        {
            DebugHelper.WriteLine("Toast hover: pointer left");
        }

        _isMouseInside = false;
        SetHovered(false);
        CheckFade();
    }

    private void SetHovered(bool hovered)
    {
        if (_isHovered == hovered)
        {
            return;
        }

        _isHovered = hovered;
        HoverChanged?.Invoke(this, hovered);
    }

    public void OnFileDragStarted()
    {
        _isFileDragActive = true;
        _fadeTimer.Stop();
        _deferredCloseTimer.Stop();
        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);
    }

    public void OnFileDragEnded(bool pointerInside)
    {
        _isFileDragActive = false;
        _leaveGraceTimer.Stop();
        _isMouseInside = pointerInside;
        SetHovered(pointerInside);
        CheckFade();
    }

    internal bool IsFadeTimerRunning => _fadeTimer.IsEnabled;

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
        CheckFade();
    }

    private void CheckFade()
    {
        if (_isDurationEnd && _config.AutoHide && !_isMouseInside && !_isMenuOpen && !_isFileDragActive)
        {
            StartFade();
        }
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var nextOpacity = GetNextFadeOpacity(_opacity, _opacityDecrement);

        if (nextOpacity > 0)
        {
            _opacity = nextOpacity;
            OpacityChanged?.Invoke(this, _opacity);
        }
        else
        {
            _opacity = 0;
            OpacityChanged?.Invoke(this, _opacity);
            _fadeTimer.Stop();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    internal static double GetNextFadeOpacity(double opacity, double opacityDecrement)
    {
        return opacity > opacityDecrement ? opacity - opacityDecrement : 0;
    }

    private void StartFade()
    {
        if (_isFileDragActive)
        {
            return;
        }

        if (_config.FadeDuration <= 0)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        // On Linux/Wayland the compositor's own fade animation (e.g. Hyprland's
        // default-opacity tag) can override Avalonia's per-window Opacity, so the
        // visual fade never happens. Keep the toast fully visible for the fade time
        // and then close; hovering again in that time cancels the close, the same way
        // hovering during the fade does on Windows.
        if (UseDeferredClose)
        {
            _deferredCloseTimer.Stop();
            _deferredCloseTimer.Start();
            return;
        }

        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);
        _fadeTimer.Start();
    }

    private void OnDeferredCloseTick(object? sender, EventArgs e)
    {
        _deferredCloseTimer.Stop();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteAction(ToastClickAction action)
    {
        _durationTimer.Stop();
        _fadeTimer.Stop();
        _leaveGraceTimer.Stop();
        _deferredCloseTimer.Stop();

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

            case ToastClickAction.AnnotateMedia:
                AnnotateMedia();
                break;

            case ToastClickAction.Upload:
                _ = UploadFileAsync();
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

    internal static string BuildMarkdownImage(string url, string? altText = null)
    {
        string escapedAltText = string.IsNullOrWhiteSpace(altText)
            ? "Image"
            : altText.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]");

        string markdownUrl = url.IndexOfAny([' ', '(', ')']) >= 0
            ? $"<{url}>"
            : url;

        return $"![{escapedAltText}]({markdownUrl})";
    }

    private void CopyMarkdownImage()
    {
        if (string.IsNullOrEmpty(_config.URL)) return;

        var markdownImage = BuildMarkdownImage(_config.URL, _config.Title);
        try
        {
            PlatformServices.Clipboard.SetText(markdownImage);
            DebugHelper.WriteLine("Copied markdown image to clipboard from toast.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to copy markdown image from toast");
        }
    }

    private void CopyErrors()
    {
        if (string.IsNullOrWhiteSpace(ErrorDetails))
        {
            return;
        }

        try
        {
            PlatformServices.Clipboard.SetText(ErrorDetails);
            DebugHelper.WriteLine("Copied task errors to clipboard from toast.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to copy errors from toast");
        }
    }

    private async void AnnotateMedia()
    {
        if (string.IsNullOrEmpty(_config.FilePath)) return;

        try
        {
            if (FileHelpers.IsImageFile(_config.FilePath))
            {
                using var bitmap = SKBitmap.Decode(_config.FilePath);
                if (bitmap != null)
                {
                    await PlatformServices.UI.ShowEditorAsync(bitmap, sourceFilePath: _config.FilePath);
                }
            }
            else
            {
                string ffmpegPath = XerahS.Common.PathsManager.GetFFmpegPath();
                await PlatformServices.UI.ShowVideoEditorAsync(
                    _config.FilePath,
                    string.IsNullOrEmpty(ffmpegPath) ? null : ffmpegPath);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to annotate media from toast");
        }
    }

    private async Task UploadFileAsync()
    {
        if (string.IsNullOrWhiteSpace(_config.FilePath))
        {
            return;
        }

        if (!File.Exists(_config.FilePath))
        {
            DebugHelper.WriteLine($"Toast upload skipped, file not found: {_config.FilePath}");
            return;
        }

        if (_taskManager == null)
        {
            DebugHelper.WriteLine("Toast upload skipped, desktop task manager is not available.");
            return;
        }

        try
        {
            var settings = GetUploadTaskSettings();
            settings.Job = WorkflowType.FileUpload;
            await _taskManager.StartFileTask(settings, _config.FilePath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to upload file from toast");
        }
    }

    private static TaskSettings GetUploadTaskSettings()
    {
        var uploadWorkflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
        if (uploadWorkflow?.TaskSettings != null)
        {
            var settings = WatchFolderManager.CloneTaskSettings(uploadWorkflow.TaskSettings);
            settings.WorkflowId = uploadWorkflow.Id;
            return settings;
        }

        return WatchFolderManager.CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());
    }

    private void PinToScreen()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && FileHelpers.IsImageFile(_config.FilePath))
        {
            try
            {
                using var bitmap = SKBitmap.Decode(_config.FilePath);
                if (bitmap != null)
                {
                    var options = SettingsManager.DefaultTaskSettings?.ToolsSettings?.PinToScreenOptions
                        ?? new PinToScreenOptions();
                    Services.PinToScreenManager.PinImage(bitmap, null, options);
                }
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
            _leaveGraceTimer.Stop();
            _deferredCloseTimer.Stop();
            _historyViewModel?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// A single button in the toast's hover toolbar.
/// </summary>
public sealed class ToastActionButtonViewModel(ToastClickAction action, string label, string icon, ICommand command)
{
    public ToastClickAction Action { get; } = action;
    public string Label { get; } = label;
    public string Icon { get; } = icon;
    public ICommand Command { get; } = command;
}
