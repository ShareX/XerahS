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

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using ShareX.ImageEditor.Presentation.Rendering;

namespace XerahS.UI.ViewModels;

public enum AfterCaptureGoal
{
    CopyImage,
    CopyFilePath,
    CopyUrl
}

public partial class AfterCaptureViewModel : ViewModelBase
{
    private const AfterCaptureTasks ExclusiveCaptureFlags =
        AfterCaptureTasks.CopyImageToClipboard
        | AfterCaptureTasks.CopyFilePathToClipboard
        | AfterCaptureTasks.UploadImageToHost;

    [ObservableProperty]
    private Bitmap _previewImage;

    [ObservableProperty]
    private AfterCaptureTasks _afterCaptureTasks;

    [ObservableProperty]
    private AfterUploadTasks _afterUploadTasks;

    [ObservableProperty]
    private AfterCaptureGoal _selectedGoal;

    [ObservableProperty] private bool _privacyMode;

    public bool Cancelled { get; private set; } = true;

    public AfterCaptureQuickAction QuickAction { get; private set; }

    public event Action? RequestClose;

    public AfterCaptureViewModel()
    {
        using var image = new SkiaSharp.SKBitmap(2, 2);
        PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(image);
        AfterCaptureTasks = AfterCaptureTasks.ShowAfterCaptureWindow | AfterCaptureTasks.UploadImageToHost;
        AfterUploadTasks = AfterUploadTasks.CopyURLToClipboard;
        _selectedGoal = AfterCaptureGoal.CopyUrl;
        ApplyGoalFlags();
    }

