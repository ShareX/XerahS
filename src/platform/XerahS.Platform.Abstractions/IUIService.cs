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

using System.Linq;
using XerahS.Core;
using ShareX.ImageEditor.Hosting;
using SkiaSharp;
// REMOVED: System.Drawing

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Service for interacting with the main UI (e.g. navigation, showing windows)
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// Hides or minimizes the main window before capture to avoid capturing the app itself.
        /// </summary>
        Task HideMainWindowAsync();

        /// <summary>
        /// Restores the main window after capture completes (if it was visible before).
        /// </summary>
        Task RestoreMainWindowAsync();

        /// <summary>
        /// Shows the image editor with the provided image and returns the edited image.
        /// When sourceFilePath is provided, Save can overwrite the original file.
        /// When taskMode is true, the editor behaves like an in-workflow annotation step.
        /// </summary>
        Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false);

        /// <summary>
        /// Shows the image editor and returns the rendered image plus editable annotation state.
        /// </summary>
        async Task<ImageEditorSessionResult?> ShowEditorSessionAsync(
            SKBitmap image,
            string? sourceFilePath = null,
            bool taskMode = false,
            IReadOnlyList<ShareX.ImageEditor.Core.Annotations.Annotation>? annotations = null,
            bool restoredAnnotations = false)
        {
            SKBitmap? sourceImage = image.Copy();
            SKBitmap? renderedImage = await ShowEditorAsync(image, sourceFilePath, taskMode);
            if (renderedImage == null)
            {
                sourceImage.Dispose();
                return null;
            }

            var annotationSnapshot = annotations?.Select(annotation => annotation.Clone()).ToArray()
                ?? Array.Empty<ShareX.ImageEditor.Core.Annotations.Annotation>();

            return new ImageEditorSessionResult(renderedImage, sourceImage, annotationSnapshot);
        }

        Task<ImageEditorSessionResult?> ShowPrivacyEditorAsync(SKBitmap image) => Task.FromResult<ImageEditorSessionResult?>(null);

        async Task<((AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel, AfterCaptureQuickAction QuickAction) Tasks, bool PrivacyEnabled)>
            ShowAfterCaptureWindowWithPrivacyAsync(SKBitmap image, AfterCaptureTasks capture, AfterUploadTasks upload, bool privacyEnabled)
            => (await ShowAfterCaptureWindowAsync(image, capture, upload), privacyEnabled);

        /// <summary>
        /// Shows the image editor and reports whether the user cancelled the task (Esc / Cancel in task mode).
        /// </summary>
        async Task<EditorSessionOutcome> ShowEditorSessionWithOutcomeAsync(SKBitmap image, bool taskMode = false)
        {
            ImageEditorSessionResult? result = await ShowEditorSessionAsync(image, taskMode: taskMode);
            return new EditorSessionOutcome(result, Cancelled: false);
        }

        /// <summary>
        /// Shows the video editor for the given video file. Returns the exported output path
        /// if the user completed export, or null if cancelled.
        /// </summary>
        Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath);

        /// <summary>
        /// Shows the After Capture window and returns selected tasks or a terminal quick action.
        /// </summary>
        Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel, AfterCaptureQuickAction QuickAction)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload);

        /// <summary>
        /// Shows the After Upload window with upload results and actions.
        /// </summary>
        Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info);

        /// <summary>
        /// Shows the Send-to action prompt and returns the chosen action.
        /// Implementations may return a fallback upload decision when interactive UI is unavailable.
        /// </summary>
        Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection);

        /// <summary>
        /// Executes a non-upload Send-to action against the provided selection.
        /// </summary>
        Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection, SendToPromptResult? decision = null);

        /// <summary>
        /// Shows the OCR window with the provided image and runs text recognition.
        /// Used as an AfterCapture task triggered by the DoOCR flag.
        /// </summary>
        Task ShowOcrWindowAsync(SKBitmap image);

        /// <summary>
        /// Shows the image analyzer window with the provided image.
        /// Used as an AfterCapture task triggered by the AnalyzeImage flag.
        /// </summary>
        Task ShowAnalyzerWindowAsync(SKBitmap image);
    }
}
