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
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class ImagePropertyItem : ObservableObject
{
    [ObservableProperty]
    private string _category = "";

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _value = "";
}

public partial class ImageAnalyzerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _inputFilePath;

    [ObservableProperty]
    private string _inputFileName = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Select an image to analyze";

    [ObservableProperty]
    private bool _hasInput;

    public ObservableCollection<ImagePropertyItem> Properties { get; } = new();

    public event EventHandler? FilePickerRequested;

    [RelayCommand]
    private void Browse()
    {
        FilePickerRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetInputFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        InputFilePath = filePath;
        InputFileName = Path.GetFileName(filePath);
        HasInput = true;

        Analyze();
    }

    [RelayCommand]
    private void Analyze()
    {
        if (string.IsNullOrEmpty(InputFilePath)) return;

        IsLoading = true;
        Properties.Clear();
        StatusText = "Analyzing...";

        try
        {
            var fileInfo = new FileInfo(InputFilePath);

            // File properties
            AddProperty("File", "Name", fileInfo.Name);
            AddProperty("File", "Size", FormatFileSize(fileInfo.Length));
            AddProperty("File", "Extension", fileInfo.Extension);
            AddProperty("File", "Directory", fileInfo.DirectoryName ?? "");
            AddProperty("File", "Created", fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
            AddProperty("File", "Modified", fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));

            // Image properties
            using var bmp = ImageHelpers.LoadBitmap(InputFilePath);
            if (bmp != null)
            {
                AddProperty("Image", "Width", $"{bmp.Width} px");
                AddProperty("Image", "Height", $"{bmp.Height} px");
                AddProperty("Image", "Pixel Count", $"{(long)bmp.Width * bmp.Height:N0}");
                AddProperty("Image", "Aspect Ratio", GetAspectRatio(bmp.Width, bmp.Height));
                AddProperty("Image", "Color Type", bmp.ColorType.ToString());
                AddProperty("Image", "Alpha Type", bmp.AlphaType.ToString());
                AddProperty("Image", "Bytes Per Pixel", bmp.BytesPerPixel.ToString());
                AddProperty("Image", "Row Bytes", $"{bmp.RowBytes:N0}");
                AddProperty("Image", "Total Bytes", FormatFileSize((long)bmp.RowBytes * bmp.Height));

                // Sample average color (center region)
                var avgColor = SampleAverageColor(bmp);
                AddProperty("Color", "Average Color", $"#{avgColor.Red:X2}{avgColor.Green:X2}{avgColor.Blue:X2}");
                AddProperty("Color", "Average RGB", $"R:{avgColor.Red} G:{avgColor.Green} B:{avgColor.Blue}");
                AddProperty("Color", "Has Transparency", (bmp.AlphaType != SkiaSharp.SKAlphaType.Opaque).ToString());

                StatusText = $"{InputFileName} ({bmp.Width} x {bmp.Height})";
            }
            else
            {
                StatusText = "Could not load image data";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            DebugHelper.WriteException(ex, "ImageAnalyzer");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CopyAll()
    {
        if (Properties.Count == 0) return;

        var sb = new StringBuilder();
        string? currentCategory = null;

        foreach (var prop in Properties)
        {
            if (prop.Category != currentCategory)
            {
                if (currentCategory != null) sb.AppendLine();
                sb.AppendLine($"[{prop.Category}]");
                currentCategory = prop.Category;
            }
            sb.AppendLine($"  {prop.Name}: {prop.Value}");
        }

        if (PlatformServices.IsInitialized && PlatformServices.Clipboard != null)
        {
            PlatformServices.Clipboard.SetText(sb.ToString());
            StatusText = "Copied to clipboard";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputFilePath = null;
        InputFileName = "";
        HasInput = false;
        Properties.Clear();
        StatusText = "Select an image to analyze";
    }

    private void AddProperty(string category, string name, string value)
    {
        Properties.Add(new ImagePropertyItem
        {
            Category = category,
            Name = name,
            Value = value
        });
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.#} GB";
    }

    private static string GetAspectRatio(int width, int height)
    {
        if (height == 0) return "N/A";
        double ratio = (double)width / height;

        // Common ratios
        if (Math.Abs(ratio - 16.0 / 9) < 0.01) return "16:9";
        if (Math.Abs(ratio - 4.0 / 3) < 0.01) return "4:3";
        if (Math.Abs(ratio - 3.0 / 2) < 0.01) return "3:2";
        if (Math.Abs(ratio - 1.0) < 0.01) return "1:1";
        if (Math.Abs(ratio - 21.0 / 9) < 0.02) return "21:9";

        int gcd = GCD(width, height);
        return $"{width / gcd}:{height / gcd}";
    }

    private static int GCD(int a, int b)
    {
        while (b != 0)
        {
            int t = b;
            b = a % b;
            a = t;
        }
        return a;
    }

    private static SkiaSharp.SKColor SampleAverageColor(SkiaSharp.SKBitmap bmp)
    {
        long totalR = 0, totalG = 0, totalB = 0;
        int sampleCount = 0;

        // Sample a grid of points for performance
        int stepX = Math.Max(1, bmp.Width / 20);
        int stepY = Math.Max(1, bmp.Height / 20);

        for (int y = 0; y < bmp.Height; y += stepY)
        {
            for (int x = 0; x < bmp.Width; x += stepX)
            {
                var pixel = bmp.GetPixel(x, y);
                totalR += pixel.Red;
                totalG += pixel.Green;
                totalB += pixel.Blue;
                sampleCount++;
            }
        }

        if (sampleCount == 0) return SkiaSharp.SKColors.Black;

        return new SkiaSharp.SKColor(
            (byte)(totalR / sampleCount),
            (byte)(totalG / sampleCount),
            (byte)(totalB / sampleCount));
    }
}