    public AfterCaptureViewModel(SkiaSharp.SKBitmap image, AfterCaptureTasks afterCapture, AfterUploadTasks afterUpload)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));

        PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(image);
        AfterCaptureTasks = afterCapture;
        AfterUploadTasks = afterUpload;
        _selectedGoal = InferGoal(afterCapture, afterUpload);
        ApplyGoalFlags();
    }

    public bool IsCopyImageGoal
    {
        get => SelectedGoal == AfterCaptureGoal.CopyImage;
        set
        {
            if (value)
            {
                SelectedGoal = AfterCaptureGoal.CopyImage;
            }
        }
    }

    public bool IsCopyFilePathGoal
    {
        get => SelectedGoal == AfterCaptureGoal.CopyFilePath;
        set
        {
            if (value)
            {
                SelectedGoal = AfterCaptureGoal.CopyFilePath;
            }
        }
    }

    public bool IsCopyUrlGoal
    {
        get => SelectedGoal == AfterCaptureGoal.CopyUrl;
        set
        {
            if (value)
            {
                SelectedGoal = AfterCaptureGoal.CopyUrl;
            }
        }
    }

    public bool ShowSaveImageOption => SelectedGoal != AfterCaptureGoal.CopyFilePath;

    public bool ShowUrlOptions => SelectedGoal == AfterCaptureGoal.CopyUrl;

    public bool SaveImageToFile
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.SaveImageToFile);
        set
        {
            if (SelectedGoal == AfterCaptureGoal.CopyFilePath && !value)
            {
                return;
            }

            SetAfterCaptureFlag(AfterCaptureTasks.SaveImageToFile, value);
            OnPropertyChanged();
        }
    }

    public bool CopyImageToClipboard
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
        set
        {
            if (SelectedGoal == AfterCaptureGoal.CopyImage && !value)
            {
                return;
            }

            SetAfterCaptureFlag(AfterCaptureTasks.CopyImageToClipboard, value);
            OnPropertyChanged();
        }
    }

    public bool CopyFilePathToClipboard
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.CopyFilePathToClipboard);
        set
        {
            if (SelectedGoal == AfterCaptureGoal.CopyFilePath && !value)
            {
                return;
            }

            SetAfterCaptureFlag(AfterCaptureTasks.CopyFilePathToClipboard, value);
            OnPropertyChanged();
        }
    }

    public bool AnnotateMedia
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.AnnotateMedia);
        set
        {
            SetAfterCaptureFlag(AfterCaptureTasks.AnnotateMedia, value);
            OnPropertyChanged();
        }
    }

    public bool UploadImageToHost
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.UploadImageToHost);
        set
        {
            if (SelectedGoal == AfterCaptureGoal.CopyUrl && !value)
            {
                return;
            }

            SetAfterCaptureFlag(AfterCaptureTasks.UploadImageToHost, value);
            OnPropertyChanged();
        }
    }

    public bool CopyOcrTextToClipboard
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.CopyOcrTextToClipboard);
        set
        {
            SetAfterCaptureFlag(AfterCaptureTasks.CopyOcrTextToClipboard, value);
            OnPropertyChanged();
        }
    }

    public bool CopyURLToClipboard
    {
        get => AfterUploadTasks.HasFlag(AfterUploadTasks.CopyURLToClipboard);
        set
        {
            if (SelectedGoal == AfterCaptureGoal.CopyUrl && !value)
            {
                return;
            }

            SetAfterUploadFlag(AfterUploadTasks.CopyURLToClipboard, value);
            OnPropertyChanged();
        }
    }

    public bool ShowAfterUploadWindow
    {
        get => AfterUploadTasks.HasFlag(AfterUploadTasks.ShowAfterUploadWindow);
        set
        {
            SetAfterUploadFlag(AfterUploadTasks.ShowAfterUploadWindow, value);
            OnPropertyChanged();
        }
    }

    public bool UseURLShortener
    {
        get => AfterUploadTasks.HasFlag(AfterUploadTasks.UseURLShortener);
        set
        {
            SetAfterUploadFlag(AfterUploadTasks.UseURLShortener, value);
            OnPropertyChanged();
        }
    }

    public bool ShareURL
    {
        get => AfterUploadTasks.HasFlag(AfterUploadTasks.ShareURL);
        set
        {
            SetAfterUploadFlag(AfterUploadTasks.ShareURL, value);
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void Continue()
    {
        Cancelled = false;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        Cancelled = true;
        RequestClose?.Invoke();
    }

    internal static AfterCaptureGoal InferGoal(AfterCaptureTasks capture, AfterUploadTasks upload)
    {
        if (capture.HasFlag(AfterCaptureTasks.UploadImageToHost) ||
            upload.HasFlag(AfterUploadTasks.CopyURLToClipboard))
        {
            return AfterCaptureGoal.CopyUrl;
        }

        if (capture.HasFlag(AfterCaptureTasks.CopyFilePathToClipboard))
        {
            return AfterCaptureGoal.CopyFilePath;
        }

        if (capture.HasFlag(AfterCaptureTasks.CopyImageToClipboard))
        {
            return AfterCaptureGoal.CopyImage;
        }

        return AfterCaptureGoal.CopyUrl;
    }

    partial void OnSelectedGoalChanged(AfterCaptureGoal value)
    {
        ApplyGoalFlags();
        OnPropertyChanged(nameof(IsCopyImageGoal));
        OnPropertyChanged(nameof(IsCopyFilePathGoal));
        OnPropertyChanged(nameof(IsCopyUrlGoal));
        OnPropertyChanged(nameof(ShowSaveImageOption));
        OnPropertyChanged(nameof(ShowUrlOptions));
    }

    partial void OnAfterCaptureTasksChanged(AfterCaptureTasks value)
    {
        OnPropertyChanged(nameof(SaveImageToFile));
        OnPropertyChanged(nameof(CopyImageToClipboard));
        OnPropertyChanged(nameof(CopyFilePathToClipboard));
        OnPropertyChanged(nameof(AnnotateMedia));
        OnPropertyChanged(nameof(UploadImageToHost));
        OnPropertyChanged(nameof(CopyOcrTextToClipboard));
    }

    partial void OnAfterUploadTasksChanged(AfterUploadTasks value)
    {
        OnPropertyChanged(nameof(CopyURLToClipboard));
        OnPropertyChanged(nameof(ShowAfterUploadWindow));
        OnPropertyChanged(nameof(UseURLShortener));
        OnPropertyChanged(nameof(ShareURL));
    }

    private void ApplyGoalFlags()
    {
        var capture = AfterCaptureTasks & ~ExclusiveCaptureFlags;
        var upload = AfterUploadTasks & ~AfterUploadTasks.CopyURLToClipboard;

        switch (SelectedGoal)
        {
            case AfterCaptureGoal.CopyImage:
                capture |= AfterCaptureTasks.CopyImageToClipboard;
                break;
            case AfterCaptureGoal.CopyFilePath:
                capture |= AfterCaptureTasks.SaveImageToFile | AfterCaptureTasks.CopyFilePathToClipboard;
                break;
            case AfterCaptureGoal.CopyUrl:
                capture |= AfterCaptureTasks.UploadImageToHost;
                upload |= AfterUploadTasks.CopyURLToClipboard;
                break;
        }

        AfterCaptureTasks = capture;
        AfterUploadTasks = upload;
    }

    private void SetAfterCaptureFlag(AfterCaptureTasks flag, bool enabled)
    {
        AfterCaptureTasks = enabled ? AfterCaptureTasks | flag : AfterCaptureTasks & ~flag;
    }

    private void SetAfterUploadFlag(AfterUploadTasks flag, bool enabled)
    {
        AfterUploadTasks = enabled ? AfterUploadTasks | flag : AfterUploadTasks & ~flag;
    }
}
