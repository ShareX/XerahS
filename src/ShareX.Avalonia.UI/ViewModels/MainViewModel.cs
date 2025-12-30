using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Core.Managers;
using ShareX.Avalonia.Core.Tasks;
using ShareX.Avalonia.Uploaders;

namespace ShareX.Avalonia.UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<WorkerTask> _tasks;

        [ObservableProperty]
        private Bitmap? _previewImage;

        [ObservableProperty]
        private bool _hasPreviewImage;

        [ObservableProperty]
        private double _previewPadding = 50;

        [ObservableProperty]
        private double _previewCornerRadius = 16;

        [ObservableProperty]
        private double _shadowBlur = 30;

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
        private EditorTool _activeTool = EditorTool.Select;

        [ObservableProperty]
        private IBrush _canvasBackground;

        [ObservableProperty]
        private double _canvasCornerRadius = 0;

        [ObservableProperty]
        private Thickness _canvasPadding;

        [ObservableProperty]
        private BoxShadows _canvasShadow;

        public MainViewModel()
        {
            _tasks = new ObservableCollection<WorkerTask>();
            _canvasBackground = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse("#667EEA"), 0),
                    new GradientStop(Color.Parse("#764BA2"), 1)
                }
            };

            // Get version from assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            _appVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";

            UpdateCanvasProperties();
        }

        partial void OnPreviewPaddingChanged(double value)
        {
            UpdateCanvasProperties();
        }

        partial void OnShadowBlurChanged(double value)
        {
            UpdateCanvasProperties();
        }

        private void UpdateCanvasProperties()
        {
            CanvasPadding = new Thickness(PreviewPadding);
            CanvasShadow = new BoxShadows(new BoxShadow
            {
                Blur = ShadowBlur,
                Color = Color.FromArgb(80, 0, 0, 0),
                OffsetX = 0,
                OffsetY = 10
            });
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
            // TODO: Implement undo stack
            StatusText = "Undo (not yet implemented)";
        }

        [RelayCommand]
        private void Redo()
        {
            // TODO: Implement redo stack
            StatusText = "Redo (not yet implemented)";
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            // TODO: Delete selected annotation
            StatusText = "Delete selected (not yet implemented)";
        }

        [RelayCommand]
        private void Clear()
        {
            PreviewImage = null;
            HasPreviewImage = false;
            ImageDimensions = "No image";
            StatusText = "Ready";
        }

        [RelayCommand]
        private async Task Copy()
        {
            if (PreviewImage == null) return;
            // TODO: Copy image to clipboard
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task QuickSave()
        {
            if (PreviewImage == null) return;
            // TODO: Quick save to default location
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task SaveAs()
        {
            if (PreviewImage == null) return;
            // TODO: Show save dialog
            await Task.CompletedTask;
        }

        private async Task ExecuteCapture(HotkeyType jobType, AfterCaptureTasks afterCapture = AfterCaptureTasks.SaveImageToFile)
        {
            var settings = new TaskSettings
            {
                Job = jobType,
                AfterCaptureJob = afterCapture
            };

            var task = WorkerTask.Create(settings);
            Tasks.Add(task);
            await task.StartAsync();

            // Update preview if capture succeeded
            if (task.Info?.Metadata?.Image != null)
            {
                UpdatePreview(task.Info.Metadata.Image);
            }
        }

        private void UpdatePreview(System.Drawing.Image image)
        {
            // Convert System.Drawing.Image to Avalonia Bitmap
            using var ms = new System.IO.MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            PreviewImage = new Bitmap(ms);
            HasPreviewImage = true;
            ImageDimensions = $"{image.Width} x {image.Height}";
            StatusText = $"Image: {image.Width} × {image.Height}";
        }
    }
}
