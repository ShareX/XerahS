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
using System.Collections.Generic;
using System.Linq;
using AvaloniaColor = Avalonia.Media.Color;
using DrawingColor = System.Drawing.Color;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.Platform.Abstractions;
using XerahS.UI.Helpers;

namespace XerahS.UI.ViewModels;

public partial class ColorPickerViewModel : ViewModelBase
{
    private const int MaxRecentColors = 20;
    private bool _isUpdating;
    private readonly IClipboardService? _clipboardService;

    public ColorPickerViewModel()
        : this(ColorConversion.ToAvaloniaColor(DrawingColor.White), null)
    {
    }

    public ColorPickerViewModel(AvaloniaColor initialColor, IClipboardService? clipboardService = null)
    {
        _clipboardService = clipboardService;
        PreviousColor = initialColor;
        SelectedColor = initialColor;
        StandardColors = new ObservableCollection<AvaloniaColor>(ColorHelpers.StandardColors.Select(ColorConversion.ToAvaloniaColor));
        RecentColors = new ObservableCollection<AvaloniaColor>();
        LoadRecentColors();
    }

    public Func<Task<PointInfo?>>? ScreenPickerRequested { get; set; }

    [ObservableProperty]
    private AvaloniaColor _selectedColor;

    [ObservableProperty]
    private AvaloniaColor _previousColor;

    [ObservableProperty]
    private int _red;

    [ObservableProperty]
    private int _green;

    [ObservableProperty]
    private int _blue;

    [ObservableProperty]
    private int _hue;

    [ObservableProperty]
    private int _saturation;

    [ObservableProperty]
    private int _brightness;

    [ObservableProperty]
    private int _cyan;

    [ObservableProperty]
    private int _magenta;

    [ObservableProperty]
    private int _yellow;

    [ObservableProperty]
    private int _key;

