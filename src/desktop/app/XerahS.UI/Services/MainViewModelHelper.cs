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
using XerahS.Core.Helpers;

namespace XerahS.UI.Services;

/// <summary>
/// Helper class for wiring up MainViewModel events to host application infrastructure.
/// </summary>
public static class MainViewModelHelper
{
    /// <summary>
    /// TaskSettings of the most recently completed capture. Save/Save-As read the PNG depth and
    /// HDR tone-mapping settings from here (matching what the pipeline wrote) instead of the
    /// global DefaultTaskSettings, which users do not change when editing a workflow.
    /// </summary>
    public static TaskSettings? LastCaptureTaskSettings { get; set; }

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
    /// getSource returns the working source image (may be HDR/RgbaF16) so the save path can honor
    /// the PNG depth and HDR tone-mapping settings instead of encoding the 8-bit preview.
    /// </summary>
    public static void WireSaveRequested(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot = null, Func<Window?>? getWindow = null, Func<SKBitmap?>? getSource = null)
    {
        viewModel.SaveRequested += () => HandleSaveRequestedAsync(viewModel, getEditedSnapshot, getWindow, getSource);
    }

    /// <summary>
    /// Wires up the SaveAsRequested event to show a save file dialog and save to the chosen path.
    /// getSource returns the working source image (may be HDR/RgbaF16) so the save path can honor
    /// the PNG depth and HDR tone-mapping settings instead of encoding the 8-bit preview.
    /// </summary>
    public static void WireSaveAsRequested(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot = null, Func<Window?>? getWindow = null, Func<SKBitmap?>? getSource = null)
    {
        viewModel.SaveAsRequested += () => HandleSaveAsRequestedAsync(viewModel, getEditedSnapshot, getWindow, getSource);
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

    private static async Task<string?> HandleSaveRequestedAsync(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot, Func<Window?>? getWindow, Func<SKBitmap?>? getSource)
    {
        DebugHelper.WriteLine("MainViewModelHelper: SaveRequested received");
        try
        {
            if (!string.IsNullOrEmpty(viewModel.ImageFilePath))
            {
                return await SaveToPathAsync(viewModel, getEditedSnapshot, getSource, viewModel.ImageFilePath);
            }

            return await HandleSaveAsRequestedAsync(viewModel, getEditedSnapshot, getWindow, getSource);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Editor save failed: {ex.Message}");
            DebugHelper.WriteException(ex);
            return null;
        }
    }

    private static readonly HashSet<MainViewModel> _saveAsInProgress = new();

    private static async Task<string?> HandleSaveAsRequestedAsync(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot, Func<Window?>? getWindow, Func<SKBitmap?>? getSource)
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

            return await SaveToPathAsync(viewModel, getEditedSnapshot, getSource, path);
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

    /// <summary>
    /// Runs the real Save-As content path without a file picker (used by the HDR self-test).
    /// Mirrors what the embedded editor does: the working source (fp16 for HDR captures) is the
    /// authoritative input for depth/tone-aware saving.
    /// </summary>
    public static Task<string?> SaveToPathForDiagnosticsAsync(MainViewModel viewModel, string path)
    {
        return SaveToPathAsync(viewModel, null, () => viewModel.CreateSourceImageCopyForPersistence(), path);
    }

    private static async Task<string?> SaveToPathAsync(MainViewModel viewModel, Func<SKBitmap?>? getEditedSnapshot, Func<SKBitmap?>? getSource, string path)
    {
        // The PNG depth setting decides the saved content for an HDR (fp16) source:
        // - "SDR" (Default): force an HDR→SDR conversion using the selected tone-mapping method
        //   (None = save "as is", i.e. clamp at SDR white with no tone curve).
        // - "SDR+HDR" (Bit16): keep the HDR data and write the 16-bit sRGB PNG as-is.
        // - "SDR+HDR (HDR10 PNG)" (Bit16Hdr): keep the HDR data and write a PQ-encoded 16-bit PNG
        //   with a cICP chunk, so the range above SDR white survives in the file.
        // Settings come from the workflow that produced the capture (LastCaptureTaskSettings),
        // falling back to the global defaults — NOT from global-only defaults, which diverge
        // from what the user sees in the workflow editor.
        var imageSettings = (LastCaptureTaskSettings ?? SettingsManager.DefaultTaskSettings)?.ImageSettings;
        bool sdrOnlyOutput = imageSettings?.ImagePNGBitDepth is not (PNGBitDepth.Bit16 or PNGBitDepth.Bit16Hdr);
        HdrToneMappingMode hdrSaveMode = imageSettings?.ImageHdrSaveToneMapping ?? HdrToneMappingMode.TonemapHable;
        DebugHelper.WriteLine($"MainViewModelHelper: SaveToPath depth={imageSettings?.ImagePNGBitDepth}, hdrSaveMode={hdrSaveMode}");

        SKBitmap? source = null;
        SKBitmap? tonemapped = null;
        SKBitmap? fallback = null;
        SKBitmap? bitmap = null;

        try
        {
            source = getSource?.Invoke();
            bool isHdrSource = source != null && source.ColorType == SKColorType.RgbaF16;
            SavePathDebugDump.Info("source-from-getSource", source);
            SavePathDebugDump.Dump("1_source", source);
            string sourceSig = SavePathDebugDump.Signature(source);
            DebugHelper.WriteLine($"[SavePathDebug] source Sig: {sourceSig}; last preview Sig: {SavePathDebugDump.LastPreviewSignature}");

            if (isHdrSource)
            {
                if (!sdrOnlyOutput)
                {
                    // SDR+HDR depth: keep the fp16 source; ImageHelpers.SaveBitmap below writes it
                    // as a 16-bit sRGB PNG without applying a tone curve.
                    bitmap = source;
                    SavePathDebugDump.Info("bitmap-fp16-kept", bitmap);
                }
                else
                {
                    // SDR depth: tonemap HDR→SDR. None passes the linear values through unchanged
                    // (they clamp at SDR white), every other mode applies its tone curve.
                    tonemapped = HdrTonemapper.TonemapToBgra8888(source!, hdrSaveMode);
                    SavePathDebugDump.Info("bitmap-tonemapped", tonemapped);
                    SavePathDebugDump.Dump("2_tonemapped", tonemapped);
                    if (tonemapped != null)
                    {
                        bitmap = tonemapped;
                    }
                    else
                    {
                        DebugHelper.WriteLine("MainViewModelHelper: HDR→SDR tonemap failed; falling back to edited snapshot.");
                        bitmap = fallback = GetSnapshotOrPreview();
                        SavePathDebugDump.Info("bitmap-fallback-after-tonemap-fail", bitmap);
                    }
                }
            }
            else
            {
                // SDR (or no) source: keep the WYSIWYG behavior — save the rendered snapshot,
                // which includes any annotations.
                bitmap = fallback = GetSnapshotOrPreview();
                SavePathDebugDump.Info("bitmap-snapshot-or-preview", bitmap);
                SavePathDebugDump.Dump("3_snapshot_or_preview", bitmap);
            }

            if (bitmap == null)
            {
                DebugHelper.WriteLine("MainViewModelHelper: SaveToPath — no image to save.");
                return null;
            }
            SavePathDebugDump.Dump("4_final", bitmap);

            Exception? imageSaveError = null;
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                bool pngContainer = ext is ".png" or "";
                bool jpegContainer = ext is ".jpg" or ".jpeg";

                // "SDR+HDR" depth always produces a 16-bit file, even when the working source is
                // already 8-bit (SDR desktop, GDI fallback, or an opened SDR image). Widening the
                // pixels keeps the selected depth meaningful instead of silently writing 32-bit
                // RGBA, which users reported as "the depth setting has no effect".
                // A JPEG container can only carry the HDR range as Ultra HDR, and only from an
                // fp16 source, so 8-bit input still falls through to a plain JPEG encode.
                bool writeSixteenBit = !sdrOnlyOutput && (pngContainer || (jpegContainer && isHdrSource));
                if (writeSixteenBit)
                {
                    ImageHelpers.SaveBitmap(bitmap, path, PNGBitDepth.Bit16);
                }
                else
                {
                    var format = jpegContainer ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
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
        }
        finally
        {
            // bitmap aliases one of these; dispose each owned object exactly once.
            fallback?.Dispose();
            tonemapped?.Dispose();
            source?.Dispose();
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

        SKBitmap? GetSnapshotOrPreview()
        {
            var bmp = getEditedSnapshot?.Invoke();
            if (bmp == null && viewModel.PreviewImage != null)
                bmp = BitmapConversionHelpers.ToSKBitmap(viewModel.PreviewImage);
            return bmp;
        }
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

            if (imageToUpload == null && viewModel.PreviewImage != null)
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

            // Use the platform clipboard service (set up via EditorClipboardAdapter).
            if (Platform.Abstractions.PlatformServices.IsInitialized)
            {
                Platform.Abstractions.PlatformServices.Clipboard.SetImage(imageToCopy.Copy());
                DebugHelper.WriteLine("MainViewModelHelper: Image copied to clipboard");
            }
            else
            {
                DebugHelper.WriteLine("MainViewModelHelper: Platform clipboard not initialized");
            }

            imageToCopy.Dispose();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Editor copy to clipboard failed: {ex.Message}");
            DebugHelper.WriteException(ex);
        }
    }
}
