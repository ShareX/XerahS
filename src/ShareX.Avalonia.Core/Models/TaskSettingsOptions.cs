using Newtonsoft.Json;
using ShareX.Editor.ImageEffects;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace XerahS.Core;

// Stubs for complex types
public class AnnotationOptions
{
    // TODO: Port AnnotationOptions
}

public class ImageEffectPreset
{
    public string Name { get; set; } = "";

    [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
    public List<ImageEffect> Effects { get; set; } = new();

    public static ImageEffectPreset GetDefaultPreset()
    {
        return new ImageEffectPreset { Name = "Default" };
    }

    public override string ToString()
    {
        return Name;
    }
}

public class ColorPickerOptions
{
    // TODO: Port ColorPickerOptions
}

public class WindowState
{
    // TODO: Port WindowState
}

// Option Classes

public class RegionCaptureOptions
{
    public const int DefaultMinimumSize = 5;
    public const int MagnifierPixelCountMinimum = 3;
    public const int MagnifierPixelCountMaximum = 35;
    public const int MagnifierPixelSizeMinimum = 3;
    public const int MagnifierPixelSizeMaximum = 30;
    public const int SnapDistance = 30;
    public const int MoveSpeedMinimum = 1;
    public const int MoveSpeedMaximum = 10;

    public bool QuickCrop { get; set; } = true;
    public int MinimumSize { get; set; } = DefaultMinimumSize;
    public RegionCaptureAction RegionCaptureActionRightClick { get; set; } = RegionCaptureAction.RemoveShapeCancelCapture;
    public RegionCaptureAction RegionCaptureActionMiddleClick { get; set; } = RegionCaptureAction.SwapToolType;
    public RegionCaptureAction RegionCaptureActionX1Click { get; set; } = RegionCaptureAction.CaptureFullscreen;
    public RegionCaptureAction RegionCaptureActionX2Click { get; set; } = RegionCaptureAction.CaptureActiveMonitor;
    public bool DetectWindows { get; set; } = true;
    public bool DetectControls { get; set; } = true;
    public bool UseDimming { get; set; } = true;
    public int BackgroundDimStrength { get; set; } = 20;
    public bool UseCustomInfoText { get; set; } = false;
    public string CustomInfoText { get; set; } = "X: $x, Y: $y$nR: $r, G: $g, B: $b$nHex: $hex";
    public List<SnapSize> SnapSizes { get; set; } = new List<SnapSize>()
    {
        new SnapSize(426, 240), // 240p
        new SnapSize(640, 360), // 360p
        new SnapSize(854, 480), // 480p
        new SnapSize(1280, 720), // 720p
        new SnapSize(1920, 1080) // 1080p
    };
    public bool ShowInfo { get; set; } = true;
    public bool ShowMagnifier { get; set; } = true;
    public bool UseSquareMagnifier { get; set; } = false;
    public int MagnifierPixelCount { get; set; } = 15;
    public int MagnifierPixelSize { get; set; } = 10;
    public bool ShowCrosshair { get; set; } = false;
    public bool UseLightResizeNodes { get; set; } = false;
    public bool EnableAnimations { get; set; } = true;
    public bool IsFixedSize { get; set; } = false;
    public Size FixedSize { get; set; } = new Size(250, 250);
    public bool ShowFPS { get; set; } = false;
    public int FPSLimit { get; set; } = 100;
    public int MenuIconSize { get; set; } = 0;
    public bool MenuLocked { get; set; } = false;
    public bool RememberMenuState { get; set; } = false;
    public bool MenuCollapsed { get; set; } = false;
    public System.Drawing.Point MenuPosition { get; set; } = System.Drawing.Point.Empty;
    public int InputDelay { get; set; } = 500;
    public bool SwitchToDrawingToolAfterSelection { get; set; } = false;
    public bool SwitchToSelectionToolAfterDrawing { get; set; } = false;
    public bool ActiveMonitorMode { get; set; } = false;

