#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Core;
using ShareX.Editor.Helpers;
using System;
using System.IO;

namespace ShareX.Ava.UI.ViewModels;

public partial class AfterCaptureViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap _previewImage;

    [ObservableProperty]
    private AfterCaptureTasks _afterCaptureTasks;

    [ObservableProperty]
    private AfterUploadTasks _afterUploadTasks;

    public bool Cancelled { get; private set; } = true;

    public event Action? RequestClose;

    public AfterCaptureViewModel(SkiaSharp.SKBitmap image, AfterCaptureTasks afterCapture, AfterUploadTasks afterUpload)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));

        PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(image);
        AfterCaptureTasks = afterCapture & ~AfterCaptureTasks.ShowAfterCaptureWindow;
        AfterUploadTasks = afterUpload;
    }

    public bool SaveImageToFile
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.SaveImageToFile);
        set
        {
            SetAfterCaptureFlag(AfterCaptureTasks.SaveImageToFile, value);
            OnPropertyChanged();
        }
    }

    public bool CopyImageToClipboard
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
        set
        {
            SetAfterCaptureFlag(AfterCaptureTasks.CopyImageToClipboard, value);
            OnPropertyChanged();
        }
    }

    public bool AnnotateImage
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.AnnotateImage);
        set
        {
            SetAfterCaptureFlag(AfterCaptureTasks.AnnotateImage, value);
            OnPropertyChanged();
        }
    }

    public bool UploadImageToHost
    {
        get => AfterCaptureTasks.HasFlag(AfterCaptureTasks.UploadImageToHost);
        set
        {
            SetAfterCaptureFlag(AfterCaptureTasks.UploadImageToHost, value);
            OnPropertyChanged();
        }
    }

    public bool CopyURLToClipboard
    {
        get => AfterUploadTasks.HasFlag(AfterUploadTasks.CopyURLToClipboard);
        set
        {
            SetAfterUploadFlag(AfterUploadTasks.CopyURLToClipboard, value);
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

    partial void OnAfterCaptureTasksChanged(AfterCaptureTasks value)
    {
        OnPropertyChanged(nameof(SaveImageToFile));
        OnPropertyChanged(nameof(CopyImageToClipboard));
        OnPropertyChanged(nameof(AnnotateImage));
        OnPropertyChanged(nameof(UploadImageToHost));
    }

    partial void OnAfterUploadTasksChanged(AfterUploadTasks value)
    {
        OnPropertyChanged(nameof(CopyURLToClipboard));
        OnPropertyChanged(nameof(UseURLShortener));
        OnPropertyChanged(nameof(ShareURL));
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
