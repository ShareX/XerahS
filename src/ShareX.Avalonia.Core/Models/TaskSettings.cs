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

using Newtonsoft.Json;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.ScreenCapture.ScreenRecording;
using System.ComponentModel;
using System.Drawing;

namespace XerahS.Core;

/// <summary>
/// Main task settings configuration class
/// </summary>
public class TaskSettings
{
    [JsonIgnore]
    public TaskSettings? TaskSettingsReference { get; private set; }

    [JsonIgnore]
    public bool IsSafeTaskSettings => TaskSettingsReference != null;

    /// <summary>
    /// The ID of the workflow that this task settings belongs to
    /// </summary>
    [JsonIgnore]
    public string? WorkflowId { get; set; }

    public string Description = "";

    public HotkeyType Job = HotkeyType.None;

    public AfterCaptureTasks AfterCaptureJob = AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.SaveImageToFile;

    public AfterUploadTasks AfterUploadJob = AfterUploadTasks.CopyURLToClipboard;

    public ImageDestination ImageDestination = ImageDestination.Imgur;
    public FileDestination ImageFileDestination = FileDestination.Dropbox;
    public TextDestination TextDestination = TextDestination.Pastebin;
    public FileDestination TextFileDestination = FileDestination.Dropbox;
    public FileDestination FileDestination = FileDestination.Dropbox;
    public UrlShortenerType URLShortenerDestination = UrlShortenerType.BITLY;
    public URLSharingServices URLSharingServiceDestination = URLSharingServices.Email;

    public bool OverrideFTP = false;
    public int FTPIndex = 0;

    public bool OverrideCustomUploader = false;
    public int CustomUploaderIndex = 0;

    public bool OverrideScreenshotsFolder = false;
    public string ScreenshotsFolder = "";

    public TaskSettingsGeneral GeneralSettings = new TaskSettingsGeneral();

    public TaskSettingsImage ImageSettings = new TaskSettingsImage();

    public TaskSettingsCapture CaptureSettings = new TaskSettingsCapture();

    public TaskSettingsUpload UploadSettings = new TaskSettingsUpload();

    public List<ExternalProgram> ExternalPrograms = new List<ExternalProgram>();

    public TaskSettingsTools ToolsSettings = new TaskSettingsTools();

    public TaskSettingsAdvanced AdvancedSettings = new TaskSettingsAdvanced();

    public bool WatchFolderEnabled = false;
    public List<WatchFolderSettings> WatchFolderList = new List<WatchFolderSettings>();

    public override string ToString()
    {
        return !string.IsNullOrEmpty(Description) ? Description : EnumExtensions.GetDescription(Job);
    }

    public FileDestination GetFileDestinationByDataType(EDataType dataType)
    {
        return dataType switch
        {
            EDataType.Image => ImageFileDestination,
            EDataType.Text => TextFileDestination,
            _ => FileDestination,
        };
    }
}

/// <summary>
/// General notification and sound settings
/// </summary>
public class TaskSettingsGeneral
{
    #region General / Notifications

    public bool PlaySoundAfterCapture = true;
    public bool PlaySoundAfterUpload = true;
    public bool PlaySoundAfterAction = true;
    public bool ShowToastNotificationAfterTaskCompleted = true;
    public float ToastWindowDuration = 3f;
    public float ToastWindowFadeDuration = 1f;
    public ContentAlignment ToastWindowPlacement = ContentAlignment.BottomRight;
    public Size ToastWindowSize = new Size(400, 300);
    public ToastClickAction ToastWindowLeftClickAction = ToastClickAction.OpenUrl;
    public ToastClickAction ToastWindowRightClickAction = ToastClickAction.CloseNotification;
    public ToastClickAction ToastWindowMiddleClickAction = ToastClickAction.AnnotateImage;
    public bool ToastWindowAutoHide = true;
    public bool DisableNotificationsOnFullscreen = false;
    public bool UseCustomCaptureSound = false;
    public string CustomCaptureSoundPath = "";
    public bool UseCustomTaskCompletedSound = false;
    public string CustomTaskCompletedSoundPath = "";
    public bool UseCustomActionCompletedSound = false;
    public string CustomActionCompletedSoundPath = "";
    public bool UseCustomErrorSound = false;
    public string CustomErrorSoundPath = "";

    #endregion
}

/// <summary>
/// Image format and quality settings
/// </summary>
public class TaskSettingsImage
{
    #region Image / General

