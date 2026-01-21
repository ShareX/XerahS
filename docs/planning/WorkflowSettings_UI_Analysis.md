# WorkflowSettings (TaskSettings) UI Analysis

**Date:** 2026-01-08
**Target Class:** `XerahS.Core.TaskSettings` (referred to as WorkflowSettings)
**UI View:** `XerahS.UI.Views.TaskSettingsPanel`

**Note:** The class `WorkflowSettings` was not found. `TaskSettings` is the core configuration class for tasks in ShareX and has been analyzed as the equivalent.

## Summary
A significant portion of `TaskSettings` is bound to the UI, specifically General, Capture, Image, and Upload settings. However, `Tools` and `Advanced` settings are currently placeholders in the UI, and several granular options within the implemented tabs are missing bindings.

---

## 1. TaskSettings (Root)

| Property | Type | Wired to UI? | Notes |
| :--- | :--- | :---: | :--- |
| `Description` | `string` | ❌ No | Used in `ToString()` but not editable in TaskSettingsPanel. |
| `Job` | `HotkeyType` | ❌ No | |
| `AfterCaptureJob` | `AfterCaptureTasks` | ✅ Yes | Bitmask flags bound to checkboxes (SaveImage, CopyImage, UploadImage, Annotate, ShowWindow). |
| `AfterUploadJob` | `AfterUploadTasks` | ✅ Yes | Bitmask flags bound to checkboxes (CopyURL, ShortenURL, ShareURL). |
| `ImageDestination` | `ImageDestination` | ❌ No | |
| `ImageFileDestination` | `FileDestination` | ❌ No | |
| `TextDestination` | `TextDestination` | ❌ No | |
| `TextFileDestination` | `FileDestination` | ❌ No | |
| `FileDestination` | `FileDestination` | ❌ No | |
| `URLShortenerDestination` | `UrlShortenerType` | ❌ No | |
| `URLSharingServiceDestination` | `URLSharingServices` | ❌ No | |
| `OverrideFTP` | `bool` | ❌ No | |
| `FTPIndex` | `int` | ❌ No | |
| `OverrideCustomUploader` | `bool` | ❌ No | |
| `CustomUploaderIndex` | `int` | ❌ No | |
| `OverrideScreenshotsFolder` | `bool` | ❌ No | |
| `ScreenshotsFolder` | `string` | ❌ No | |
| `WatchFolderEnabled` | `bool` | ❌ No | |
| `WatchFolderList` | `List<WatchFolderSettings>` | ❌ No | |
| `ExternalPrograms` | `List<ExternalProgram>` | ❌ No | "Actions" tab is a placeholder. |
| `GeneralSettings` | `TaskSettingsGeneral` | ⚠️ Partial | See Section 2. |
| `ImageSettings` | `TaskSettingsImage` | ⚠️ Partial | See Section 3. |
| `CaptureSettings` | `TaskSettingsCapture` | ⚠️ Partial | See Section 4. |
| `UploadSettings` | `TaskSettingsUpload` | ⚠️ Partial | See Section 5. |
| `ToolsSettings` | `TaskSettingsTools` | ❌ No | "Tools" tab is a placeholder. |
| `AdvancedSettings` | `TaskSettingsAdvanced` | ❌ No | "Advanced" tab is a placeholder. |

---

## 2. TaskSettingsGeneral (General Tab)

| Property | Wired to UI? | Notes |
| :--- | :---: | :--- |
| `PlaySoundAfterCapture` | ✅ Yes | Checkbox |
| `PlaySoundAfterUpload` | ✅ Yes | Checkbox |
| `PlaySoundAfterAction` | ✅ Yes | Checkbox |
| `ShowToastNotificationAfterTaskCompleted` | ✅ Yes | Checkbox (Bound as `ShowToastNotification`) |
| `ToastWindowDuration` | ✅ Yes | NumericUpDown |
| `ToastWindowFadeDuration` | ✅ Yes | NumericUpDown |
| `ToastWindowPlacement` | ❌ No | |
| `ToastWindowSize` | ❌ No | |
| `ToastWindowLeftClickAction` | ❌ No | |
| `ToastWindowRightClickAction` | ❌ No | |
| `ToastWindowMiddleClickAction` | ❌ No | |
| `ToastWindowAutoHide` | ❌ No | |
| `DisableNotificationsOnFullscreen` | ❌ No | |
| `UseCustomCaptureSound` | ✅ Yes | Checkbox |
| `CustomCaptureSoundPath` | ✅ Yes | TextBox |
| `UseCustomTaskCompletedSound` | ❌ No | |
| `CustomTaskCompletedSoundPath` | ❌ No | |
| `UseCustomActionCompletedSound` | ❌ No | |
| `CustomActionCompletedSoundPath` | ❌ No | |
| `UseCustomErrorSound` | ❌ No | |
| `CustomErrorSoundPath` | ❌ No | |

---

## 3. TaskSettingsImage (Image Tab)

