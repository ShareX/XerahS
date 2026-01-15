using System.ComponentModel;

namespace XerahS.Core;

public enum RegionCaptureAction
{
    None,
    CancelCapture,
    RemoveShapeCancelCapture,
    RemoveShape,
    SwapToolType,
    CaptureFullscreen,
    CaptureActiveMonitor,
    CaptureLastRegion
}

public enum ShapeType
{
    RegionRectangle,
    RegionEllipse,
    RegionFreehand,
    ToolSelect,
    DrawingRectangle,
    DrawingEllipse,
    DrawingFreehand,
    DrawingFreehandArrow,
    DrawingLine,
    DrawingArrow,
    DrawingTextOutline,
    DrawingTextBackground,
    DrawingSpeechBalloon,
    DrawingStep,
    DrawingMagnify,
    DrawingImage,
    DrawingImageScreen,
    DrawingSticker,
    DrawingCursor,
    DrawingSmartEraser,
    EffectBlur,
    EffectPixelate,
    EffectHighlight,
    ToolSpotlight,
    ToolCrop,
    ToolCutOut
}

public enum ImageEditorStartMode
{
    AutoSize,
    Normal,
    Maximized,
    PreviousState,
    Fullscreen
}

public enum ImageInterpolationMode
{
    HighQualityBicubic,
    Bicubic,
    HighQualityBilinear,
    Bilinear,
    NearestNeighbor
}

public enum ImageBeautifierBackgroundType
{
    Gradient,
    Color,
    Image,
    Desktop,
    Transparent
}

public enum ImageCombinerAlignment
{
    LeftOrTop,
    Center,
    RightOrBottom
}

public enum ConverterVideoCodecs
{
    [Description("H.264 / x264")]
    x264,
    [Description("H.265 / x265")]
    x265,
    [Description("H.264 / NVENC")]
    h264_nvenc,
    [Description("HEVC / NVENC")]
    hevc_nvenc,
    [Description("H.264 / AMF")]
    h264_amf,
    [Description("HEVC / AMF")]
    hevc_amf,
    [Description("H.264 / Quick Sync")]
    h264_qsv,
    [Description("HEVC / Quick Sync")]
    hevc_qsv,
    [Description("VP8")]
    vp8,
    [Description("VP9")]
    vp9,
    [Description("AV1")]
    av1,
    [Description("MPEG-4 / Xvid")]
    xvid,
    [Description("GIF")]
    gif,
    [Description("WebP")]
    webp,
    [Description("APNG")]
    apng
}

public enum ThumbnailLocationType
{
    [Description("Default folder")]
    DefaultFolder,
    [Description("Parent folder of the media file")]
    ParentFolder,
    [Description("Custom folder")]
    CustomFolder
}

public enum EImageFormat
{
    [Description("png")]
    PNG,
    [Description("jpg")]
    JPEG,
    [Description("gif")]
    GIF,
    [Description("bmp")]
    BMP,
    [Description("tif")]
    TIFF
}

public enum IndexerOutput
{
    [Description("Text")]
    Txt,
    [Description("HTML")]
    Html,
    [Description("XML")]
    Xml,
    [Description("JSON")]
    Json
}

public enum AIProvider
{
    OpenAI,
    Gemini,
    OpenRouter,
    Custom
}

public enum Orientation
{
    Horizontal,
    Vertical
}



public enum ScreenRecordState
{
    Waiting,
    BeforeStart,
    AfterStart,
    AfterRecordingStart,
    RecordingEnd,
    Encoding
}

public enum ScreenRecordingStatus
{
    Waiting,
    Working,
    Recording,
    Paused,
    Stopped,
    Aborted
}

public enum FFmpegVideoCodec
{
    [Description("H.264 / x264")]
    libx264,
    [Description("H.265 / x265")]
    libx265,
    [Description("VP8")]
    libvpx,
    [Description("VP9")]
    libvpx_vp9,
    [Description("MPEG-4 / Xvid")]
    libxvid,
    [Description("H.264 / NVENC")]
    h264_nvenc,
    [Description("HEVC / NVENC")]
    hevc_nvenc,
    [Description("H.264 / AMF")]
    h264_amf,
    [Description("HEVC / AMF")]
    hevc_amf,
    [Description("H.264 / Quick Sync")]
    h264_qsv,
    [Description("HEVC / Quick Sync")]
    hevc_qsv,
    [Description("GIF")]
    gif,
    [Description("WebP")]
    libwebp,
    [Description("APNG")]
    apng
}

public enum FFmpegAudioCodec
{
    [Description("AAC")]
    libvoaacenc,
    [Description("Opus")]
    libopus,
    [Description("Vorbis")]
    libvorbis,
    [Description("MP3")]
    libmp3lame
}

public enum FFmpegPreset
{
    [Description("Ultra fast")]
    ultrafast,
    [Description("Super fast")]
    superfast,
    [Description("Very fast")]
    veryfast,
    [Description("Faster")]
    faster,
    [Description("Fast")]
    fast,
    [Description("Medium")]
    medium,
    [Description("Slow")]
    slow,
    [Description("Slower")]
    slower,
    [Description("Very slow")]
    veryslow,
    [Description("Placebo")]
    placebo
}

public enum FFmpegNVENCPreset
{
    [Description("Fastest (Lowest quality)")]
    p1,
    [Description("Faster (Lower quality)")]
    p2,
    [Description("Fast (Low quality)")]
    p3,
    [Description("Medium (Medium quality)")]
    p4,
    [Description("Slow (Good quality)")]
    p5,
    [Description("Slower (Better quality)")]
    p6,
    [Description("Slowest (Best quality)")]
    p7
}

public enum FFmpegNVENCTune
{
    [Description("High quality")]
    hq,
    [Description("Low latency")]
    ll,
    [Description("Ultra low latency")]
    ull,
    [Description("Lossless")]
    lossless
}

public enum FFmpegPaletteGenStatsMode
{
    full,
    diff,
    single
}

public enum FFmpegPaletteUseDither
{
    none,
    bayer,
    heckbert,
    floyd_steinberg,
    sierra2,
    sierra2_4a,
    sierra3,
    burkes,
    atkinson
}

public enum FFmpegAMFUsage
{
    [Description("Generic transcoding")]
    transcoding,
    [Description("Ultra low latency transcoding")]
    ultralowlatency,
    [Description("Low latency transcoding")]
    lowlatency,
    [Description("Webcam")]
    webcam,
    [Description("High quality transcoding")]
    high_quality,
    [Description("Low latency yet high quality transcoding")]
    lowlatency_high_quality
}

public enum FFmpegAMFQuality
{
    [Description("Prefer speed")]
    speed,
    [Description("Balanced")]
    balanced,
    [Description("Prefer quality")]
    quality
}

public enum FFmpegQSVPreset
{
    [Description("Very fast")]
    veryfast,
    [Description("Faster")]
    faster,
    [Description("Fast")]
    fast,
    [Description("Medium")]
    medium,
    [Description("Slow")]
    slow,
    [Description("Slower")]
    slower,
    [Description("Very slow")]
    veryslow
}
