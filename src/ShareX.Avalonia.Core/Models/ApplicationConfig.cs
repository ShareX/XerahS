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

using ShareX.Ava.Common;
using ShareX.Ava.Uploaders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace ShareX.Ava.Core;

/// <summary>
/// Application-wide configuration settings
/// </summary>
public class ApplicationConfig : SettingsBase<ApplicationConfig>
{
    public TaskSettings DefaultTaskSettings = new TaskSettings();

    public DateTime FirstTimeRunDate = DateTime.Now;
    public string FileUploadDefaultDirectory = "";
    public int NameParserAutoIncrementNumber = 0;
    public List<QuickTaskInfo> QuickTaskPresets = QuickTaskInfo.DefaultPresets;

    // Main window
    public bool FirstTimeMinimizeToTray = true;
    public List<int> TaskListViewColumnWidths = new List<int>();
    public int PreviewSplitterDistance = 335;

    public ApplicationConfig()
    {
        this.ApplyDefaultPropertyValues();
    }

    #region Settings - General

    public SupportedLanguage Language = SupportedLanguage.Automatic;
    public bool ShowTray = true;
    public bool SilentRun = false;
    public bool TrayIconProgressEnabled = true;
    public bool TaskbarProgressEnabled = true;
    public bool UseWhiteShareXIcon = false;
    public bool RememberMainFormPosition = false;
    public System.Drawing.Point MainFormPosition = System.Drawing.Point.Empty;
    public bool RememberMainFormSize = false;
    public Size MainFormSize = Size.Empty;

    public HotkeyType TrayLeftClickAction = HotkeyType.RectangleRegion;
    public HotkeyType TrayLeftDoubleClickAction = HotkeyType.OpenMainWindow;
    public HotkeyType TrayMiddleClickAction = HotkeyType.ClipboardUploadWithContentViewer;

    public bool AutoCheckUpdate = true;
    public UpdateChannel UpdateChannel = UpdateChannel.Release;
    public bool CheckPreReleaseUpdates = false;

    #endregion Settings - General

    #region Settings - Theme

    public List<ShareXTheme> Themes = ShareXTheme.GetDefaultThemes();
    public int SelectedTheme = 0;

    #endregion Settings - Theme

    #region Settings - Paths

    public bool UseCustomScreenshotsPath = false;
    public string CustomScreenshotsPath = "";
    public string SaveImageSubFolderPattern = "%y-%mo";
    public string SaveImageSubFolderPatternWindow = "";

    #endregion Settings - Paths

    #region Settings - Main window

    public bool ShowMenu = true;
    public TaskViewMode TaskViewMode = TaskViewMode.ThumbnailView;

    // Thumbnail view
    public bool ShowThumbnailTitle = true;
    public ThumbnailTitleLocation ThumbnailTitleLocation = ThumbnailTitleLocation.Top;
    public Size ThumbnailSize = new Size(200, 150);
    public ThumbnailViewClickAction ThumbnailClickAction = ThumbnailViewClickAction.Default;

    // List view
    public bool ShowColumns = true;
    public ImagePreviewVisibility ImagePreview = ImagePreviewVisibility.Automatic;
    public ImagePreviewLocation ImagePreviewLocation = ImagePreviewLocation.Side;

    #endregion Settings - Main window

    #region Settings - Cleanup

    public bool AutoCleanupBackupFiles = false;
    public bool AutoCleanupLogFiles = false;
    public int CleanupKeepFileCount = 10;

    #endregion Settings - Cleanup

    #region Settings - Proxy

    public ProxyInfo ProxySettings = new ProxyInfo();

    #endregion Settings - Proxy

    #region Settings - Upload

    public int UploadLimit = 0;
    public int BufferSizePower = 5;
    public List<ClipboardFormat> ClipboardContentFormats = new List<ClipboardFormat>();

    public int MaxUploadFailRetry = 1;
    public bool UseSecondaryUploaders = false;
    public List<ImageDestination> SecondaryImageUploaders = new List<ImageDestination>();
    public List<TextDestination> SecondaryTextUploaders = new List<TextDestination>();
    public List<FileDestination> SecondaryFileUploaders = new List<FileDestination>();

    #endregion Settings - Upload

