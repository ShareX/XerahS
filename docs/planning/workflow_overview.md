# Workflow Overview

This document outlines the planned structure and makeup of workflows in XerahS. A "Workflow" essentially corresponds to a configured `TaskSettings` object which defines the behavior of a capture or upload job.

## Workflow Makeup

The core of a workflow is the `TaskSettings` class. It orchestrates the entire pipeline from initiation (Hotkey/Job) to final output (After Upload).

### Core Components
- **Job**: The entry point or trigger type (e.g., Capture Region, Upload File).
- **Image Effects**: A pipeline of effects to apply to image content during the "After Capture" phase.
- **After Capture Tasks**: A bitmask of actions to perform immediately after content is captured or selected.
- **Destinations**: Configuration for where different types of content (Image, Text, File) should be sent.
- **After Upload Tasks**: A bitmask of actions to perform after a file/content has been successfully uploaded.

## 1. After Capture Tasks

`AfterCaptureTasks` are executed sequentially (where applicable) once the capture data is available.

**Configuration:** `TaskSettings.AfterCaptureJob` (Enum Flags)

**Available Tasks:**
1.  **ShowQuickTaskMenu**: Show a menu to select further actions.
2.  **ShowAfterCaptureWindow**: Show a dedicated post-capture window.
3.  **BeautifyImage**: Apply beautification settings (e.g., background, padding).
4.  **AddImageEffects**: Apply configured `ImageEffectPresets`. **(See Image Effects Configuration)**
5.  **AnnotateImage**: Open the image in the annotation editor.
6.  **CopyImageToClipboard**: Copy the image data to the system clipboard.
7.  **PinToScreen**: Pin the captured image to the screen as an overlay.
8.  **SendImageToPrinter**: Send the image to the default printer.
9.  **SaveImageToFile**: Save the image to the local filesystem (automatically configured path).
10. **SaveImageToFileWithDialog**: Open a "Save As" dialog for the image.
11. **SaveThumbnailImageToFile**: Save a generated thumbnail to a file.
12. **PerformActions**: Run external programs or custom actions.
13. **CopyFileToClipboard**: Copy the file object (not path or image data) to clipboard.
14. **CopyFilePathToClipboard**: Copy the absolute file path text to clipboard.
15. **ShowInExplorer**: Open the file location in the file explorer.
16. **AnalyzeImage**: Perform image analysis tasks.
17. **ScanQRCode**: Check the image for QR codes and decode them.
18. **DoOCR**: Perform Optical Character Recognition on the image.
19. **ShowBeforeUploadWindow**: Show a confirmation/metadata window before uploading.
20. **UploadImageToHost**: Upload the image to the configured destination.
21. **DeleteFile**: Delete the local file (usually after upload).

## 2. After Upload Tasks

`AfterUploadTasks` tasks runs after `UploadImageToHost` (or other upload jobs) completes successfully.

**Configuration:** `TaskSettings.AfterUploadJob` (Enum Flags)

**Available Tasks:**
1.  **ShowAfterUploadWindow**: Show a summary window of the upload result.
2.  **UseURLShortener**: Shorten the resulting URL using the configured shortener.
3.  **ShareURL**: Share the URL (or shortened URL) via configured sharing service (e.g., Email).
4.  **CopyURLToClipboard**: Copy the final URL to the clipboard.
5.  **OpenURL**: Open the final URL in the default web browser.
6.  **ShowQRCode**: Display a QR code of the final URL.

## 3. Destinations

Destinations determine where content is sent during the upload phase.

**Configuration:**
- **ImageDestination**: Service for image uploads (e.g., Imgur, S3).
- **TextDestination**: Service for text uploads (e.g., Pastebin).
- **FileDestination**: Service for generic file uploads (e.g., Dropbox, FTP).
- **URLShortenerDestination**: Service for shortening URLs (e.g., Bitly).
- **URLSharingServiceDestination**: Service for sharing URLs.

Users can choose specific services or use "Custom Uploaders". Configuration allows overriding these destinations per-workflow (TaskSetting).

## 4. Hotkeys

Hotkeys trigger specific `TaskSettings` configurations (Workflows).

**Enum:** `HotkeyType`

**Key Categories:**
- **Capture**: `PrintScreen`, `ActiveWindow`, `RectangleRegion`, `CustomRegion`, `ScreenRecorder` (Video/GIF), `AutoCapture`.
- **Upload**: `FileUpload`, `ClipboardUpload`, `UploadText`, `ShortenURL`.
- **Tools**: `ColorPicker`, `Ruler`, `ImageEditor`, `QRCode`, `PinToScreen`.

Each Hotkey can be associated with a specific `TaskSettings` profile, or use the "Application Settings" default profile.

## 5. Image Effects Configuration

Image Effects are applied during the `AfterCaptureTasks.AddImageEffects` step.

**Constraint:** This step must occur **after** capture but **before** `SaveImageToFile` or `UploadImageToHost` to ensure the modified image is the one being saved/uploaded.

**Structure:**
- **ImageEffectPresets**: A list of presets configured in `TaskSettings.ImageSettings`.
- **Pipeline**: Effects are applied in the order they appear in the preset list.
- **Editor**: An "Image Effects" editor (UI) allows users to construct these pipelines (add borders, shadows, watermarks, resizing, filters).

**Planned Configuration Flow:**
1.  User creates an "Image Effect" profile.
2.  User adds effects (e.g., "Canvas: Margin", "Filter: Blur", "Watermark").
3.  User selects this profile in `TaskSettings`.
4.  When `AfterCaptureTasks` runs, if `AddImageEffects` is checked, the selected profile is applied to the in-memory image.