    public EImageFormat ImageFormat = EImageFormat.PNG;
    public PNGBitDepth ImagePNGBitDepth = PNGBitDepth.Default;
    public int ImageJPEGQuality = 90;
    public GIFQuality ImageGIFQuality = GIFQuality.Default;
    public bool ImageAutoUseJPEG = true;
    public int ImageAutoUseJPEGSize = 2048;
    public bool ImageAutoJPEGQuality = false;
    public FileExistAction FileExistAction = FileExistAction.Ask;

    #endregion Image / General

    #region Image / Thumbnail

    public int ThumbnailWidth = 200;
    public int ThumbnailHeight = 0;
    public string ThumbnailName = "-thumbnail";
    public bool ThumbnailCheckSize = false;

    #endregion Image / Thumbnail

    #region Image / Effects

    public List<ImageEffectPreset> ImageEffectPresets = new List<ImageEffectPreset>() { ImageEffectPreset.GetDefaultPreset() };
    public int SelectedImageEffectPreset = 0;
    public bool ShowImageEffectsWindowAfterCapture = false;
    public bool ImageEffectOnlyRegionCapture = false;
    public bool UseRandomImageEffect = false;

    #endregion Image / Effects
}

/// <summary>
/// Capture and screen recording settings
/// </summary>
public class TaskSettingsCapture
{
    #region Capture / General

    [Category("Capture"), DefaultValue(true), Description("Use modern screen capture (Direct3D11) if available.")]
    public bool UseModernCapture { get; set; } = true;

    public bool ShowCursor = true;
    public decimal ScreenshotDelay = 0;
    public bool CaptureTransparent = false;
    public bool CaptureShadow = true;
    public int CaptureShadowOffset = 100;
    public bool CaptureClientArea = false;
    public bool CaptureAutoHideTaskbar = false;
    public bool CaptureAutoHideDesktopIcons = false;
    public Rectangle CaptureCustomRegion = new Rectangle(0, 0, 0, 0);
    public string CaptureCustomWindow = "";

    #endregion Capture / General

    #region Capture / Screen recorder

    public int ScreenRecordFPS = 30;
    public int GIFFPS = 15;
    public bool ScreenRecordShowCursor = true;
    public bool ScreenRecordAutoStart = true;
    public float ScreenRecordStartDelay = 0f;
    public bool ScreenRecordFixedDuration = false;
    public float ScreenRecordDuration = 3f;
    public bool ScreenRecordTwoPassEncoding = false;
    public bool ScreenRecordAskConfirmationOnAbort = false;
    public bool ScreenRecordTransparentRegion = false;

    #endregion Capture / Screen recorder

    public RegionCaptureOptions RegionCaptureOptions = new RegionCaptureOptions();
    public FFmpegOptions FFmpegOptions { get; set; } = new FFmpegOptions();
    public ScreenRecordingSettings ScreenRecordingSettings = new ScreenRecordingSettings();
    public ScrollingCaptureOptions ScrollingCaptureOptions = new ScrollingCaptureOptions();
    public OCROptions OCROptions = new OCROptions();
}

/// <summary>
/// Upload and file naming settings
/// </summary>
public class TaskSettingsUpload
{
    #region Upload / File naming

    public bool UseCustomTimeZone = false;
    public TimeZoneInfo CustomTimeZone = TimeZoneInfo.Utc;
    public string NameFormatPattern = "%y%mo%dT%h%mi_%ra{10}";
    public string NameFormatPatternActiveWindow = "%y%mo%dT%h%mi_%pn_%ra{10}";
    public bool FileUploadUseNamePattern = false;
    public bool FileUploadReplaceProblematicCharacters = false;
    public bool URLRegexReplace = false;
    public string URLRegexReplacePattern = "^https?://(.+)$";
    public string URLRegexReplaceReplacement = "https://$1";

    #endregion Upload / File naming

    #region Upload / Clipboard upload

    public bool ClipboardUploadURLContents = false;
    public bool ClipboardUploadShortenURL = false;
    public bool ClipboardUploadShareURL = false;
    public bool ClipboardUploadAutoIndexFolder = false;

    #endregion Upload / Clipboard upload

    #region Upload / Uploader filters

    public List<UploaderFilter> UploaderFilters = new List<UploaderFilter>();

    #endregion Upload / Uploader filters
}

/// <summary>
/// Tools settings (color picker, indexer, etc.)
/// </summary>
public class TaskSettingsTools
{
    public string ScreenColorPickerFormat = "$hex";
    public string ScreenColorPickerFormatCtrl = "$r255, $g255, $b255";
    public string ScreenColorPickerInfoText = "RGB: $r255, $g255, $b255$nHex: $hex$nX: $x Y: $y";