    #region Settings - History

    public bool HistorySaveTasks = true;
    public bool HistoryCheckURL = true;

    public RecentTask[]? RecentTasks = null;
    public bool RecentTasksSave = false;
    public int RecentTasksMaxCount = 10;
    public bool RecentTasksShowInMainWindow = true;
    public bool RecentTasksShowInTrayMenu = true;
    public bool RecentTasksTrayMenuMostRecentFirst = false;

    // TODO: Add HistorySettings when HistoryLib is ported
    // TODO: Add ImageHistorySettings when HistoryLib is ported

    #endregion Settings - History

    #region Settings - Print

    public bool DontShowPrintSettingsDialog = false;
    public PrintSettings PrintSettings = new PrintSettings();

    #endregion Settings - Print

    #region Settings - Advanced

    [Category("Application"), DefaultValue(false), Description("Calculate and show file sizes in binary units (KiB, MiB etc.)")]
    public bool BinaryUnits { get; set; }



    [Category("Application"), DefaultValue(false), Description("Show most recent task first in main window.")]
    public bool ShowMostRecentTaskFirst { get; set; }

    [Category("Application"), DefaultValue(false), Description("Show only customized tasks in main window workflows.")]
    public bool WorkflowsOnlyShowEdited { get; set; }

    [Category("Application"), DefaultValue(false), Description("Automatically expand capture menu when you open the tray menu.")]
    public bool TrayAutoExpandCaptureMenu { get; set; }

    [Category("Application"), DefaultValue(true), Description("Show tips and hotkeys in main window when task list is empty.")]
    public bool ShowMainWindowTip { get; set; }

    [Category("Application"), DefaultValue(""), Description("Custom browser path.")]
    public string BrowserPath { get; set; } = "";

    [Category("Application"), DefaultValue(false), Description("Save settings after task completed.")]
    public bool SaveSettingsAfterTaskCompleted { get; set; }

    [Category("Application"), DefaultValue(false), Description("Auto select last completed task.")]
    public bool AutoSelectLastCompletedTask { get; set; }

    [Category("Application"), DefaultValue(false), Description("Enable developer mode.")]
    public bool DevMode { get; set; }

    [Category("Hotkey"), DefaultValue(false), Description("Disable all hotkeys.")]
    public bool DisableHotkeys { get; set; }

    [Category("Hotkey"), DefaultValue(false), Description("Disable hotkeys on fullscreen.")]
    public bool DisableHotkeysOnFullscreen { get; set; }

    private int hotkeyRepeatLimit = 500;

    [Category("Hotkey"), DefaultValue(500), Description("Hotkey repeat limit in milliseconds.")]
    public int HotkeyRepeatLimit
    {
        get => hotkeyRepeatLimit;
        set => hotkeyRepeatLimit = Math.Max(value, 200);
    }

    [Category("Clipboard"), DefaultValue(true), Description("Show clipboard content viewer.")]
    public bool ShowClipboardContentViewer { get; set; }

    [Category("Clipboard"), DefaultValue(true), Description("Fill white background for clipboard copy.")]
    public bool DefaultClipboardCopyImageFillBackground { get; set; }

    [Category("Clipboard"), DefaultValue(false), Description("Use alternative clipboard copy image method.")]
    public bool UseAlternativeClipboardCopyImage { get; set; }

    [Category("Clipboard"), DefaultValue(false), Description("Use alternative clipboard get image method.")]
    public bool UseAlternativeClipboardGetImage { get; set; }

    [Category("Image"), DefaultValue(true), Description("Rotate image by EXIF orientation.")]
    public bool RotateImageByExifOrientationData { get; set; }

    [Category("Image"), DefaultValue(false), Description("Strip PNG color space information.")]
    public bool PNGStripColorSpaceInformation { get; set; }

    [Category("Upload"), DefaultValue(false), Description("Disable uploading application-wide.")]
    public bool DisableUpload { get; set; }

    [Category("Upload"), DefaultValue(true), Description("Ignore emojis in URL encoding.")]
    public bool URLEncodeIgnoreEmoji { get; set; }

    [Category("Upload"), DefaultValue(true), Description("Show multi-upload warning.")]
    public bool ShowMultiUploadWarning { get; set; }

