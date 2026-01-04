namespace ShareX.Editor;

/// <summary>
/// Enumerates all available shape types.
/// </summary>
public enum ShapeType
{
    // Region
    RegionRectangle,
    RegionEllipse,
    RegionFreehand,

    // Drawing
    DrawingRectangle,
    DrawingEllipse,
    DrawingLine,
    DrawingArrow,
    DrawingFreehand,
    DrawingFreehandArrow,
    DrawingText,
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

    // Effect
    EffectBlur,
    EffectPixelate,
    EffectHighlight,

    // Tool
    ToolSelect,
    ToolCrop
}