    public PinToScreenOptions PinToScreenOptions = new PinToScreenOptions();
    public IndexerSettings IndexerSettings = new IndexerSettings();
    public ImageBeautifierOptions ImageBeautifierOptions = new ImageBeautifierOptions();
    public ImageCombinerOptions ImageCombinerOptions = new ImageCombinerOptions();
    public VideoConverterOptions VideoConverterOptions = new VideoConverterOptions();
    public VideoThumbnailOptions VideoThumbnailOptions = new VideoThumbnailOptions();
    public BorderlessWindowSettings BorderlessWindowSettings = new BorderlessWindowSettings();
    public AIOptions AIOptions = new AIOptions();
}

/// <summary>
/// Advanced settings with property attributes
/// </summary>
public class TaskSettingsAdvanced
{
    [Category("General"), DefaultValue(false), Description("Allow after capture tasks for image files.")]
    public bool ProcessImagesDuringFileUpload { get; set; }

    [Category("General"), DefaultValue(false), Description("Use after capture tasks for clipboard image uploads.")]
    public bool ProcessImagesDuringClipboardUpload { get; set; }

    [Category("General"), DefaultValue(false), Description("Use after capture tasks for browser extension image uploads.")]
    public bool ProcessImagesDuringExtensionUpload { get; set; }

    [Category("General"), DefaultValue(true), Description("Allows file related after capture tasks.")]
    public bool UseAfterCaptureTasksDuringFileUpload { get; set; }

    [Category("General"), DefaultValue(true), Description("Save text as file for text upload tasks.")]
    public bool TextTaskSaveAsFile { get; set; }

    [Category("General"), DefaultValue(false), Description("Clear clipboard when upload task starts.")]
    public bool AutoClearClipboard { get; set; }

    [Category("Capture"), DefaultValue(false), Description("Disable annotation support in region capture.")]
    public bool RegionCaptureDisableAnnotation { get; set; }

    [Category("Upload"), Description("File extensions for image uploader.")]
    public List<string> ImageExtensions { get; set; } = new();

    [Category("Upload"), Description("File extensions for text uploader.")]
    public List<string> TextExtensions { get; set; } = new();

    [Category("Upload"), DefaultValue(false), Description("Copy URL before starting upload.")]
    public bool EarlyCopyURL { get; set; }

    [Category("Upload text"), DefaultValue("txt"), Description("File extension for text files.")]
    public string TextFileExtension { get; set; } = "txt";

    [Category("Upload text"), DefaultValue("text"), Description("Text format.")]
    public string TextFormat { get; set; } = "text";

    [Category("Upload text"), DefaultValue(""), Description("Custom text input.")]
    public string TextCustom { get; set; } = "";

    [Category("Upload text"), DefaultValue(true), Description("HTML encode custom text input.")]
    public bool TextCustomEncodeInput { get; set; }

    [Category("After upload"), DefaultValue(false), Description("Force HTTPS in result URL.")]
    public bool ResultForceHTTPS { get; set; }

    [Category("After upload"), DefaultValue("$result"), Description("Clipboard format after upload.")]
    public string ClipboardContentFormat { get; set; } = "$result";

    [Category("After upload"), DefaultValue("$result"), Description("Balloon tip format after upload.")]
    public string BalloonTipContentFormat { get; set; } = "$result";

    [Category("After upload"), DefaultValue("$result"), Description("Open URL format after upload.")]
    public string OpenURLFormat { get; set; } = "$result";

    [Category("After upload"), DefaultValue(0), Description("Auto shorten URL if longer than N characters.")]
    public int AutoShortenURLLength { get; set; }

    [Category("After upload"), DefaultValue(false), Description("Auto close after upload form.")]
    public bool AutoCloseAfterUploadForm { get; set; }

    [Category("Name pattern"), DefaultValue(100), Description("Max name pattern length.")]
    public int NamePatternMaxLength { get; set; }

    [Category("Name pattern"), DefaultValue(50), Description("Max title length in name pattern.")]
    public int NamePatternMaxTitleLength { get; set; }

    public TaskSettingsAdvanced()
    {
        this.ApplyDefaultPropertyValues();
        ImageExtensions = FileHelpers.ImageFileExtensions.ToList();
        TextExtensions = FileHelpers.TextFileExtensions.ToList();
    }
}

/// <summary>
/// Watch folder configuration
/// </summary>
public class WatchFolderSettings
{
    public string FolderPath { get; set; } = "";
    public string Filter { get; set; } = "*.*";
    public bool IncludeSubdirectories { get; set; } = false;
    public bool MoveFilesToScreenshotsFolder { get; set; } = false;
}