    public AnnotationOptions AnnotationOptions { get; set; } = new AnnotationOptions();
    public ShapeType LastRegionTool { get; set; } = ShapeType.RegionRectangle;
    public ShapeType LastAnnotationTool { get; set; } = ShapeType.DrawingRectangle;
    public ShapeType LastEditorTool { get; set; } = ShapeType.DrawingRectangle;

    public ImageEditorStartMode ImageEditorStartMode { get; set; } = ImageEditorStartMode.AutoSize;
    public WindowState ImageEditorWindowState { get; set; } = new WindowState();
    public bool ZoomToFitOnOpen { get; set; } = false;
    public bool EditorAutoCopyImage { get; set; } = false;
    public bool AutoCloseEditorOnTask { get; set; } = false;
    public bool ShowEditorPanTip { get; set; } = true;
    public ImageInterpolationMode ImageEditorResizeInterpolationMode { get; set; } = ImageInterpolationMode.Bicubic;
    public Size EditorNewImageSize { get; set; } = new Size(800, 600);
    public bool EditorNewImageTransparent { get; set; } = false;
    public Color EditorNewImageBackgroundColor { get; set; } = Color.White;
    public Color EditorCanvasColor { get; set; } = Color.Transparent;
    public List<ImageEffectPreset> ImageEffectPresets { get; set; } = new List<ImageEffectPreset>();
    public int SelectedImageEffectPreset { get; set; } = 0;

    public ColorPickerOptions ColorPickerOptions { get; set; } = new ColorPickerOptions();
    public string ScreenColorPickerInfoText { get; set; } = "";
}

public class SnapSize
{
    private const int MinimumWidth = 2;
    private int width;
    public int Width
    {
        get => width;
        set => width = Math.Max(value, MinimumWidth);
    }

    private const int MinimumHeight = 2;
    private int height;
    public int Height
    {
        get => height;
        set => height = Math.Max(value, MinimumHeight);
    }

    public SnapSize()
    {
        width = MinimumWidth;
        height = MinimumHeight;
    }

    public SnapSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString() => $"{Width}x{Height}";
}

public class ScrollingCaptureOptions
{
    public int StartDelay { get; set; } = 300;
    public bool AutoScrollTop { get; set; } = false;
    public int ScrollDelay { get; set; } = 300;
    // ScrollMethod is in Enums
    // public ScrollMethod ScrollMethod { get; set; } = ScrollMethod.MouseWheel; 
    public int ScrollAmount { get; set; } = 2;
    public bool AutoIgnoreBottomEdge { get; set; } = true;
    public bool AutoUpload { get; set; } = false;
    public bool ShowRegion { get; set; } = true;
}

public class OCROptions
{
    public string Language { get; set; } = "en";
    public float ScaleFactor { get; set; } = 2f;
    public bool SingleLine { get; set; } = false;
    public bool Silent { get; set; } = false;
    public bool AutoCopy { get; set; } = false;
    public List<ServiceLink> ServiceLinks { get; set; } = DefaultServiceLinks;
    public bool CloseWindowAfterOpeningServiceLink { get; set; } = false;
    public int SelectedServiceLink { get; set; } = 0;

    public static List<ServiceLink> DefaultServiceLinks => new List<ServiceLink>()
    {
        new ServiceLink("Google Translate", "https://translate.google.com/?sl=auto&tl=en&text={0}&op=translate"),
        new ServiceLink("Google Search", "https://www.google.com/search?q={0}"),
        new ServiceLink("Google Images", "https://www.google.com/search?q={0}&tbm=isch"),
        new ServiceLink("Bing", "https://www.bing.com/search?q={0}"),
        new ServiceLink("DuckDuckGo", "https://duckduckgo.com/?q={0}"),
        new ServiceLink("DeepL", "https://www.deepl.com/translator#auto/en/{0}")
    };
}

public class ServiceLink
{
    public string Name { get; set; }
    public string URL { get; set; }

    public ServiceLink(string name, string url)
    {
        Name = name;
        URL = url;
    }

