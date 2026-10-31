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
using Avalonia.Media.Imaging;
using XerahS.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Presentation.Rendering;
using SkiaSharp;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;
using ImageHelpers = XerahS.Common.ImageHelpers;

namespace XerahS.UI.ViewModels;

public partial class QrCodeGeneratorViewModel : ViewModelBase, IDisposable
{
    private const int DefaultPreviewSize = 320;

    private SKBitmap? _generatedBitmap;
    private Bitmap? _previewImage;
    private readonly IViewDialogService _dialogService;

    public QrCodeGeneratorViewModel(IViewDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    /// <summary>
    /// Invoked by the view when the user wants to dismiss the modal.
    /// True = save / apply, false = cancel.
    /// </summary>
    public Action<bool>? CloseRequested { get; set; }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(true);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyImageCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Enter text to generate a QR code.";

    public Bitmap? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (ReferenceEquals(_previewImage, value))
            {
                return;
            }

            _previewImage?.Dispose();
            _previewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreviewImage));
            SaveImageCommand.NotifyCanExecuteChanged();
            CopyImageCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasPreviewImage => PreviewImage != null;

    public string CharacterCountText => $"{InputText.Length} / {QrCodeService.MaxInputLength}";

    private bool CanGenerate() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    private bool CanSaveOrCopy() => !IsBusy && HasPreviewImage;

    partial void OnInputTextChanged(string value)
    {
        OnPropertyChanged(nameof(CharacterCountText));
        GenerateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task PasteFromClipboardAsync()
    {
        if (!PlatformServices.IsInitialized)
        {
            StatusMessage = "Clipboard is not available.";
            return;
        }

        try
        {
            var text = await PlatformServices.Clipboard.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusMessage = "Clipboard is empty or does not contain text.";
                return;
            }

            InputText = text.Trim();
            StatusMessage = "Text pasted from clipboard.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clipboard read failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private void Generate()
    {
        IsBusy = true;
        StatusMessage = "Generating QR code...";

        try
        {
            if (!QrCodeService.TryGenerate(InputText.Trim(), DefaultPreviewSize, out var bitmap, out var error))
            {
                StatusMessage = error ?? "Failed to generate QR code.";
                ResetPreview();
                return;
            }

            SetGeneratedBitmap(bitmap);
            StatusMessage = "QR code generated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Generation failed: {ex.Message}";
            ResetPreview();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveOrCopy))]
    private async Task SaveImageAsync()
    {
        if (!HasPreviewImage || _generatedBitmap == null)
        {
            return;
        }

        var filePath = await _dialogService.ShowSaveFilePickerAsync("Save QR Code", "qr-code.png", "png", new[] { "*.png", "*.*" });

        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusMessage = "Save cancelled.";
            return;
        }

        try
        {
            ImageHelpers.SaveBitmap(_generatedBitmap, filePath);
            StatusMessage = $"Saved to {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveOrCopy))]
    private async Task CopyImageAsync()
    {
        if (!PlatformServices.IsInitialized || _generatedBitmap == null)
        {
            StatusMessage = "Clipboard is not available.";
            return;
        }

        try
        {
            PlatformServices.Clipboard.SetImage(_generatedBitmap);
            StatusMessage = "QR code image copied to clipboard.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clipboard copy failed: {ex.Message}";
        }
    }

    private void SetGeneratedBitmap(SKBitmap? bitmap)
    {
        _generatedBitmap?.Dispose();
        _generatedBitmap = bitmap;
        PreviewImage = bitmap != null ? BitmapConversionHelpers.ToAvaloniBitmap(bitmap) : null;
    }

    private void ResetPreview()
    {
        SetGeneratedBitmap(null);
    }

    public void Dispose()
    {
        _generatedBitmap?.Dispose();
        _generatedBitmap = null;
        _previewImage?.Dispose();
        _previewImage = null;
    }
}