| Property | Wired to UI? | Notes |
| :--- | :---: | :--- |
| `ImageFormat` | ✅ Yes | ComboBox |
| `ImagePNGBitDepth` | ❌ No | |
| `ImageJPEGQuality` | ✅ Yes | NumericUpDown |
| `ImageGIFQuality` | ❌ No | |
| `ImageAutoUseJPEG` | ❌ No | |
| `ImageAutoUseJPEGSize` | ❌ No | |
| `ImageAutoJPEGQuality` | ❌ No | |
| `FileExistAction` | ❌ No | |
| `ThumbnailWidth` | ✅ Yes | NumericUpDown |
| `ThumbnailHeight` | ✅ Yes | NumericUpDown |
| `ThumbnailName` | ✅ Yes | TextBox |
| `ThumbnailCheckSize` | ✅ Yes | Checkbox |
| `ImageEffectPresets` | ✅ Yes | ListBox (via ImageEffectsViewModel) |
| `SelectedImageEffectPreset` | ✅ Yes | SelectedItem |
| `ShowImageEffectsWindowAfterCapture` | ❌ No | |
| `ImageEffectOnlyRegionCapture` | ❌ No | |
| `UseRandomImageEffect` | ❌ No | |

---

## 4. TaskSettingsCapture (Capture Tab)

| Property | Wired to UI? | Notes |
| :--- | :---: | :--- |
| `UseModernCapture` | ✅ Yes | Checkbox |
| `ShowCursor` | ✅ Yes | Checkbox |
| `ScreenshotDelay` | ✅ Yes | NumericUpDown |
| `CaptureTransparent` | ✅ Yes | Checkbox |
| `CaptureShadow` | ✅ Yes | Checkbox |
| `CaptureShadowOffset` | ❌ No | |
| `CaptureClientArea` | ✅ Yes | Checkbox |
| `CaptureAutoHideTaskbar` | ✅ Yes | Checkbox |
| `CaptureAutoHideDesktopIcons` | ❌ No | |
| `CaptureCustomRegion` | ❌ No | |
| `CaptureCustomWindow` | ✅ Yes | TextBox |
| `ScreenRecordFPS` | ✅ Yes | NumericUpDown |
| `GIFFPS` | ❌ No | |
| `ScreenRecordShowCursor` | ❌ No | |
| `ScreenRecordAutoStart` | ❌ No | |
| `ScreenRecordStartDelay` | ✅ Yes | NumericUpDown |
| `ScreenRecordFixedDuration` | ❌ No | |
| `ScreenRecordDuration` | ✅ Yes | NumericUpDown |
| `ScreenRecordTwoPassEncoding` | ❌ No | |
| `ScreenRecordAskConfirmationOnAbort` | ❌ No | |
| `ScreenRecordTransparentRegion` | ❌ No | |
| `RegionCaptureOptions` | ❌ No | Complex object |
| `FFmpegOptions` | ❌ No | Complex object |
| `ScrollingCaptureOptions` | ❌ No | Complex object |
| `OCROptions` | ❌ No | Complex object |

---

## 5. TaskSettingsUpload (Upload Tab)

| Property | Wired to UI? | Notes |
| :--- | :---: | :--- |
| `UseCustomTimeZone` | ❌ No | |
| `CustomTimeZone` | ❌ No | |
| `NameFormatPattern` | ✅ Yes | TextBox |
| `NameFormatPatternActiveWindow` | ✅ Yes | TextBox |
| `FileUploadUseNamePattern` | ✅ Yes | Checkbox |
| `FileUploadReplaceProblematicCharacters` | ✅ Yes | Checkbox |
| `URLRegexReplace` | ✅ Yes | Checkbox |
| `URLRegexReplacePattern` | ✅ Yes | TextBox |
| `URLRegexReplaceReplacement` | ✅ Yes | TextBox |
| `ClipboardUploadURLContents` | ✅ Yes | Checkbox |
| `ClipboardUploadShortenURL` | ✅ Yes | Checkbox |
| `ClipboardUploadShareURL` | ❌ No | |
| `ClipboardUploadAutoIndexFolder` | ❌ No | |
| `UploaderFilters` | ❌ No | List |

---

## 6. TaskSettingsTools & TaskSettingsAdvanced (Tools & Advanced Tabs)

All properties in these sections are currently **Not Wired**. The tabs contain placeholder text.

### TaskSettingsTools Unbound Properties:
*   `ScreenColorPickerFormat`, `ScreenColorPickerFormatCtrl`, `ScreenColorPickerInfoText`
*   `PinToScreenOptions`, `IndexerSettings`, `ImageBeautifierOptions`, `ImageCombinerOptions`, `VideoConverterOptions`, `VideoThumbnailOptions`, `BorderlessWindowSettings`, `AIOptions`

### TaskSettingsAdvanced Unbound Properties:
*   `ProcessImagesDuringFileUpload`, `ProcessImagesDuringClipboardUpload`, `ProcessImagesDuringExtensionUpload`
*   `UseAfterCaptureTasksDuringFileUpload`, `TextTaskSaveAsFile`, `AutoClearClipboard`
*   `RegionCaptureDisableAnnotation`
*   `ImageExtensions`, `TextExtensions`
*   `EarlyCopyURL`
*   `TextFileExtension`, `TextFormat`, `TextCustom`, `TextCustomEncodeInput`
*   `ResultForceHTTPS`, `ClipboardContentFormat`, `BalloonTipContentFormat`, `OpenURLFormat`
*   `AutoShortenURLLength`, `AutoCloseAfterUploadForm`
*   `NamePatternMaxLength`, `NamePatternMaxTitleLength`
