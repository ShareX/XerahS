using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Highlight annotation - translucent color overlay
/// </summary>
public class HighlightAnnotation : BaseEffectAnnotation
{
    public HighlightAnnotation()
    {
        ToolType = EditorTool.Highlighter;
        StrokeColor = "#55FFFF00"; // Default yellow transparent
        StrokeWidth = 0; // No border by default
    }

    public override void Render(DrawingContext context)
    {
        var rect = GetBounds();
        var brush = new SolidColorBrush(ParseColor(StrokeColor));
        
        // Draw the highlight rectangle
        context.DrawRectangle(brush, null, rect);
    }
}