    [Category("Upload"), DefaultValue(100), Description("Large file size warning threshold in MB.")]
    public int ShowLargeFileSizeWarning { get; set; }

    [Category("Paths"), DefaultValue(true), Description("Use machine-specific uploaders config.")]
    public bool UseMachineSpecificUploadersConfig { get; set; } = true;

    [Category("Paths"), Description("Custom uploaders config path.")]
    public string CustomUploadersConfigPath { get; set; } = "";

    [Category("Paths"), Description("Custom workflows config path.")]
    public string CustomWorkflowsConfigPath { get; set; } = "";

    [Category("Paths"), Description("Secondary custom screenshots path.")]
    public string CustomScreenshotsPath2 { get; set; } = "";

    [Category("Drag and drop window"), DefaultValue(150), Description("Drop window size.")]
    public int DropSize { get; set; }

    [Category("Drag and drop window"), DefaultValue(5), Description("Drop window offset.")]
    public int DropOffset { get; set; }

    [Category("Drag and drop window"), DefaultValue(ContentAlignment.BottomRight), Description("Drop window alignment.")]
    public ContentAlignment DropAlignment { get; set; }

    [Category("Drag and drop window"), DefaultValue(100), Description("Drop window opacity.")]
    public int DropOpacity { get; set; }

    [Category("Drag and drop window"), DefaultValue(255), Description("Drop window hover opacity.")]
    public int DropHoverOpacity { get; set; }

    #endregion Settings - Advanced

    #region AutoCapture Form

    public Rectangle AutoCaptureRegion = Rectangle.Empty;
    public decimal AutoCaptureRepeatTime = 60;
    public bool AutoCaptureMinimizeToTray = true;
    public bool AutoCaptureWaitUpload = true;

    #endregion AutoCapture Form

    #region ScreenRecord Form

    public Rectangle ScreenRecordRegion = Rectangle.Empty;

    #endregion ScreenRecord Form

    #region Actions toolbar

    public List<HotkeyType> ActionsToolbarList = new List<HotkeyType>()
    {
        HotkeyType.RectangleRegion,
        HotkeyType.PrintScreen,
        HotkeyType.ScreenRecorder,
        HotkeyType.None,
        HotkeyType.FileUpload,
        HotkeyType.ClipboardUploadWithContentViewer
    };

    public bool ActionsToolbarRunAtStartup = false;
    public System.Drawing.Point ActionsToolbarPosition = System.Drawing.Point.Empty;
    public bool ActionsToolbarLockPosition = false;
    public bool ActionsToolbarStayTopMost = true;

    #endregion Actions toolbar

    #region Color Picker Form

    public List<Color> RecentColors = new List<Color>();

    #endregion Color Picker Form
}

/// <summary>
/// Quick task preset information
/// </summary>
public class QuickTaskInfo
{
    public string Name { get; set; } = "";
    public AfterCaptureTasks AfterCapture { get; set; }
    public AfterUploadTasks AfterUpload { get; set; }

    public static List<QuickTaskInfo> DefaultPresets => new List<QuickTaskInfo>
    {
        new QuickTaskInfo { Name = "Save, Upload, Copy URL", AfterCapture = AfterCaptureTasks.SaveImageToFile | AfterCaptureTasks.UploadImageToHost, AfterUpload = AfterUploadTasks.CopyURLToClipboard },
        new QuickTaskInfo { Name = "Save only", AfterCapture = AfterCaptureTasks.SaveImageToFile, AfterUpload = AfterUploadTasks.None },
        new QuickTaskInfo { Name = "Copy to clipboard", AfterCapture = AfterCaptureTasks.CopyImageToClipboard, AfterUpload = AfterUploadTasks.None },
        new QuickTaskInfo { Name = "Annotate", AfterCapture = AfterCaptureTasks.AnnotateImage, AfterUpload = AfterUploadTasks.None },
    };
}

/// <summary>
/// Recent task tracking
/// </summary>
public class RecentTask
{
    public string FilePath { get; set; } = "";
    public string URL { get; set; } = "";
    public string ThumbnailURL { get; set; } = "";
    public string DeletionURL { get; set; } = "";
    public string ShortenedURL { get; set; } = "";
}
