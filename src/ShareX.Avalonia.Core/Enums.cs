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

using XerahS.Common;
using System.ComponentModel;

namespace XerahS.Core;

public enum ShareXBuild
{
    Debug,
    Release,
    Steam,
    MicrosoftStore,
    Unknown
}

public enum UpdateChannel // Localized
{
    Release,
    PreRelease,
    Dev
}

public enum SupportedLanguage
{
    Automatic, // Localized
    [Description("العربية (Arabic)")]
    Arabic,
    [Description("Nederlands (Dutch)")]
    Dutch,
    [Description("English")]
    English,
    [Description("Français (French)")]
    French,
    [Description("Deutsch (German)")]
    German,
    [Description("עִברִית (Hebrew)")]
    Hebrew,
    [Description("Magyar (Hungarian)")]
    Hungarian,
    [Description("Bahasa Indonesia (Indonesian)")]
    Indonesian,
    [Description("Italiano (Italian)")]
    Italian,
    [Description("日本語 (Japanese)")]
    Japanese,
    [Description("한국어 (Korean)")]
    Korean,
    [Description("Español mexicano (Mexican Spanish)")]
    MexicanSpanish,
    [Description("فارسی (Persian)")]
    Persian,
    [Description("Polski (Polish)")]
    Polish,
    [Description("Português (Portuguese)")]
    Portuguese,
    [Description("Português-Brasil (Portuguese-Brazil)")]
    PortugueseBrazil,
    [Description("Română (Romanian)")]
    Romanian,
    [Description("Русский (Russian)")]
    Russian,
    [Description("简体中文 (Simplified Chinese)")]
    SimplifiedChinese,
    [Description("Español (Spanish)")]
    Spanish,
    [Description("繁體中文 (Traditional Chinese)")]
    TraditionalChinese,
    [Description("Türkçe (Turkish)")]
    Turkish,
    [Description("Українська (Ukrainian)")]
    Ukrainian,
    [Description("Tiếng Việt (Vietnamese)")]
    Vietnamese
}

public enum TaskJob
{
    Job,
    DataUpload,
    FileUpload,
    TextUpload,
    ShortenURL,
    ShareURL,
    Download,
    DownloadUpload
}

public enum TaskStatus
{
    InQueue,
    Preparing,
    Working,
    Stopping,
    Stopped,
    Failed,
    Completed,
    History,
    Canceled
}

public enum CaptureType
{
    Fullscreen,
    Monitor,
    ActiveMonitor,
    Window,
    ActiveWindow,
    Region,
    CustomRegion,
    LastRegion
}

public enum ScreenRecordStartMethod
{
    Region,
    ActiveWindow,
    CustomRegion,
    LastRegion
}

