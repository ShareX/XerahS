# After Upload Window UI/Workflow Notes (2026-01-28)

Target view: `src\XerahS.UI\Views\AfterUploadWindow.axaml`

## Workflow integration summary
- Window is shown only when upload succeeds and a non-empty URL is available.
- Workflow flag: `AfterUploadTasks.ShowAfterUploadWindow` (per workflow task settings).
- Trigger happens after upload completion (and after URL shortener step when implemented).
- Invocation is non-blocking: UI opens on the dispatcher while the pipeline continues.

## UI behavior summary
- Two-pane layout with clipboard formats on the left and preview on the right.
- Primary URL is surfaced at the top, with uploader host and auto-close status.
- Format list groups: Primary URL, Embeds, Management, Local, Custom (when configured).
- Empty or duplicate format values are filtered out to keep the list concise.
- Selected format preview is shown in a read-only field for quick copy.
- Preview gracefully falls back when a local image is not available.

## Action coverage
- Copy image (enabled only when local image file exists).
- Copy selected format (defaults to first available format).
- Open URL, open file, open folder (enabled based on availability).
- Close window and optional auto-close timer (60 seconds when enabled).

## Data contract passed to UI
- URL, shortened URL, thumbnail URL, deletion URL.
- File path and file name (optional).
- Data type string and uploader host for display.
- Workflow-specific clipboard/open URL format strings.
- Auto-close flag and optional preview bitmap.
