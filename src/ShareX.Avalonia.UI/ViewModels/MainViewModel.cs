#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Editor.Annotations;
using ShareX.Ava.Core;
using ShareX.Ava.Core.Tasks;
using System.Collections.ObjectModel;

namespace ShareX.Ava.UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        public sealed class GradientPreset
        {
            public required string Name { get; init; }
            public required IBrush Brush { get; init; }
        }

        private const string OutputRatioAuto = "Auto";

        [ObservableProperty]
        private string _exportState = "";

        [ObservableProperty]
        private bool _showCaptureToolbar = true;

        // Events to signal View to perform canvas operations
        public event EventHandler? UndoRequested;
        public event EventHandler? RedoRequested;
        public event EventHandler? DeleteRequested;
        public event EventHandler? ClearAnnotationsRequested;

        [ObservableProperty]
        private ObservableCollection<WorkerTask> _tasks;

        [ObservableProperty]
        private Bitmap? _previewImage;

        [ObservableProperty]
        private bool _hasPreviewImage;

        [ObservableProperty]
        private double _imageWidth;

        [ObservableProperty]
        private double _imageHeight;

        partial void OnPreviewImageChanged(Bitmap? value)
        {
            if (value != null)
            {
                ImageWidth = value.Size.Width;
                ImageHeight = value.Size.Height;
                HasPreviewImage = true;
                OnPropertyChanged(nameof(SmartPaddingColor));
                
                // Apply smart padding crop if enabled (but not if we're already applying it)
                if (UseSmartPadding && !_isApplyingSmartPadding)
                {
                    ApplySmartPaddingCrop();
                }
            }
            else
            {
                ImageWidth = 0;
                ImageHeight = 0;
                HasPreviewImage = false;
            }
        }

        [ObservableProperty]
        private double _previewPadding = 30;

        [ObservableProperty]
        private double _smartPadding = 0;

        [ObservableProperty]
        private bool _useSmartPadding = false;

        private bool _isApplyingSmartPadding = false;

        public Thickness SmartPaddingThickness => new Thickness(SmartPadding);

        public IBrush SmartPaddingColor
        {
            get
            {
                if (_previewImage == null || _smartPadding <= 0)
                {
                    return Brushes.Transparent;
                }

                try
                {
                    var topLeftColor = SamplePixelColor(PreviewImage, 0, 0);
                    return new SolidColorBrush(topLeftColor);
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
        }

        [ObservableProperty]
        private double _previewCornerRadius = 15;

        [ObservableProperty]
        private double _shadowBlur = 30;

        private const double MinZoom = 0.25;
        private const double MaxZoom = 4.0;
        private const double ZoomStep = 0.1;

        [ObservableProperty]
        private double _zoom = 1.0;

        [ObservableProperty]
        private string _imageDimensions = "No image";

        [ObservableProperty]
        private bool _isPngFormat = true;

        [ObservableProperty]
        private string _appVersion;

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private string _selectedColor = "#EF4444";

        [ObservableProperty]
        private int _strokeWidth = 4;

        [ObservableProperty]
        private EditorTool _activeTool = EditorTool.Rectangle;

        [ObservableProperty]
        private EffectsPanelViewModel _effectsPanel = new();

        [ObservableProperty]
        private bool _isEffectsPanelOpen;

        [ObservableProperty]
        private int _numberCounter = 1;

        [ObservableProperty]
        private string _selectedOutputRatio = OutputRatioAuto;

        [ObservableProperty]
        private double? _targetOutputAspectRatio;

        [RelayCommand]
        private void ResetNumberCounter()
        {
            NumberCounter = 1;
        }

        [RelayCommand]
        private void SetOutputRatio(string ratioKey)
        {
            SelectedOutputRatio = string.IsNullOrWhiteSpace(ratioKey) ? OutputRatioAuto : ratioKey;
        }

        // Modal Overlay Properties
        [ObservableProperty]
        private bool _isModalOpen;

        [ObservableProperty]
        private object? _modalContent;

        [RelayCommand]
        private void CloseModal()
        {
            IsModalOpen = false;
            ModalContent = null;
        }

        [ObservableProperty]
        private IBrush _canvasBackground;

        public ObservableCollection<GradientPreset> GradientPresets { get; }

        [ObservableProperty]
        private double _canvasCornerRadius = 0;

        [ObservableProperty]
        private Thickness _canvasPadding;

        [ObservableProperty]
        private BoxShadows _canvasShadow;

        // Event for View to provide flattened image
        public event Func<Task<Bitmap?>>? SnapshotRequested;

        // Event for View to show SaveAs dialog and return selected path
        public event Func<Task<string?>>? SaveAsRequested;

        [ObservableProperty]
        private string? _lastSavedPath;



        public static MainViewModel Current { get; private set; }

        public MainViewModel()
        {
            Current = this;
            _tasks = new ObservableCollection<WorkerTask>();
            GradientPresets = BuildGradientPresets();
            _canvasBackground = CopyBrush(GradientPresets[1].Brush);

            // Get version from assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            _appVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";

            UpdateCanvasProperties();
        }

        partial void OnSelectedOutputRatioChanged(string value)
        {
            _targetOutputAspectRatio = ParseAspectRatio(value);
            UpdateCanvasProperties();
            StatusText = _targetOutputAspectRatio.HasValue
                ? $"Output ratio set to {value}"
                : "Output ratio auto";
        }

        partial void OnPreviewPaddingChanged(double value)
        {
            UpdateCanvasProperties();
        }

        partial void OnSmartPaddingChanged(double value)
        {
            OnPropertyChanged(nameof(SmartPaddingColor));
            OnPropertyChanged(nameof(SmartPaddingThickness));
        }

        partial void OnUseSmartPaddingChanged(bool value)
        {
            ApplySmartPaddingCrop();
        }

        partial void OnPreviewCornerRadiusChanged(double value)
        {
            UpdateCanvasProperties();
        }

        partial void OnShadowBlurChanged(double value)
        {
            UpdateCanvasProperties();
        }

        private Color SamplePixelColor(Bitmap bitmap, int x, int y)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;

            using var skBitmap = SkiaSharp.SKBitmap.Decode(stream);
            if (skBitmap == null || x >= skBitmap.Width || y >= skBitmap.Height)
            {
                return Colors.Transparent;
            }

            var skColor = skBitmap.GetPixel(x, y);
            return Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
        }

        private void ApplySmartPaddingCrop()
        {
            if (_originalSourceImage == null || PreviewImage == null)
            {
                return;
            }

            if (_isApplyingSmartPadding)
            {
                return; // Prevent recursive calls
            }

            if (!UseSmartPadding)
            {
                // Restore original image from backup
                if (!_isApplyingSmartPadding && _originalSourceImage != null)
                {
                    _isApplyingSmartPadding = true;
                    try
                    {
                        UpdatePreview(_originalSourceImage);
                        StatusText = "Smart Padding: Restored original image";
                    }
                    finally
                    {
                        _isApplyingSmartPadding = false;
                    }
                }
                return;
            }

            _isApplyingSmartPadding = true;
            try
            {
                // Start from the original image backup
                var skBitmap = _originalSourceImage;
                
                if (skBitmap == null) return;

                // Get top-left pixel color as reference
                var targetColor = skBitmap.GetPixel(0, 0);
                const int tolerance = 30; // Color tolerance for matching

                // Find bounds of content (non-matching pixels)
                int minX = skBitmap.Width;
                int minY = skBitmap.Height;
                int maxX = 0;
                int maxY = 0;

                for (int y = 0; y < skBitmap.Height; y++)
                {
                    for (int x = 0; x < skBitmap.Width; x++)
                    {
                        var pixel = skBitmap.GetPixel(x, y);
                        
                        // Check if pixel is different from target color (within tolerance)
                        if (Math.Abs(pixel.Red - targetColor.Red) > tolerance ||
                            Math.Abs(pixel.Green - targetColor.Green) > tolerance ||
                            Math.Abs(pixel.Blue - targetColor.Blue) > tolerance ||
                            Math.Abs(pixel.Alpha - targetColor.Alpha) > tolerance)
                        {
                            minX = Math.Min(minX, x);
                            minY = Math.Min(minY, y);
                            maxX = Math.Max(maxX, x);
                            maxY = Math.Max(maxY, y);
                        }
                    }
                }

                // Check if we found any content
                if (minX > maxX || minY > maxY)
                {
                    // No content found, keep original
                    StatusText = "Smart Padding: No content to crop";
                    return;
                }

                // Calculate crop rectangle
                int cropX = minX;
                int cropY = minY;
                int cropWidth = maxX - minX + 1;
                int cropHeight = maxY - minY + 1;

                // Ensure valid dimensions
                if (cropWidth <= 0 || cropHeight <= 0)
                {
                    return;
                }

                // Perform the crop on the original image
                var cropped = ShareX.Ava.Common.ImageHelpers.Crop(_originalSourceImage, cropX, cropY, cropWidth, cropHeight);

                // Update preview with cropped image
                _currentSourceImage = cropped;
                PreviewImage = Helpers.BitmapConversionHelpers.ToAvaloniBitmap(cropped);
                ImageDimensions = $"{cropped.Width} x {cropped.Height}";
                StatusText = $"Smart Padding: Cropped to {cropWidth}x{cropHeight}";
            }
            catch (Exception ex)
            {
                StatusText = $"Smart Padding error: {ex.Message}";
                ShareX.Ava.Common.DebugHelper.WriteLine($"Smart padding crop failed: {ex.Message}");
            }
            finally
            {
                _isApplyingSmartPadding = false;
            }
        }

        [RelayCommand]
        private void ApplyGradientPreset(GradientPreset preset)
        {
            // Clone to avoid accidental brush sharing between controls
            CanvasBackground = CopyBrush(preset.Brush);
            StatusText = $"Gradient set to {preset.Name}";
        }

        private void UpdateCanvasProperties()
        {
            CanvasPadding = CalculateOutputPadding(PreviewPadding, _targetOutputAspectRatio);
            CanvasShadow = new BoxShadows(new BoxShadow
            {
                Blur = ShadowBlur,
                Color = Color.FromArgb(80, 0, 0, 0),
                OffsetX = 0,
                OffsetY = 10
            });
            CanvasCornerRadius = Math.Max(0, PreviewCornerRadius);
            OnPropertyChanged(nameof(SmartPaddingColor));
        }

        private Thickness CalculateOutputPadding(double basePadding, double? targetAspectRatio)
        {
            if (_previewImage == null || _previewImage.Size.Width <= 0 || _previewImage.Size.Height <= 0 || !targetAspectRatio.HasValue)
            {
                return new Thickness(basePadding);
            }

            double imageWidth = _previewImage.Size.Width;
            double imageHeight = _previewImage.Size.Height;

            double totalWidth = imageWidth + (basePadding * 2);
            double totalHeight = imageHeight + (basePadding * 2);
            double currentAspect = totalWidth / totalHeight;
            double target = targetAspectRatio.Value;

            double extraX = 0;
            double extraY = 0;

            const double epsilon = 0.0001;
            if (currentAspect > target + epsilon)
            {
                // Too wide, add vertical padding
                double requiredHeight = totalWidth / target;
                double addHeight = Math.Max(0, requiredHeight - totalHeight);
                extraY = addHeight / 2;
            }
            else if (currentAspect + epsilon < target)
            {
                // Too tall, add horizontal padding
                double requiredWidth = totalHeight * target;
                double addWidth = Math.Max(0, requiredWidth - totalWidth);
                extraX = addWidth / 2;
            }

            return new Thickness(basePadding + extraX, basePadding + extraY, basePadding + extraX, basePadding + extraY);
        }

        private static double? ParseAspectRatio(string ratio)
        {
            if (string.IsNullOrWhiteSpace(ratio) || string.Equals(ratio, OutputRatioAuto, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var parts = ratio.Split(':');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var w) &&
                double.TryParse(parts[1], out var h) &&
                w > 0 && h > 0)
            {
                return w / h;
            }

            return null;
        }

        private static ObservableCollection<GradientPreset> BuildGradientPresets()
        {
            static LinearGradientBrush Make(string start, string end) => new()
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new Avalonia.Media.GradientStop(Color.Parse(start), 0),
                    new Avalonia.Media.GradientStop(Color.Parse(end), 1)
                }
            };

            return new ObservableCollection<GradientPreset>
            {
                new() { Name = "Sunset", Brush = Make("#F093FB", "#F5576C") },
                new() { Name = "Ocean", Brush = Make("#667EEA", "#764BA2") },
                new() { Name = "Forest", Brush = Make("#11998E", "#38EF7D") },
                new() { Name = "Fire", Brush = Make("#F12711", "#F5AF19") },
                new() { Name = "Cool Blue", Brush = Make("#2193B0", "#6DD5ED") },
                new() { Name = "Lavender", Brush = Make("#B8B8FF", "#D6A4FF") },
                new() { Name = "Aqua", Brush = Make("#13547A", "#80D0C7") },
                new() { Name = "Grape", Brush = Make("#7F00FF", "#E100FF") },
                new() { Name = "Peach", Brush = Make("#FFB88C", "#DE6262") },
                new() { Name = "Sky", Brush = Make("#56CCF2", "#2F80ED") },
                new() { Name = "Warm", Brush = Make("#F2994A", "#F2C94C") },
                new() { Name = "Mint", Brush = Make("#00B09B", "#96C93D") },
                new() { Name = "Midnight", Brush = Make("#232526", "#414345") },
                new() { Name = "Carbon", Brush = Make("#373B44", "#4286F4") },
                new() { Name = "Deep Space", Brush = Make("#000428", "#004E92") },
                new() { Name = "Noir", Brush = Make("#0F2027", "#2C5364") },
                new() { Name = "Royal", Brush = Make("#141E30", "#243B55") },
                new() { Name = "Rose Gold", Brush = Make("#E8CBC0", "#636FA4") },
                new() { Name = "Emerald", Brush = Make("#076585", "#FFFFFF") },
                new() { Name = "Amethyst", Brush = Make("#9D50BB", "#6E48AA") },
                new() { Name = "Neon", Brush = Make("#FF0844", "#FFB199") },
                new() { Name = "Aurora", Brush = Make("#00C9FF", "#92FE9D") },
                new() { Name = "Candy", Brush = Make("#D53369", "#DAAE51") },
                new() { Name = "Clean", Brush = new SolidColorBrush(Color.Parse("#FFFFFF")) }
            };
        }

        private static IBrush CopyBrush(IBrush brush)
        {
            switch (brush)
            {
                case SolidColorBrush solid:
                    return new SolidColorBrush(solid.Color)
                    {
                        Opacity = solid.Opacity
                    };
                case LinearGradientBrush linear:
                    var stops = new GradientStops();
                    foreach (var stop in linear.GradientStops)
                    {
                        stops.Add(new Avalonia.Media.GradientStop(stop.Color, stop.Offset));
                    }

                    return new LinearGradientBrush
                    {
                        StartPoint = linear.StartPoint,
                        EndPoint = linear.EndPoint,
                        GradientStops = stops,
                        SpreadMethod = linear.SpreadMethod,
                        Opacity = linear.Opacity
                    };
                default:
                    // Fall back to the original reference if an unsupported brush type is supplied.
                    return brush;
            }
        }

        partial void OnZoomChanged(double value)
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (Math.Abs(clamped - value) > 0.0001)
            {
                Zoom = clamped;
                return;
            }

            StatusText = $"Zoom {clamped:P0}";
        }

        [RelayCommand]
        private async Task CaptureFullscreen()
        {
            await ExecuteCapture(HotkeyType.PrintScreen);
        }

        [RelayCommand]
        private async Task CaptureRegion()
        {
            await ExecuteCapture(HotkeyType.RectangleRegion);
        }

        [RelayCommand]
        private async Task CaptureWindow()
        {
            await ExecuteCapture(HotkeyType.ActiveWindow);
        }

        [RelayCommand]
        private async Task CaptureAndUpload()
        {
            await ExecuteCapture(HotkeyType.RectangleRegion, AfterCaptureTasks.UploadImageToHost);
        }

        // Static color palette for annotation toolbar
        public static string[] ColorPalette => new[]
        {
            "#EF4444", "#F97316", "#EAB308", "#22C55E",
            "#0EA5E9", "#6366F1", "#A855F7", "#EC4899",
            "#FFFFFF", "#000000", "#64748B", "#1E293B"
        };

        // Static stroke widths
        public static int[] StrokeWidths => new[] { 2, 4, 6, 8, 10 };

        [RelayCommand]
        private void SelectTool(EditorTool tool)
        {
            ActiveTool = tool;
        }

        [RelayCommand]
        private void SetColor(string color)
        {
            SelectedColor = color;
        }

        [RelayCommand]
        private void SetStrokeWidth(int width)
        {
            StrokeWidth = width;
        }

        [RelayCommand]
        private void Undo()
        {
            UndoRequested?.Invoke(this, EventArgs.Empty);
            StatusText = "Undo requested";
        }

        [RelayCommand]
        private void Redo()
        {
            RedoRequested?.Invoke(this, EventArgs.Empty);
            StatusText = "Redo requested";
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
            StatusText = "Delete requested";
        }

        [RelayCommand]
        private void ClearAnnotations()
        {
            ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
            ResetNumberCounter();
            StatusText = "Annotations cleared";
        }

        [RelayCommand]
        private void ToggleEffectsPanel()
        {
            IsEffectsPanelOpen = !IsEffectsPanelOpen;
            StatusText = IsEffectsPanelOpen ? "Effects panel opened" : "Effects panel closed";
        }

        [RelayCommand]
        private void ZoomIn()
        {
            Zoom = Math.Clamp(Math.Round((Zoom + ZoomStep) * 100) / 100, MinZoom, MaxZoom);
        }

        [RelayCommand]
        private void ZoomOut()
        {
            Zoom = Math.Clamp(Math.Round((Zoom - ZoomStep) * 100) / 100, MinZoom, MaxZoom);
        }

        [RelayCommand]
        private void ResetZoom()
        {
            Zoom = 1.0;
        }

        [RelayCommand]
        private void ApplyEffect()
        {
            if (EffectsPanel.SelectedEffect == null)
            {
                StatusText = "No effect selected";
                return;
            }

            if (PreviewImage == null)
            {
                StatusText = "No image to apply effect to";
                return;
            }

            try
            {
                StatusText = $"Applying {EffectsPanel.SelectedEffect.Name}...";

                // Convert Avalonia Bitmap to SKBitmap
                using var skBitmap = Helpers.BitmapConversionHelpers.ToSKBitmap(PreviewImage);

                // Apply effect
                using var resultBitmap = EffectsPanel.SelectedEffect.Apply(skBitmap);

                // Convert back to Avalonia Bitmap
                PreviewImage = Helpers.BitmapConversionHelpers.ToAvaloniBitmap(resultBitmap);

                StatusText = $"Applied {EffectsPanel.SelectedEffect.Name}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error applying effect: {ex.Message}";
            }
        }

        [RelayCommand]
        private void Clear()
        {
            PreviewImage = null;
            _currentSourceImage = null;
            _originalSourceImage = null;
            // HasPreviewImage = false; // Handled by OnPreviewImageChanged
            ImageDimensions = "No image";
            StatusText = "Ready";
            ResetNumberCounter();

            // Clear annotations as well
            ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
        }

        // Event for View to handle clipboard copy (requires TopLevel access)
        public event Func<Bitmap, Task>? CopyRequested;

        // Event for View to show error dialog
        public event Func<string, string, Task>? ShowErrorDialog;

        [RelayCommand]
        private async Task Copy()
        {
            // Get flattened image with annotations
            Bitmap? snapshot = null;
            if (SnapshotRequested != null)
            {
                snapshot = await SnapshotRequested.Invoke();
            }

            // Fallback to preview image if snapshot fails
            var imageToUse = snapshot ?? PreviewImage;
            if (imageToUse == null)
            {
                StatusText = "No image to copy";
                return;
            }

            if (CopyRequested != null)
            {
                try
                {
                    await CopyRequested.Invoke(imageToUse);
                    StatusText = snapshot != null
                        ? "Image with annotations copied to clipboard"
                        : "Image copied to clipboard";
                    ExportState = "Copied";
                    ShareX.Ava.Common.DebugHelper.WriteLine("Clipboard copy: Image copied to clipboard.");
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Failed to copy image to clipboard.\n\nError: {ex.Message}";
                    StatusText = $"Copy failed: {ex.Message}";
                    ShareX.Ava.Common.DebugHelper.WriteLine($"Clipboard copy failed: {ex.Message}");

                    // Show error dialog
                    if (ShowErrorDialog != null)
                    {
                        await ShowErrorDialog.Invoke("Copy Failed", errorMessage);
                    }
                }
            }
            else
            {
                StatusText = "Clipboard not available";
            }
        }

        [RelayCommand]
        private async Task QuickSave()
        {
            // Try get flattened image first
            Bitmap? snapshot = null;
            if (SnapshotRequested != null)
            {
                snapshot = await SnapshotRequested.Invoke();
            }

            if (snapshot == null && _currentSourceImage == null) return;

            try
            {
                // Simple quick save to Pictures/ShareX
                var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShareX");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                var filename = $"ShareX_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
                var path = System.IO.Path.Combine(folder, filename);

                if (snapshot != null)
                {
                    snapshot.Save(path);
                }
                else if (_currentSourceImage != null)
                {
                    ShareX.Ava.Common.ImageHelpers.SaveBitmap(_currentSourceImage, path);
                }

                StatusText = $"Saved to {filename}";
                ExportState = "Saved";
                ShareX.Ava.Common.DebugHelper.WriteLine($"File saved: {path}");
            }
            catch (Exception ex)
            {
                StatusText = $"Save failed: {ex.Message}";
                ShareX.Ava.Common.DebugHelper.WriteLine($"File save failed: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task SaveAs()
        {
            if (SaveAsRequested == null)
            {
                StatusText = "SaveAs dialog not available";
                return;
            }

            // Show file picker dialog via View
            var path = await SaveAsRequested.Invoke();
            if (string.IsNullOrEmpty(path))
            {
                StatusText = "Save cancelled";
                return;
            }

            // Get flattened image with annotations
            Bitmap? snapshot = null;
            if (SnapshotRequested != null)
            {
                snapshot = await SnapshotRequested.Invoke();
            }

            var imageToSave = snapshot ?? PreviewImage;
            if (imageToSave == null)
            {
                StatusText = "No image to save";
                return;
            }

            try
            {
                // Save based on file extension
                var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

                imageToSave.Save(path);

                var filename = System.IO.Path.GetFileName(path);
                StatusText = $"Saved to {filename}";
                ExportState = "Saved";
                LastSavedPath = path;
                ShareX.Ava.Common.DebugHelper.WriteLine($"File saved (Save As): {path}");
            }
            catch (Exception ex)
            {
                StatusText = $"Save failed: {ex.Message}";
                ShareX.Ava.Common.DebugHelper.WriteLine($"File save failed (Save As): {ex.Message}");
            }
        }

        private async Task ExecuteCapture(HotkeyType jobType, AfterCaptureTasks afterCapture = AfterCaptureTasks.SaveImageToFile)
        {
            // Clone default settings to use user's config (paths, patterns, etc.)
            var defaultSettings = SettingManager.Settings.DefaultTaskSettings;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(defaultSettings);
            var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskSettings>(json)!;

            settings.Job = jobType;
            settings.AfterCaptureJob = afterCapture;

            var task = WorkerTask.Create(settings);
            Tasks.Add(task);
            await task.StartAsync();

            // Update preview if capture succeeded
            if (task.Info?.Metadata?.Image != null)
            {
                UpdatePreview(task.Info.Metadata.Image);
            }
        }

        private SkiaSharp.SKBitmap? _currentSourceImage;
        private SkiaSharp.SKBitmap? _originalSourceImage; // Backup for smart padding restore

        public void UpdatePreview(SkiaSharp.SKBitmap image)
        {
            // Store source image for operations like Crop
            _currentSourceImage = image;

            // Update original backup first so smart padding uses the new image during PreviewImage change
            if (!_isApplyingSmartPadding)
            {
                _originalSourceImage?.Dispose();
                _originalSourceImage = image.Copy();
            }

            // Convert SKBitmap to Avalonia Bitmap
            PreviewImage = Helpers.BitmapConversionHelpers.ToAvaloniBitmap(image);
            ImageDimensions = $"{image.Width} x {image.Height}";
            StatusText = $"Image: {image.Width} × {image.Height}";

            // Reset view state for the new image
            Zoom = 1.0;
            ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);
            ResetNumberCounter();
        }

        public void CropImage(int x, int y, int width, int height)
        {
            if (_currentSourceImage == null) return;
            if (width <= 0 || height <= 0) return;

            // Ensure bounds
            var rect = new SkiaSharp.SKRectI(x, y, x + width, y + height);
            var imageRect = new SkiaSharp.SKRectI(0, 0, _currentSourceImage.Width, _currentSourceImage.Height);
            rect.Intersect(imageRect);

            if (rect.Width <= 0 || rect.Height <= 0) return;

            var cropped = ShareX.Ava.Common.ImageHelpers.Crop(_currentSourceImage, rect.Left, rect.Top, rect.Width, rect.Height);
            UpdatePreview(cropped);
        }
    }
}