public enum HotkeyType // Localized
{
    [Description("None")]
    None,
    // Upload
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Upload file")]
    FileUpload,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Upload folder")]
    FolderUpload,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Upload from clipboard")]
    ClipboardUpload,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Upload from clipboard with content viewer")]
    ClipboardUploadWithContentViewer,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Upload text")]
    UploadText,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Upload from URL")]
    UploadURL,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Drag and drop upload")]
    DragDropUpload,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Shorten URL")]
    ShortenURL,
    [Category(EnumExtensions.HotkeyType_Category_Upload)]
    [Description("Stop all active uploads")]
    StopUploads,
    // Screen capture
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture entire screen")]
    PrintScreen,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture active window")]
    ActiveWindow,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture any window")]
    CustomWindow,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture active monitor")]
    ActiveMonitor,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture region")]
    RectangleRegion,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture region (Light)")]
    RectangleLight,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture region (Transparent)")]
    RectangleTransparent,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture pre configured region")]
    CustomRegion,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Capture last region")]
    LastRegion,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Start/Stop scrolling capture")]
    ScrollingCapture,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Auto capture")]
    AutoCapture,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Start auto capture using last region")]
    StartAutoCapture,
    [Category(EnumExtensions.HotkeyType_Category_ScreenCapture)]
    [Description("Stop auto capture")]
    StopAutoCapture,
    // Screen record
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording")]
    ScreenRecorder,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording using active window region")]
    ScreenRecorderActiveWindow,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording using pre configured region")]
    ScreenRecorderCustomRegion,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording using last region")]
    StartScreenRecorder,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording (GIF)")]
    ScreenRecorderGIF,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording (GIF) using active window region")]
    ScreenRecorderGIFActiveWindow,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording (GIF) using pre configured region")]
    ScreenRecorderGIFCustomRegion,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Start/Stop screen recording (GIF) using last region")]
    StartScreenRecorderGIF,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Stop screen recording")]
    StopScreenRecording,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Pause screen recording")]
    PauseScreenRecording,
    [Category(EnumExtensions.HotkeyType_Category_ScreenRecord)]
    [Description("Abort screen recording")]
    AbortScreenRecording,
    // Tools
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Color picker")]
    ColorPicker,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Screen color picker")]
    ScreenColorPicker,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Ruler")]
    Ruler,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Pin to screen")]
    PinToScreen,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Pin to screen (From screen)")]
    PinToScreenFromScreen,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Pin to screen (From clipboard)")]
    PinToScreenFromClipboard,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Pin to screen (From file)")]
    PinToScreenFromFile,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Pin to screen (Close all)")]
    PinToScreenCloseAll,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Image editor")]
    ImageEditor,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Image beautifier")]
    ImageBeautifier,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Image effects")]
    ImageEffects,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Image viewer")]
    ImageViewer,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Image combiner")]
    ImageCombiner,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Image splitter")]
    ImageSplitter,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Image thumbnailer")]
    ImageThumbnailer,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Video converter")]
    VideoConverter,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Video thumbnailer")]
    VideoThumbnailer,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Analyze image")]
    AnalyzeImage,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("OCR")]
    OCR,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("QR code")]
    QRCode,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("QR code (Scan screen)")]
    QRCodeDecodeFromScreen,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("QR code (Scan region)")]
    QRCodeScanRegion,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Hash checker")]
    HashCheck,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Metadata")]
    Metadata,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Strip metadata")]
    StripMetadata,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Directory indexer")]
    IndexFolder,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Clipboard viewer")]
    ClipboardViewer,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Borderless window")]
    BorderlessWindow,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Make active window borderless")]
    ActiveWindowBorderless,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Make active window top most")]
    ActiveWindowTopMost,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Inspect window")]
    InspectWindow,
    [Category(EnumExtensions.HotkeyType_Category_Tools)]
    [Description("Monitor test")]
    MonitorTest,
    // Other
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Disable/Enable hotkeys")]
    DisableHotkeys,
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Open main window")]
    OpenMainWindow,
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Open screenshots folder")]
    OpenScreenshotsFolder,
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Open history window")]
    OpenHistory,
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Open image history window")]
    OpenImageHistory,
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Toggle actions toolbar")]
    ToggleActionsToolbar,
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Toggle tray menu")]
    ToggleTrayMenu,
    [Category(EnumExtensions.HotkeyType_Category_Other)]
    [Description("Exit ShareX")]
    ExitShareX
}

public enum ToastClickAction // Localized
{
    CloseNotification,
    AnnotateImage,
    CopyImageToClipboard,
    CopyFile,
    CopyFilePath,
    CopyUrl,
    OpenFile,
    OpenFolder,
    OpenUrl,
    Upload,
    PinToScreen,
    DeleteFile
}

public enum ThumbnailViewClickAction // Localized
{
    Default,
    Select,
    OpenImageViewer,
    OpenFile,
    OpenFolder,
    OpenURL,
    EditImage
}

public enum FileExistAction // Localized
{
    Ask,
    Overwrite,
    UniqueName,
    Cancel
}

public enum ImagePreviewVisibility // Localized
{
    Show, Hide, Automatic
}

public enum ImagePreviewLocation // Localized
{
    Side, Bottom
}

public enum ThumbnailTitleLocation // Localized
{
    Top, Bottom
}

public enum RegionCaptureType
{
    Default, Light, Transparent
}

public enum ScreenTearingTestMode
{
    VerticalLines,
    HorizontalLines
}

public enum StartupState
{
    Disabled,
    DisabledByUser,
    Enabled,
    DisabledByPolicy,
    EnabledByPolicy
}

public enum BalloonTipClickAction
{
    None,
    OpenURL,
    OpenDebugLog
}

public enum TaskViewMode // Localized
{
    ListView,
    ThumbnailView
}

public enum NativeMessagingAction
{
    None,
    UploadImage,
    UploadVideo,
    UploadAudio,
    UploadText,
    ShortenURL
}

public enum NotificationSound
{
    Capture,
    TaskCompleted,
    ActionCompleted,
    Error
}
