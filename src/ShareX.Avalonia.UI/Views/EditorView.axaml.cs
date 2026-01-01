#nullable enable

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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShareX.Ava.UI.ViewModels;
using ShareX.Ava.UI.Controls;
using System.ComponentModel;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ShareX.Ava.UI.Views
{
    public partial class EditorView : UserControl
    {
        private const double MinZoom = 0.25;
        private const double MaxZoom = 4.0;
        private const double ZoomStep = 0.1;

        private bool _isPanning;
        private Point _panStart;
        private Vector _panOrigin;

        private readonly AnnotationCanvasViewModel _canvasViewModel = new();

        public EditorView()
        {
            InitializeComponent();
            AddHandler(PointerWheelChangedEvent, OnPreviewPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (this.FindControl<AnnotationCanvas>("AnnotationCanvasControl") is { } canvas)
            {
                canvas.ViewModel = _canvasViewModel;
                canvas.Focusable = true;
                canvas.Focus();
            }

            if (DataContext is MainViewModel vm)
            {
                vm.UndoRequested += OnUndoRequested;
                vm.RedoRequested += OnRedoRequested;
                vm.DeleteRequested += OnDeleteRequested;
                vm.ClearAnnotationsRequested += OnClearRequested;
                vm.SnapshotRequested += GetSnapshot;
                vm.SaveAsRequested += ShowSaveAsDialog;
                vm.CopyRequested += CopyToClipboard;
                vm.ShowErrorDialog += ShowErrorDialog;
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.PropertyChanged += OnViewModelPropertyChanged;

                SyncFromVm(vm);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.UndoRequested -= OnUndoRequested;
                vm.RedoRequested -= OnRedoRequested;
                vm.DeleteRequested -= OnDeleteRequested;
                vm.ClearAnnotationsRequested -= OnClearRequested;
                vm.SnapshotRequested -= GetSnapshot;
                vm.SaveAsRequested -= ShowSaveAsDialog;
                vm.CopyRequested -= CopyToClipboard;
                vm.ShowErrorDialog -= ShowErrorDialog;
                vm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void SyncFromVm(MainViewModel vm)
        {
            _canvasViewModel.ActiveTool = vm.ActiveTool;
            _canvasViewModel.StrokeColor = vm.SelectedColor;
            _canvasViewModel.StrokeWidth = vm.StrokeWidth;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MainViewModel vm) return;
            if (e.PropertyName == nameof(MainViewModel.ActiveTool))
            {
                _canvasViewModel.ActiveTool = vm.ActiveTool;
            }
            else if (e.PropertyName == nameof(MainViewModel.SelectedColor))
            {
                _canvasViewModel.StrokeColor = vm.SelectedColor;
            }
            else if (e.PropertyName == nameof(MainViewModel.StrokeWidth))
            {
                _canvasViewModel.StrokeWidth = vm.StrokeWidth;
            }
            else if (e.PropertyName == nameof(MainViewModel.PreviewImage))
            {
                // clear annotations on new image
                _canvasViewModel.Clear();
            }
        }

        private void OnUndoRequested(object? sender, EventArgs e)
        {
            _canvasViewModel.Undo();
            InvalidateVisual();
        }

        private void OnRedoRequested(object? sender, EventArgs e)
        {
            _canvasViewModel.Redo();
            InvalidateVisual();
        }

        private void OnDeleteRequested(object? sender, EventArgs e)
        {
            _canvasViewModel.RemoveSelected();
            InvalidateVisual();
        }

        private void OnClearRequested(object? sender, EventArgs e)
        {
            _canvasViewModel.Clear();
            InvalidateVisual();
        }

        private void OnPreviewPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

            var oldZoom = vm.Zoom;
            var direction = e.Delta.Y > 0 ? 1 : -1;
            var newZoom = Math.Clamp(Math.Round((oldZoom + direction * ZoomStep) * 100) / 100, MinZoom, MaxZoom);
            if (Math.Abs(newZoom - oldZoom) < 0.0001) return;

            var scrollViewer = this.FindControl<ScrollViewer>("CanvasScrollViewer");
            if (scrollViewer != null)
            {
                var pointerPosition = e.GetPosition(scrollViewer);
                var offsetBefore = scrollViewer.Offset;
                var logicalPoint = new Vector(
                    (offsetBefore.X + pointerPosition.X) / oldZoom,
                    (offsetBefore.Y + pointerPosition.Y) / oldZoom);

                vm.Zoom = newZoom;

                Dispatcher.UIThread.Post(() =>
                {
                    var targetOffset = new Vector(
                        logicalPoint.X * newZoom - pointerPosition.X,
                        logicalPoint.Y * newZoom - pointerPosition.Y);

                    var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
                    var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

                    scrollViewer.Offset = new Vector(
                        Math.Clamp(targetOffset.X, 0, maxX),
                        Math.Clamp(targetOffset.Y, 0, maxY));
                }, DispatcherPriority.Render);
            }
            else
            {
                vm.Zoom = newZoom;
            }

            e.Handled = true;
        }

        private void OnScrollViewerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            var properties = e.GetCurrentPoint(scrollViewer).Properties;
            if (!properties.IsMiddleButtonPressed) return;

            _isPanning = true;
            _panStart = e.GetPosition(scrollViewer);
            _panOrigin = scrollViewer.Offset;
            scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(scrollViewer);
            e.Handled = true;
        }

        private void OnScrollViewerPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning || sender is not ScrollViewer scrollViewer) return;

            var current = e.GetPosition(scrollViewer);
            var delta = current - _panStart;

            var target = new Vector(
                _panOrigin.X - delta.X,
                _panOrigin.Y - delta.Y);

            var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

            scrollViewer.Offset = new Vector(
                Math.Clamp(target.X, 0, maxX),
                Math.Clamp(target.Y, 0, maxY));

            e.Handled = true;
        }

        private void OnScrollViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            if (_isPanning)
            {
                _isPanning = false;
                scrollViewer.Cursor = null;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        public async Task<Bitmap?> GetSnapshot()
        {
            var container = this.FindControl<Grid>("CanvasContainer");
            if (container == null || container.Width <= 0 || container.Height <= 0) return null;

            try
            {
                var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)container.Width, (int)container.Height), new Vector(96, 96));
                rtb.Render(container);
                return rtb;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Snapshot failed: " + ex.Message);
                return null;
            }
        }

        public async Task<string?> ShowSaveAsDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return null;

            try
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Image",
                    DefaultExtension = "png",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                        new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                        new FilePickerFileType("Bitmap Image") { Patterns = new[] { "*.bmp" } }
                    },
                    SuggestedFileName = $"ShareX_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
                });

                return file?.Path.LocalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SaveAs dialog failed: " + ex.Message);
                return null;
            }
        }

        public async Task CopyToClipboard(Bitmap image)
        {
            try
            {
                using var memoryStream = new System.IO.MemoryStream();
                image.Save(memoryStream);
                memoryStream.Position = 0;

                using var drawingImage = System.Drawing.Image.FromStream(memoryStream);
                ShareX.Ava.Platform.Abstractions.PlatformServices.Clipboard.SetImage(drawingImage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard copy failed: {ex.Message}");
                throw;
            }

            await Task.CompletedTask;
        }

        public async Task ShowErrorDialog(string title, string message)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 500,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 460
            };

            var buttonPanel = new StackPanel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Padding = new Thickness(30, 8)
            };

            okButton.Click += (s, e) => messageBox.Close();

            buttonPanel.Children.Add(okButton);
            panel.Children.Add(messageText);
            panel.Children.Add(buttonPanel);
            messageBox.Content = panel;

            await messageBox.ShowDialog(TopLevel.GetTopLevel(this) as Window);
        }

        public void PerformCrop()
        {
            // Crop operation not implemented in refactored canvas yet.
        }
    }
}
