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
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ShareX.ImageEditor.Core.Persistence;
using ShareX.ImageEditor.Presentation.Rendering;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using System.IO;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;

namespace XerahS.UI.Services;

/// <summary>
/// Helper class for wiring up MainViewModel events to host application infrastructure.
/// </summary>
public static class MainViewModelHelper
{
    /// <summary>
    /// Wires up the UploadRequested event to the XerahS upload pipeline.
    /// </summary>
    public static void WireUploadRequested(MainViewModel viewModel, IDesktopTaskManager taskManager, Func<SKBitmap?>? getEditedSnapshot = null)
    {
        viewModel.UploadRequested += () =>
        {
            _ = HandleUploadRequestedAsync(viewModel, taskManager, getEditedSnapshot);
        };
    }

    /// <summary>
    /// Wires up the CopyRequested event to copy the image to the system clipboard.
    /// When getEditedSnapshot is provided, uses the rendered image (with annotations) instead of the base preview.
    /// </summary>
    public static void WireCopyRequested(MainViewModel viewModel, Func<SkiaSharp.SKBitmap?>? getEditedSnapshot = null)
    {
        viewModel.CopyRequested += () =>
        {
            HandleCopyRequested(viewModel, getEditedSnapshot);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Wires up the SaveRequested event to overwrite the last saved path, or falls back to SaveAs.
    /// </summary>
    public static void WireSaveRequested(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot = null, Func<Window?>? getWindow = null)
    {
        viewModel.SaveRequested += () => HandleSaveRequestedAsync(viewModel, getEditedSnapshot, getWindow);
    }

    /// <summary>
    /// Wires up the SaveAsRequested event to show a save file dialog and save to the chosen path.
    /// </summary>
    public static void WireSaveAsRequested(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot = null, Func<Window?>? getWindow = null)
    {
        viewModel.SaveAsRequested += () => HandleSaveAsRequestedAsync(viewModel, getEditedSnapshot, getWindow);
    }

    /// <summary>
    /// Wires up the PinRequested event to pin the edited image to screen.
    /// When getEditedSnapshot is provided, uses the rendered image (with annotations) instead of the base preview.
    /// </summary>
    public static void WirePinRequested(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot = null)
    {
        viewModel.PinRequested += () =>
        {
            HandlePinRequested(viewModel, getEditedSnapshot);
        };
    }

    private static async Task<string?> HandleSaveRequestedAsync(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot, Func<Window?>? getWindow)
    {
        DebugHelper.WriteLine("MainViewModelHelper: SaveRequested received");
        try
        {
            if (!string.IsNullOrEmpty(viewModel.ImageFilePath))
            {
                return await SaveToPathAsync(viewModel, getEditedSnapshot, viewModel.ImageFilePath);
            }

            return await HandleSaveAsRequestedAsync(viewModel, getEditedSnapshot, getWindow);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Editor save failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return null;
        }
    }

    private static readonly HashSet<MainViewModel> _saveAsInProgress = new();

    private static async Task<string?> HandleSaveAsRequestedAsync(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot, Func<Window?>? getWindow)
    {
        // Guard against re-entry (e.g. double-click or keyboard repeat triggering the dialog twice)
        if (!_saveAsInProgress.Add(viewModel))
        {
            DebugHelper.WriteLine("MainViewModelHelper: SaveAsRequested ignored — save already in progress.");
            return null;
        }

        DebugHelper.WriteLine("MainViewModelHelper: SaveAsRequested received");
        try
        {
            var window = getWindow?.Invoke();
            var topLevel = window != null ? TopLevel.GetTopLevel(window) : null;
            if (topLevel?.StorageProvider == null)
            {
                DebugHelper.WriteLine("MainViewModelHelper: SaveAs — no storage provider available.");
                return null;
            }

            string suggestedName = string.IsNullOrEmpty(viewModel.ImageFilePath)
                ? "image.png"
                : Path.GetFileName(viewModel.ImageFilePath);

            var options = new FilePickerSaveOptions
            {
                Title = "Save Image As",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                },
                DefaultExtension = "png"
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                DebugHelper.WriteLine("MainViewModelHelper: SaveAs cancelled or path unavailable.");
                return null;
            }

            return await SaveToPathAsync(viewModel, getEditedSnapshot, path);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Editor save-as failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return null;
        }
        finally
        {
            _saveAsInProgress.Remove(viewModel);
        }
    }

    private static async Task<string?> SaveToPathAsync(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot, string path)
    {
        SKBitmap? bitmap = getEditedSnapshot?.Invoke();
        if (bitmap == null && viewModel.PreviewImage != null)
            bitmap = BitmapConversionHelpers.ToSKBitmap(viewModel.PreviewImage);

        if (bitmap == null)
        {
            DebugHelper.WriteLine("MainViewModelHelper: SaveToPath — no image to save.");
            return null;
        }

        Exception? imageSaveError = null;
        try
        {
            using (bitmap)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                var format = ext is ".jpg" or ".jpeg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
                int quality = format == SKEncodedImageFormat.Jpeg ? 95 : 100;
                using var data = bitmap.Encode(format, quality);
                using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                data.SaveTo(stream);
            }
        }
        catch (Exception ex)
        {
            imageSaveError = ex;
        }

        if (imageSaveError != null)
        {
            DebugHelper.WriteLine($"MainViewModelHelper: Image save failed (file may be incomplete): {imageSaveError.Message}");
            DebugHelper.WriteException(imageSaveError);
            viewModel.IsDirty = true;
            return null;
        }

        viewModel.ImageFilePath = path;

        try
        {
            var annotations = viewModel.GetAnnotationSnapshotForPersistence();
            using var sourceImage = viewModel.CreateSourceImageCopyForPersistence();
            if (annotations.Count > 0 && sourceImage != null)
            {
                string? sidecarPath = await XannProjectFileService.SaveAsync(path, sourceImage, annotations);
                DebugHelper.WriteLine($"MainViewModelHelper: Annotation sidecar saved to '{sidecarPath}'");
            }
            else
            {
                bool deleted = XannProjectFileService.TryDeleteSidecar(path);
                if (deleted)
                {
                    DebugHelper.WriteLine($"MainViewModelHelper: Annotation sidecar removed for '{path}'");
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            DebugHelper.WriteLine($"MainViewModelHelper: Annotation sidecar save failed (image saved successfully): {ex.Message}");
            DebugHelper.WriteException(ex);
            throw;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"MainViewModelHelper: Annotation sidecar save failed (image saved successfully): {ex.Message}");
            DebugHelper.WriteException(ex);
        }

        DebugHelper.WriteLine($"MainViewModelHelper: Image saved to '{path}'");
        viewModel.IsDirty = false;
        return path;
    }

    private static async Task HandleUploadRequestedAsync(MainViewModel viewModel, IDesktopTaskManager taskManager, Func<SKBitmap?>? getEditedSnapshot)
    {
        DebugHelper.WriteLine("MainViewModelHelper: UploadRequested received");

        try
        {
            using var editedSnapshot = getEditedSnapshot?.Invoke();
            SKBitmap? imageToUpload = editedSnapshot;

            if (imageToUpload != null)
            {
                DebugHelper.WriteLine($"MainViewModelHelper: Using edited snapshot {imageToUpload.Width}x{imageToUpload.Height} for upload");
            }

            // A failed rendered export must not silently publish the clean preview.
            if (imageToUpload == null && getEditedSnapshot == null && !viewModel.HasAnnotations && viewModel.PreviewImage != null)
            {
                imageToUpload = BitmapConversionHelpers.ToSKBitmap(viewModel.PreviewImage);
                if (imageToUpload != null)
                {
                    DebugHelper.WriteLine($"MainViewModelHelper: Using preview image {imageToUpload.Width}x{imageToUpload.Height} for upload");
                }
            }

            using var previewBitmap = ReferenceEquals(imageToUpload, editedSnapshot) ? null : imageToUpload;

            if (imageToUpload == null)
            {
                DebugHelper.WriteLine("MainViewModelHelper: UploadRequested ignored because no upload image is available.");
                return;
            }

            var taskSettings = new TaskSettings
            {
                Job = WorkflowType.None,
                AfterCaptureJob = AfterCaptureTasks.None,
                AfterUploadJob = AfterUploadTasks.CopyURLToClipboard
            };

            DebugHelper.WriteLine($"MainViewModelHelper: TaskSettings created - Job={taskSettings.Job}, AfterCapture={taskSettings.AfterCaptureJob}, AfterUpload={taskSettings.AfterUploadJob}, DestId={taskSettings.DestinationInstanceId}");

            DebugHelper.WriteLine("MainViewModelHelper: Calling TaskManager.StartImageUploadTask...");
            await taskManager.StartImageUploadTask(taskSettings, imageToUpload.Copy());
            DebugHelper.WriteLine("MainViewModelHelper: TaskManager.StartImageUploadTask completed");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Editor upload failed: {ex.Message}");
            DebugHelper.WriteException(ex);
        }
    }

    private static void HandlePinRequested(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot = null)
    {
        DebugHelper.WriteLine("MainViewModelHelper: PinRequested received");

        try
        {
            SKBitmap? imageToPin = getEditedSnapshot?.Invoke();
            if (imageToPin != null)
            {
                DebugHelper.WriteLine($"MainViewModelHelper: Using edited snapshot {imageToPin.Width}x{imageToPin.Height} for pin to screen");
            }

            if (imageToPin == null && viewModel.PreviewImage != null)
            {
                imageToPin = BitmapConversionHelpers.ToSKBitmap(viewModel.PreviewImage);
                if (imageToPin != null)
                {
                    DebugHelper.WriteLine($"MainViewModelHelper: Using preview image {imageToPin.Width}x{imageToPin.Height} for pin to screen");
                }
            }

            if (imageToPin == null)
            {
                DebugHelper.WriteLine("MainViewModelHelper: PinRequested ignored because no image available.");
                return;
            }

            var options = SettingsManager.DefaultTaskSettings?.ToolsSettings?.PinToScreenOptions
                ?? new PinToScreenOptions();

            PinToScreenManager.PinImage(imageToPin, null, options);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Editor pin to screen failed: {ex.Message}");
            DebugHelper.WriteException(ex);
        }
    }

    private static void HandleCopyRequested(MainViewModel viewModel, Func<SkiaSharp.SKBitmap?>? getEditedSnapshot = null)
    {
        DebugHelper.WriteLine("MainViewModelHelper: CopyRequested received");

        try
        {
            SkiaSharp.SKBitmap? imageToCopy = getEditedSnapshot?.Invoke();
            if (imageToCopy != null)
                DebugHelper.WriteLine($"MainViewModelHelper: Using edited snapshot {imageToCopy.Width}x{imageToCopy.Height} for clipboard");

            if (imageToCopy == null && viewModel.PreviewImage != null)
            {
                imageToCopy = BitmapConversionHelpers.ToSKBitmap(viewModel.PreviewImage);
                if (imageToCopy != null)
                    DebugHelper.WriteLine($"MainViewModelHelper: Using preview image {imageToCopy.Width}x{imageToCopy.Height} for clipboard");
            }

            if (imageToCopy == null)
            {
                DebugHelper.WriteLine("MainViewModelHelper: CopyRequested ignored because no image available.");
                return;
            }

            using var ownedImage = imageToCopy;

            // Use the platform clipboard service (set up via EditorClipboardAdapter).
            if (Platform.Abstractions.PlatformServices.IsInitialized)
            {
                Platform.Abstractions.PlatformServices.Clipboard.SetImage(ownedImage);
                DebugHelper.WriteLine("MainViewModelHelper: Image copied to clipboard");
            }
            else
            {
                DebugHelper.WriteLine("MainViewModelHelper: Platform clipboard not initialized");
            }

        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Editor copy to clipboard failed: {ex.Message}");
            DebugHelper.WriteException(ex);
        }
    }
}
