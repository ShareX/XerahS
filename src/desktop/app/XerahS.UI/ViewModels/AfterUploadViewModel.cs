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

*/

#endregion License Information (GPL v3)

using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Helpers;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Core;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.UI.ViewModels;

public sealed partial class AfterUploadViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer? _autoCloseTimer;
    private int _autoCloseRemainingSeconds;
    private bool _disposed;

    private AfterUploadFormatEntry? _selectedFormat;

    public event Action? RequestClose;

    public ObservableCollection<AfterUploadFormatEntry> Formats { get; } = new();

    public AfterUploadFormatEntry? SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetProperty(ref _selectedFormat, value))
            {
                EnsureSelectableFormat();
                SelectedFormatValue = _selectedFormat?.Value ?? string.Empty;
            }
        }
    }

    [ObservableProperty]
    private string _selectedFormatValue = string.Empty;

    [ObservableProperty]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private string _previewFallbackTitle = "No preview available";

    [ObservableProperty]
    private string _previewFallbackDescription = "This upload did not include a local preview.";

    public string PrimaryUrl { get; }
    public string? RawUrl { get; }
    public string? ShortenedUrl { get; }
    public string? ThumbnailUrl { get; }
    public string? DeletionUrl { get; }
    public string? FilePath { get; }
    public string FileName { get; }
    public string? FileSizeText { get; }
    public string? UploaderHost { get; }
    public string? DataType { get; }

    public bool HasPreviewImage => PreviewImage != null;
    public bool HasPreviewFallback => !HasPreviewImage;
    public bool HasLocalFile => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);
    public bool HasNoLocalFile => !HasLocalFile;
    public bool HasUrl => !string.IsNullOrWhiteSpace(PrimaryUrl);
    public bool HasUploaderHost => !string.IsNullOrWhiteSpace(UploaderHost);
    public bool HasFileSize => !string.IsNullOrWhiteSpace(FileSizeText);
    public bool CanCopyImage => HasLocalFile && !string.IsNullOrEmpty(FilePath) && FileHelpers.IsImageFile(FilePath);
    public bool CanOpenFile => HasLocalFile;
    public bool CanOpenFolder => HasLocalFile;
    public bool CanOpenUrl => HasUrl;
    public bool HasFormats => Formats.Any(entry => entry.IsSelectable);
    public bool IsAutoCloseEnabled => _autoCloseTimer != null;

    public string AutoCloseText => IsAutoCloseEnabled
        ? $"Auto closes in {_autoCloseRemainingSeconds}s"
        : string.Empty;

    [ObservableProperty]
    private string? _errorDetails;

    public bool HasErrors => !string.IsNullOrWhiteSpace(ErrorDetails);

    public ICommand CopyImageCommand { get; }
    public ICommand CopyFormatCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand CopyErrorsCommand { get; }
    public ICommand CloseCommand { get; }

    public AfterUploadViewModel(AfterUploadWindowInfo info)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));

        RawUrl = info.Url;
        ShortenedUrl = info.ShortenedUrl;
        ThumbnailUrl = info.ThumbnailUrl;
        DeletionUrl = info.DeletionUrl;
        PrimaryUrl = ShortenedUrl ?? RawUrl ?? string.Empty;
        FilePath = string.IsNullOrWhiteSpace(info.FilePath) ? null : info.FilePath;
        FileName = string.IsNullOrWhiteSpace(info.FileName) ? "Upload" : info.FileName;
        FileSizeText = HasLocalFile ? FileHelpers.GetFileSizeReadable(FilePath!) : null;
        UploaderHost = info.UploaderHost;
        DataType = info.DataType;
        ErrorDetails = info.ErrorDetails;

        LoadPreview(info.PreviewImage);
        BuildFormats(info.ClipboardContentFormat, info.OpenUrlFormat);
        EnsureSelectableFormat();
        SelectedFormatValue = SelectedFormat?.Value ?? string.Empty;

        CopyImageCommand = new RelayCommand(CopyImageToClipboard);
        CopyFormatCommand = new RelayCommand(CopySelectedFormatToClipboard);
        OpenUrlCommand = new RelayCommand(OpenPrimaryUrl);
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        CopyErrorsCommand = new RelayCommand(CopyErrors);
        CloseCommand = new RelayCommand(RequestCloseWindow);

        if (info.AutoCloseAfterUploadForm)
        {
            _autoCloseRemainingSeconds = 60;
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoCloseTimer.Tick += (_, _) => HandleAutoCloseTick();
            _autoCloseTimer.Start();
            OnPropertyChanged(nameof(AutoCloseText));
        }
    }

    private void LoadPreview(SKBitmap? previewBitmap)
    {
        if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
        {
            if (FileHelpers.IsImageFile(FilePath))
            {
                try
                {
                    PreviewImage = new Bitmap(FilePath);
                    PreviewFallbackTitle = "Preview ready";
                    PreviewFallbackDescription = "Image preview loaded from local file.";
                    return;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "Failed to load after-upload preview image");
                }
            }
            else
            {
                PreviewFallbackTitle = "Preview unavailable";
                PreviewFallbackDescription = "Only images can be previewed. Use the file actions to open it.";
            }
        }
        else if (previewBitmap != null)
        {
            try
            {
                PreviewImage = BitmapConversionHelpers.ToAvaloniBitmap(previewBitmap);
                PreviewFallbackTitle = "Preview ready";
                PreviewFallbackDescription = "Preview loaded from captured image.";
                return;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load preview bitmap");
            }
        }

        PreviewImage = null;
    }

    partial void OnPreviewImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasPreviewImage));
        OnPropertyChanged(nameof(HasPreviewFallback));
    }

    private void BuildFormats(string? clipboardFormat, string? openUrlFormat)
    {
        var formatValues = new HashSet<string>(StringComparer.Ordinal);
        var isImage = CanCopyImage || string.Equals(DataType, "Image", StringComparison.OrdinalIgnoreCase);
        var defaultFormat = string.IsNullOrWhiteSpace(clipboardFormat) ? "$result" : clipboardFormat;
        var resolvedOpenUrlFormat = string.IsNullOrWhiteSpace(openUrlFormat) ? "$result" : openUrlFormat;

        AddFormatGroup("Primary URL", new[]
        {
            BuildFormat("Clipboard format", ResolveFormat(defaultFormat)),
            BuildFormat("Open URL format", ResolveFormat(resolvedOpenUrlFormat)),
            BuildFormat("URL", RawUrl),
            BuildFormat("Shortened URL", ShortenedUrl),
            BuildFormat("Thumbnail URL", ThumbnailUrl)
        }, formatValues);

        AddFormatGroup("Embeds", new[]
        {
            BuildFormat("Markdown", isImage ? $"![{FileName}]({PrimaryUrl})" : $"[{FileName}]({PrimaryUrl})"),
            BuildFormat("HTML", isImage
                ? $"<img src=\"{PrimaryUrl}\" alt=\"{FileName}\" />"
                : $"<a href=\"{PrimaryUrl}\">{FileName}</a>"),
            BuildFormat("BBCode", isImage
                ? $"[img]{PrimaryUrl}[/img]"
                : $"[url={PrimaryUrl}]{FileName}[/url]")
        }, formatValues);

        AddFormatGroup("Management", new[]
        {
            BuildFormat("Deletion URL", DeletionUrl)
        }, formatValues);

        AddFormatGroup("Local", new[]
        {
            BuildFormat("File path", FilePath),
            BuildFormat("File name", FileName)
        }, formatValues);

        var customFormats = SettingsManager.Settings?.ClipboardContentFormats;
        if (customFormats != null && customFormats.Count > 0)
        {
            var items = customFormats
                .Select(format => BuildFormat(format.Description, ResolveFormat(format.Format)))
                .ToArray();
            AddFormatGroup("Custom", items, formatValues);
        }
    }

    private static AfterUploadFormatEntry BuildFormat(string label, string? value)
    {
        return new AfterUploadFormatEntry(label, value);
    }

    private void AddFormatGroup(
        string title,
        IReadOnlyList<AfterUploadFormatEntry> entries,
        HashSet<string> formatValues)
    {
        var validEntries = new List<AfterUploadFormatEntry>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            if (formatValues.Add(entry.Value))
            {
                validEntries.Add(entry);
            }
        }

        if (validEntries.Count == 0)
        {
            return;
        }

        Formats.Add(AfterUploadFormatEntry.CreateHeader(title));
        foreach (var entry in validEntries)
        {
            Formats.Add(entry);
        }
    }

    private string ResolveFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return string.Empty;
        }

        return format
            .Replace("$result", PrimaryUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("$url", RawUrl ?? PrimaryUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("$shorturl", ShortenedUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$shortened", ShortenedUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$thumbnail", ThumbnailUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$deletion", DeletionUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$filepath", FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$filename", FileName, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureSelectableFormat()
    {
        if (_selectedFormat != null && _selectedFormat.IsSelectable)
        {
            return;
        }

        _selectedFormat = Formats.FirstOrDefault(entry => entry.IsSelectable);
        OnPropertyChanged(nameof(SelectedFormat));
    }

    private void HandleAutoCloseTick()
    {
        if (_autoCloseRemainingSeconds <= 0)
        {
            _autoCloseTimer?.Stop();
            RequestCloseWindow();
            return;
        }

        _autoCloseRemainingSeconds--;
        OnPropertyChanged(nameof(AutoCloseText));
    }

    private void RequestCloseWindow()
    {
        RequestClose?.Invoke();
    }

    private void CopyImageToClipboard()
    {
        if (!CanCopyImage || string.IsNullOrEmpty(FilePath))
        {
            return;
        }

        try
        {
            using var bitmap = SKBitmap.Decode(FilePath);
            if (bitmap != null)
            {
                PlatformServices.Clipboard.SetImage(bitmap);
                DebugHelper.WriteLine($"AfterUpload: Copied image to clipboard: {FilePath}");
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AfterUpload: Failed to copy image to clipboard");
        }
    }

    private void CopySelectedFormatToClipboard()
    {
        var entry = SelectedFormat ?? Formats.FirstOrDefault(item => item.IsSelectable);
        if (entry == null || !entry.IsSelectable || string.IsNullOrWhiteSpace(entry.Value))
        {
            return;
        }

        try
        {
            PlatformServices.Clipboard.SetText(entry.Value);
            DebugHelper.WriteLine($"AfterUpload: Copied format to clipboard ({entry.Label}).");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AfterUpload: Failed to copy format to clipboard");
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
            DebugHelper.WriteLine("AfterUpload: Copied errors to clipboard.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AfterUpload: Failed to copy errors to clipboard");
        }
    }

    private void OpenPrimaryUrl()
    {
        if (!CanOpenUrl)
        {
            return;
        }

        try
        {
            PlatformServices.System.OpenUrl(PrimaryUrl);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AfterUpload: Failed to open URL");
        }
    }

    private void OpenFile()
    {
        if (!CanOpenFile || string.IsNullOrEmpty(FilePath))
        {
            return;
        }

        try
        {
            PlatformServices.System.OpenFile(FilePath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AfterUpload: Failed to open file");
        }
    }

    private void OpenFolder()
    {
        if (!CanOpenFolder || string.IsNullOrEmpty(FilePath))
        {
            return;
        }

        try
        {
            FileHelpers.OpenFolderWithFile(FilePath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AfterUpload: Failed to open folder");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _autoCloseTimer?.Stop();
        _disposed = true;
    }
}

public sealed class AfterUploadFormatEntry
{
    public string Label { get; }
    public string? Value { get; }
    public bool IsHeader { get; }
    public bool IsSelectable => !IsHeader;

    private AfterUploadFormatEntry(string label, string? value, bool isHeader)
    {
        Label = label;
        Value = value;
        IsHeader = isHeader;
    }

    public AfterUploadFormatEntry(string label, string? value)
        : this(label, value, false)
    {
    }

    public static AfterUploadFormatEntry CreateHeader(string label)
    {
        return new AfterUploadFormatEntry(label, null, true);
    }
}

