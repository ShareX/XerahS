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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class OcrViewModel : ViewModelBase
{
    private SKBitmap? _sourceImage;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private OcrLanguage? _selectedLanguage;

    [ObservableProperty]
    private double _scaleFactor = 2.0;

    [ObservableProperty]
    private bool _singleLine;

    public ObservableCollection<OcrLanguage> AvailableLanguages { get; } = new();

    public double[] ScaleFactorOptions { get; } = [1.0, 1.5, 2.0, 3.0, 4.0];

    /// <summary>
    /// Callback to capture a new screen region. Set by the tool service.
    /// </summary>
    public Func<Task<SKBitmap?>>? SelectRegionRequested { get; set; }

    public OcrViewModel(SKBitmap sourceImage)
    {
        _sourceImage = sourceImage;
        LoadAvailableLanguages();
    }

    private void LoadAvailableLanguages()
    {
        var ocrService = PlatformServices.Ocr;
        if (ocrService == null || !ocrService.IsSupported)
        {
            return;
        }

        var languages = ocrService.GetAvailableLanguages();
        foreach (var lang in languages)
        {
            AvailableLanguages.Add(lang);
        }

        // Default to English if available, otherwise first language
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l =>
            l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            ?? AvailableLanguages.FirstOrDefault();
    }

    [RelayCommand]
    public async Task RunOcrAsync()
    {
        if (_sourceImage == null)
        {
            StatusText = "No image to process.";
            return;
        }

        var ocrService = PlatformServices.Ocr;
        if (ocrService == null || !ocrService.IsSupported)
        {
            StatusText = "OCR service not available.";
            return;
        }

        if (SelectedLanguage == null)
        {
            StatusText = "No language selected.";
            return;
        }

        IsProcessing = true;
        StatusText = "Processing...";
        ResultText = string.Empty;

        try
        {
            var options = new OcrOptions
            {
                Language = SelectedLanguage.LanguageTag,
                ScaleFactor = (float)ScaleFactor,
                SingleLine = SingleLine
            };

            var result = await ocrService.RecognizeAsync(_sourceImage, options);

            if (result.Success)
            {
                ResultText = result.Text;
                HasResult = !string.IsNullOrEmpty(result.Text);
                StatusText = HasResult
                    ? $"Done. {result.Text.Split('\n').Length} line(s) recognized."
                    : "No text detected.";
            }
            else
            {
                StatusText = result.ErrorMessage ?? "OCR failed.";
                HasResult = false;
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OCR recognition");
            StatusText = $"Error: {ex.Message}";
            HasResult = false;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (string.IsNullOrEmpty(ResultText))
        {
            return;
        }

        try
        {
            await PlatformServices.Clipboard.SetTextAsync(ResultText);
            StatusText = "Copied to clipboard.";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OCR clipboard copy");
            StatusText = "Failed to copy to clipboard.";
        }
    }

    [RelayCommand]
    private async Task SelectRegionAsync()
    {
        if (SelectRegionRequested == null)
        {
            return;
        }

        var newImage = await SelectRegionRequested();

        if (newImage != null)
        {
            _sourceImage?.Dispose();
            _sourceImage = newImage;
            await RunOcrAsync();
        }
    }

    [RelayCommand]
    private void OpenServiceLink(string url)
    {
        if (string.IsNullOrEmpty(ResultText) || string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            string encodedText = Uri.EscapeDataString(ResultText);
            string fullUrl = string.Format(url, encodedText);
            PlatformServices.System.OpenUrl(fullUrl);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OCR open service link");
        }
    }

    partial void OnSelectedLanguageChanged(OcrLanguage? value)
    {
        // Re-run OCR when language changes and we already have a result
        if (value != null && HasResult && _sourceImage != null)
        {
            _ = RunOcrAsync();
        }
    }
}