    public override string ToString() => Name;
}

public class PinToScreenOptions
{
    public int InitialScale { get; set; } = 100;
    public int ScaleStep { get; set; } = 10;
    public bool HighQualityScale { get; set; } = true;
    public int InitialOpacity { get; set; } = 100;
    public int OpacityStep { get; set; } = 10;
    public ContentAlignment Placement { get; set; } = ContentAlignment.BottomRight;
    public int PlacementOffset { get; set; } = 10;
    public bool TopMost { get; set; } = true;
    public bool KeepCenterLocation { get; set; } = true;
    public Color BackgroundColor { get; set; } = Color.White;
    public bool Shadow { get; set; } = true;
    public bool Border { get; set; } = true;
    public int BorderSize { get; set; } = 2;
    public Color BorderColor { get; set; } = Color.CornflowerBlue;
    public Size MinimizeSize { get; set; } = new Size(100, 100);
}

public class IndexerSettings
{
    [DefaultValue(IndexerOutput.Html)]
    public IndexerOutput Output { get; set; }

    [DefaultValue(true)]
    public bool SkipHiddenFolders { get; set; }

    [DefaultValue(true)]
    public bool SkipHiddenFiles { get; set; }

    [DefaultValue(false)]
    public bool SkipFiles { get; set; }

    [DefaultValue(0)]
    public int MaxDepthLevel { get; set; }

    [DefaultValue(true)]
    public bool ShowSizeInfo { get; set; }

    [DefaultValue(true)]
    public bool AddFooter { get; set; }

    [DefaultValue("|___")]
    public string IndentationText { get; set; }

    [DefaultValue(false)]
    public bool AddEmptyLineAfterFolders { get; set; }

    [DefaultValue(false)]
    public bool UseCustomCSSFile { get; set; }

    [DefaultValue(false)]
    public bool DisplayPath { get; set; }

    [DefaultValue(false)]
    public bool DisplayPathLimited { get; set; }

    [DefaultValue("")]
    public string CustomCSSFilePath { get; set; }

    [DefaultValue(true)]
    public bool UseAttribute { get; set; }

    [DefaultValue(true)]
    public bool CreateParseableJson { get; set; }

    [JsonIgnore]
    public bool BinaryUnits;

    public IndexerSettings()
    {
        Output = IndexerOutput.Html;
        SkipHiddenFolders = true;
        SkipHiddenFiles = true;
        IndentationText = "|___";
        ShowSizeInfo = true;
        AddFooter = true;
        UseAttribute = true;
        CreateParseableJson = true;
    }
}

public class ImageBeautifierOptions
{
    public int Margin { get; set; }
    public int Padding { get; set; }
    public bool SmartPadding { get; set; }
    public int RoundedCorner { get; set; }
    public int ShadowRadius { get; set; }
    public int ShadowOpacity { get; set; }
    public int ShadowDistance { get; set; }
    public int ShadowAngle { get; set; }
    public Color ShadowColor { get; set; }
    public ImageBeautifierBackgroundType BackgroundType { get; set; }
    public GradientInfo BackgroundGradient { get; set; }
    public Color BackgroundColor { get; set; }
    public string BackgroundImageFilePath { get; set; }

    public ImageBeautifierOptions()
    {
        Margin = 80;
        Padding = 40;
        SmartPadding = true;
        RoundedCorner = 20;
        ShadowRadius = 30;
        ShadowOpacity = 80;
        ShadowDistance = 10;
        ShadowAngle = 180;
        ShadowColor = Color.Black;
        BackgroundType = ImageBeautifierBackgroundType.Gradient;
        BackgroundGradient = new GradientInfo(LinearGradientMode.ForwardDiagonal, Color.FromArgb(255, 81, 47), Color.FromArgb(221, 36, 118));
        BackgroundColor = Color.FromArgb(34, 34, 34);
        BackgroundImageFilePath = "";
    }
}

public class GradientInfo
{
    public LinearGradientMode Type { get; set; }
    public List<GradientStop> Colors { get; set; }

    public GradientInfo() : this(LinearGradientMode.Vertical) { }

    public GradientInfo(LinearGradientMode type)
    {
        Type = type;
        Colors = new List<GradientStop>();
    }