    [ObservableProperty]
    private string _hex = "#FFFFFF";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickFromScreenCommand))]
    private bool _isScreenPicking;

    public ObservableCollection<AvaloniaColor> RecentColors { get; }

    public ObservableCollection<AvaloniaColor> StandardColors { get; }

    public bool HasRecentColors => RecentColors.Count > 0;
    public bool HasNoRecentColors => !HasRecentColors;

    partial void OnSelectedColorChanged(AvaloniaColor value)
    {
        if (_isUpdating)
        {
            return;
        }

        UpdateFieldsFromColor(ColorConversion.ToDrawingColor(value));
    }

    partial void OnIsScreenPickingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPickFromScreen));
    }

    partial void OnRedChanged(int value) => UpdateFromRgb();
    partial void OnGreenChanged(int value) => UpdateFromRgb();
    partial void OnBlueChanged(int value) => UpdateFromRgb();

    partial void OnHueChanged(int value) => UpdateFromHsb();
    partial void OnSaturationChanged(int value) => UpdateFromHsb();
    partial void OnBrightnessChanged(int value) => UpdateFromHsb();

    partial void OnCyanChanged(int value) => UpdateFromCmyk();
    partial void OnMagentaChanged(int value) => UpdateFromCmyk();
    partial void OnYellowChanged(int value) => UpdateFromCmyk();
    partial void OnKeyChanged(int value) => UpdateFromCmyk();

    partial void OnHexChanged(string value) => UpdateFromHex();

    private void UpdateFieldsFromColor(DrawingColor color)
    {
        _isUpdating = true;
        try
        {
            Red = color.R;
            Green = color.G;
            Blue = color.B;

            var hsb = ColorHelpers.ColorToHSB(color);
            Hue = (int)Math.Round(hsb.Hue360);
            Saturation = (int)Math.Round(hsb.Saturation100);
            Brightness = (int)Math.Round(hsb.Brightness100);

            var cmyk = ColorHelpers.ColorToCMYK(color);
            Cyan = (int)Math.Round(cmyk.Cyan100);
            Magenta = (int)Math.Round(cmyk.Magenta100);
            Yellow = (int)Math.Round(cmyk.Yellow100);
            Key = (int)Math.Round(cmyk.Key100);

            Hex = $"#{ColorHelpers.ColorToHex(color)}";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateFromRgb()
    {
        if (_isUpdating)
        {
            return;
        }

        var color = DrawingColor.FromArgb(
            255,
            ColorHelpers.ValidColor(Red),
            ColorHelpers.ValidColor(Green),
            ColorHelpers.ValidColor(Blue));

        SetSelectedColor(color);
    }

    private void UpdateFromHsb()
    {
        if (_isUpdating)
        {
            return;
        }

        var hsb = new HSB(
            Math.Clamp(Hue, 0, 360),
            Math.Clamp(Saturation, 0, 100),
            Math.Clamp(Brightness, 0, 100),
            255);

        SetSelectedColor(hsb.ToColor());
    }

    private void UpdateFromCmyk()
    {
        if (_isUpdating)
        {
            return;
        }

        var cmyk = new CMYK(
            Math.Clamp(Cyan, 0, 100),
            Math.Clamp(Magenta, 0, 100),
            Math.Clamp(Yellow, 0, 100),
            Math.Clamp(Key, 0, 100),
            255);

        SetSelectedColor(cmyk.ToColor());
    }

    private void UpdateFromHex()
    {
        if (_isUpdating || string.IsNullOrWhiteSpace(Hex))
        {
            return;
        }

        try
        {
            var cleaned = Hex.Trim().TrimStart('#');
            var color = ColorHelpers.HexToColor(cleaned);
            SetSelectedColor(color);
        }
        catch
        {
            // Ignore invalid hex until user finishes editing.
        }
    }

    private void SetSelectedColor(DrawingColor color)
    {
        _isUpdating = true;
        try
        {
            SelectedColor = ColorConversion.ToAvaloniaColor(color);
            UpdateFieldsFromColor(color);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private string FormatRgb()
    {
        var color = ColorConversion.ToDrawingColor(SelectedColor);
        return $"rgb({color.R}, {color.G}, {color.B})";
    }

    private string FormatHsb()
    {
        var color = ColorConversion.ToDrawingColor(SelectedColor);
        var hsb = ColorHelpers.ColorToHSB(color);
        return $"hsb({Math.Round(hsb.Hue360)}, {Math.Round(hsb.Saturation100)}%, {Math.Round(hsb.Brightness100)}%)";
    }

    private string FormatCmyk()
    {
        var color = ColorConversion.ToDrawingColor(SelectedColor);
        var cmyk = ColorHelpers.ColorToCMYK(color);
        return $"cmyk({Math.Round(cmyk.Cyan100)}%, {Math.Round(cmyk.Magenta100)}%, {Math.Round(cmyk.Yellow100)}%, {Math.Round(cmyk.Key100)}%)";
    }

    private async Task CopyToClipboardAsync(string text)
    {
        // Use injected service if available, otherwise fall back to PlatformServices
        var clipboard = _clipboardService ?? (PlatformServices.IsInitialized ? PlatformServices.Clipboard : null);
        if (clipboard == null)
        {
            StatusMessage = "Clipboard service is not available.";
            return;
        }

        await clipboard.SetTextAsync(text);
        StatusMessage = "Color copied to clipboard.";
    }

    private void LoadRecentColors()
    {
        RecentColors.Clear();

        var settings = SettingsManager.Settings;
        if (settings?.RecentColors == null)
        {
            return;
        }

        foreach (var color in settings.RecentColors)
        {
            RecentColors.Add(ColorConversion.ToAvaloniaColor(color));
        }

        OnPropertyChanged(nameof(HasRecentColors));
        OnPropertyChanged(nameof(HasNoRecentColors));
    }

    private void TrackRecentColor()
    {
        var settings = SettingsManager.Settings;
        if (settings == null)
        {
            return;
        }

        var drawing = ColorConversion.ToDrawingColor(SelectedColor);
        settings.RecentColors ??= new List<DrawingColor>();

        settings.RecentColors.RemoveAll(c => c.ToArgb() == drawing.ToArgb());
        settings.RecentColors.Insert(0, drawing);

        if (settings.RecentColors.Count > MaxRecentColors)
        {
            settings.RecentColors.RemoveRange(MaxRecentColors, settings.RecentColors.Count - MaxRecentColors);
        }

        LoadRecentColors();
        SettingsManager.SaveApplicationConfigAsync();
    }

    [RelayCommand]
    private void SelectSwatch(AvaloniaColor color)
    {
        SetSelectedColor(ColorConversion.ToDrawingColor(color));
    }

    [RelayCommand]
    private async Task CopyHexAsync()
    {
        TrackRecentColor();
        await CopyToClipboardAsync(Hex);
    }

    [RelayCommand]
    private async Task CopyRgbAsync()
    {
        TrackRecentColor();
        await CopyToClipboardAsync(FormatRgb());
    }

    [RelayCommand]
    private async Task CopyHsbAsync()
    {
        TrackRecentColor();
        await CopyToClipboardAsync(FormatHsb());
    }

    [RelayCommand]
    private async Task CopyCmykAsync()
    {
        TrackRecentColor();
        await CopyToClipboardAsync(FormatCmyk());
    }

    [RelayCommand(CanExecute = nameof(CanPickFromScreen))]
    private async Task PickFromScreenAsync()
    {
        if (ScreenPickerRequested == null)
        {
            return;
        }

        IsScreenPicking = true;
        try
        {
            var result = await ScreenPickerRequested();
            if (result == null)
            {
                return;
            }

            SetSelectedColor(result.Color);
            StatusMessage = ColorPickerService.GetInfoText(SettingsManager.DefaultTaskSettings?.ToolsSettings, result.Color, result.Position);
            TrackRecentColor();
        }
        finally
        {
            IsScreenPicking = false;
        }
    }

    private bool CanPickFromScreen => !IsScreenPicking;
}
