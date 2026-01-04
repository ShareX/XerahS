using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.IO;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Image annotation - stickers or inserted images
/// </summary>
public class ImageAnnotation : Annotation
{
    private Bitmap? _imageBitmap;
    
    /// <summary>
    /// File path to the image (if external)
    /// </summary>
    public string ImagePath { get; set; } = "";

    /// <summary>
    /// Base64 encoded data or similar if embedded directly?
    /// For now, we rely on the Bitmap being loaded.
    /// </summary>

    public ImageAnnotation()
    {
        ToolType = EditorTool.Image;
        StrokeWidth = 0; // Usually no border
    }

    public void LoadImage(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                ImagePath = path;
                using var fs = File.OpenRead(path);
                _imageBitmap = new Bitmap(fs);
                // Default size to image size?
                // Or user draws rect?
                // Usually stickers are dropped at native resolution or a default size.
            }
            catch { }
        }
    }

    public void SetImage(Bitmap bitmap)
    {
        _imageBitmap = bitmap; // Take ownership? Clone?
    }

    public override void Render(DrawingContext context)
    {
        var rect = GetBounds();
        
        if (_imageBitmap != null)
        {
            context.DrawImage(_imageBitmap, rect);
        }
        else
        {
            // Placeholder
            var pen = new Pen(Brushes.Gray, 2) { DashStyle = DashStyle.Dash };
            context.DrawRectangle(null, pen, rect);
            
            var formattedText = new FormattedText(
                "Image",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                Brushes.Gray);
                
            context.DrawText(formattedText, rect.Center - new Point(formattedText.Width/2, formattedText.Height/2));
        }

        if (IsSelected)
        {
            var selPen = new Pen(Brushes.DodgerBlue, 2);
            context.DrawRectangle(null, selPen, rect);
        }
    }

    public override bool HitTest(Point point, double tolerance = 5)
    {
        return GetBounds().Inflate(tolerance).Contains(point);
    }
}