    public GradientInfo(LinearGradientMode type, params Color[] colors) : this(type)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            Colors.Add(new GradientStop(colors[i], (int)Math.Round(100f / (colors.Length - 1) * i)));
        }
    }
}

public class GradientStop
{
    public Color Color { get; set; } = Color.Black;
    public float Location { get; set; }

    public GradientStop() { }
    public GradientStop(Color color, float offset)
    {
        Color = color;
        Location = offset;
    }
}

public class ImageCombinerOptions
{
    public Orientation Orientation { get; set; } = Orientation.Vertical;
    public ImageCombinerAlignment Alignment { get; set; } = ImageCombinerAlignment.LeftOrTop;
    public int Space { get; set; } = 0;
    public int WrapAfter { get; set; } = 0;
    public bool AutoFillBackground { get; set; } = true;
}

public class VideoConverterOptions
{
    public string InputFilePath { get; set; }
    public string OutputFolderPath { get; set; }
    public string OutputFileName { get; set; }

    public ConverterVideoCodecs VideoCodec { get; set; } = ConverterVideoCodecs.x264;
    public int VideoQuality { get; set; } = 23;
    public bool VideoQualityUseBitrate { get; set; } = false;
    public int VideoQualityBitrate { get; set; } = 3000;
    public bool UseCustomArguments { get; set; } = false;
    public string CustomArguments { get; set; } = "";
    public bool AutoOpenFolder { get; set; } = true;
}

public class VideoThumbnailOptions
{
    public ThumbnailLocationType OutputLocation { get; set; } = ThumbnailLocationType.DefaultFolder;
    public string CustomOutputDirectory { get; set; } = "";
    public EImageFormat ImageFormat { get; set; } = EImageFormat.PNG;
    public int ThumbnailCount { get; set; } = 9;
    public string FilenameSuffix { get; set; } = "_Thumbnail";
    public bool RandomFrame { get; set; } = false;
    public bool UploadThumbnails { get; set; } = true;
    public bool KeepScreenshots { get; set; } = false;
    public bool OpenDirectory { get; set; } = false;
    public int MaxThumbnailWidth { get; set; } = 512;
    public bool CombineScreenshots { get; set; } = true;
    public int Padding { get; set; } = 10;
    public int Spacing { get; set; } = 10;
    public int ColumnCount { get; set; } = 3;
    public bool AddVideoInfo { get; set; } = true;
    public bool AddTimestamp { get; set; } = true;
    public bool DrawShadow { get; set; } = true;
    public bool DrawBorder { get; set; } = true;
}

public class BorderlessWindowSettings
{
    public bool RememberWindowTitle { get; set; } = true;
    public string WindowTitle { get; set; }
    public bool AutoCloseWindow { get; set; }
    public bool ExcludeTaskbarArea { get; set; }
}

public class AIOptions
{
    public AIProvider Provider { get; set; } = AIProvider.OpenAI;
    public string OpenAIAPIKey { get; set; }
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
    public string OpenAICustomURL { get; set; }
    public string GeminiAPIKey { get; set; }
    public string GeminiModel { get; set; } = "gemini-1.5-flash-latest";
    public string OpenRouterAPIKey { get; set; }
    public string OpenRouterModel { get; set; } = "google/gemini-flash-1.5";
    public string ReasoningEffort { get; set; } = "minimal";
    public string Verbosity { get; set; } = "medium";
    public string Input { get; set; } = "What is in this image?";
    public bool AutoStartRegion { get; set; } = true;
    public bool AutoStartAnalyze { get; set; } = true;
    public bool AutoCopyResult { get; set; } = false;
}

public class FFmpegOptions
{
    // General
    public bool OverrideCLIPath { get; set; } = false;
    public string CLIPath { get; set; } = "";
    public string VideoSource { get; set; } = FFmpegCaptureDevice.GDIGrab.Value;
    public string AudioSource { get; set; } = FFmpegCaptureDevice.None.Value;
    public FFmpegVideoCodec VideoCodec { get; set; } = FFmpegVideoCodec.libx264;
    public FFmpegAudioCodec AudioCodec { get; set; } = FFmpegAudioCodec.libvoaacenc;
    public string UserArgs { get; set; } = "";
    public bool UseCustomCommands { get; set; } = false;
    public string CustomCommands { get; set; } = "";

