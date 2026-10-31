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
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class OcrViewModel : ViewModelBase
{
    private SKBitmap? _sourceImage;
    private bool _hasRecognizedOnce;

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
            StatusText = ocrService?.UnavailableReason ?? "OCR is not supported on this platform.";
            return;
        }

        OcrLanguage[] languages;
        try
        {
            languages = ocrService.GetAvailableLanguages();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OCR language loading");
            StatusText = $"Could not load OCR languages: {ex.Message}";
            return;
        }

        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in languages)
        {
            string languageTag = lang.LanguageTag?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(languageTag) || !seenTags.Add(languageTag))
            {
                continue;
            }

            string displayName = NormalizeDisplayName(lang.DisplayName, languageTag);
            AvailableLanguages.Add(new OcrLanguage(displayName, languageTag));
        }

        // Prefer the language persisted by the onboarding wizard (OCROptions.Language)
        // so the user's choice carries over into the OCR tool; fall back to English,
        // then to the first available language.
        string? persistedLanguage = ResolvePersistedLanguageTag();
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l =>
            string.Equals(l.LanguageTag, persistedLanguage, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLanguages.FirstOrDefault(l =>
                l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            ?? AvailableLanguages.FirstOrDefault();
    }

    private static string? ResolvePersistedLanguageTag()
    {
        try
        {
            return SettingsManager.DefaultTaskSettings?.CaptureSettings?.OCROptions?.Language?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeDisplayName(string? displayName, string languageTag)
    {
        string normalized = displayName?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(normalized) ? languageTag : normalized;
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
            StatusText = ocrService?.UnavailableReason ?? "OCR service not available.";
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
        HasResult = false;

        try
        {
            var options = new OcrOptions
            {
                Language = NormalizeOcrLanguage(SelectedLanguage.LanguageTag),
                ScaleFactor = NormalizeOcrScaleFactor(ScaleFactor),
                SingleLine = SingleLine
            };

            _hasRecognizedOnce = true;
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
                StatusText = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "OCR failed."
                    : result.ErrorMessage.Trim();
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
        // Re-run OCR when language changes after at least one OCR attempt,
        // even if the previous attempt found no text.
        if (value != null && _hasRecognizedOnce && _sourceImage != null && !IsProcessing)
        {
            _ = RunOcrAsync();
        }
    }

    private static string NormalizeOcrLanguage(string? language)
    {
        string? trimmedLanguage = language?.Trim();
        return string.IsNullOrEmpty(trimmedLanguage) ? "en" : trimmedLanguage;
    }

    private static float NormalizeOcrScaleFactor(double scaleFactor)
    {
        return double.IsFinite(scaleFactor) ? Math.Max((float)scaleFactor, 1f) : 1f;
    }
}
