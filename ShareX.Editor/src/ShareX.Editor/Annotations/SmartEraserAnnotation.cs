using Avalonia;
using Avalonia.Media;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Smart Eraser annotation - samples pixel color from the rendered canvas (including other annotations)
/// at click point and uses it for drawing to hide sensitive information by covering it with the 
/// sampled color from the visual output
/// </summary>
public class SmartEraserAnnotation : FreehandAnnotation
{
    public SmartEraserAnnotation()
    {
        ToolType = EditorTool.SmartEraser;
        // Default to semi-transparent red (will be overridden by sampled color from rendered canvas)
        StrokeColor = "#80FF0000";
        StrokeWidth = 10;
    }

    // StrokeColor will be set to the sampled pixel color from the RENDERED canvas
    // (including all annotations) when the user first clicks with the Smart Eraser tool.
    // This allows users to "paint over" sensitive information with colors that match
    // existing annotations or the background, effectively hiding it seamlessly.
}