    // Video
    public FFmpegPreset x264_Preset { get; set; } = FFmpegPreset.ultrafast;
    public int x264_CRF { get; set; } = 28;
    public bool x264_Use_Bitrate { get; set; } = false;
    public int x264_Bitrate { get; set; } = 3000; // kbps
    public int VPx_Bitrate { get; set; } = 3000; // kbps
    public int XviD_QScale { get; set; } = 10;
    public FFmpegNVENCPreset NVENC_Preset { get; set; } = FFmpegNVENCPreset.p4;
    public FFmpegNVENCTune NVENC_Tune { get; set; } = FFmpegNVENCTune.ll;
    public int NVENC_Bitrate { get; set; } = 3000; // kbps
    public FFmpegPaletteGenStatsMode GIFStatsMode { get; set; } = FFmpegPaletteGenStatsMode.full;
    public FFmpegPaletteUseDither GIFDither { get; set; } = FFmpegPaletteUseDither.sierra2_4a;
    public int GIFBayerScale { get; set; } = 2;
    public FFmpegAMFUsage AMF_Usage { get; set; } = FFmpegAMFUsage.lowlatency;
    public FFmpegAMFQuality AMF_Quality { get; set; } = FFmpegAMFQuality.speed;
    public int AMF_Bitrate { get; set; } = 3000; // kbps
    public FFmpegQSVPreset QSV_Preset { get; set; } = FFmpegQSVPreset.fast;
    public int QSV_Bitrate { get; set; } = 3000; // kbps

    // Audio
    public int AAC_Bitrate { get; set; } = 128; // kbps
    public int Opus_Bitrate { get; set; } = 128; // kbps
    public int Vorbis_QScale { get; set; } = 3;
    public int MP3_QScale { get; set; } = 4;

    public string FFmpegPath
    {
        get
        {
            if (OverrideCLIPath && !string.IsNullOrEmpty(CLIPath))
            {
                // Stub: Return raw path or handle minimal logic.
                // Original used FileHelpers.GetAbsolutePath(CLIPath);
                return CLIPath;
            }
            return "ffmpeg.exe"; // Stub
        }
    }

    public bool IsSourceSelected => IsVideoSourceSelected || IsAudioSourceSelected;
    public bool IsVideoSourceSelected => !string.IsNullOrEmpty(VideoSource);
    public bool IsAudioSourceSelected => !string.IsNullOrEmpty(AudioSource) && (!IsVideoSourceSelected || !IsAnimatedImage);
    public bool IsAnimatedImage => VideoCodec == FFmpegVideoCodec.gif || VideoCodec == FFmpegVideoCodec.libwebp || VideoCodec == FFmpegVideoCodec.apng;
    public bool IsEvenSizeRequired => !IsAnimatedImage;
}

public class FFmpegCaptureDevice
{
    public string Value { get; set; }
    public string Title { get; set; }

    public FFmpegCaptureDevice(string value, string title)
    {
        Value = value;
        Title = title;
    }

    public static FFmpegCaptureDevice None { get; } = new FFmpegCaptureDevice("", "None");
    public static FFmpegCaptureDevice GDIGrab { get; } = new FFmpegCaptureDevice("gdigrab", "gdigrab (Graphics Device Interface)");
    public static FFmpegCaptureDevice DDAGrab { get; } = new FFmpegCaptureDevice("ddagrab", "ddagrab (Desktop Duplication API)");
    public static FFmpegCaptureDevice ScreenCaptureRecorder { get; } = new FFmpegCaptureDevice("screen-capture-recorder", "dshow (screen-capture-recorder)");
    public static FFmpegCaptureDevice VirtualAudioCapturer { get; } = new FFmpegCaptureDevice("virtual-audio-capturer", "dshow (virtual-audio-capturer)");

    public override string ToString() => Title;
}
