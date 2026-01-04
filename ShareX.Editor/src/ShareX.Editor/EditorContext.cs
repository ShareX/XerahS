using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// The main editor context that coordinates all editing operations.
/// </summary>
public class EditorContext : IDisposable
{
    /// <summary>
    /// The shape manager for this editor.
    /// </summary>
    public ShapeManager ShapeManager { get; } = new();

    /// <summary>
    /// The command stack for undo/redo.
    /// </summary>
    public CommandStack CommandStack { get; } = new();

    /// <summary>
    /// The background image being edited.
    /// </summary>
    public SKBitmap? BackgroundImage { get; private set; }

    /// <summary>
    /// Canvas size in pixels.
    /// </summary>
    public SKSize CanvasSize { get; private set; }

    /// <summary>
    /// The current tool selected.
    /// </summary>
    public ShapeType CurrentTool { get; set; } = ShapeType.DrawingRectangle;

    /// <summary>
    /// Current border color for new shapes.
    /// </summary>
    public SKColor CurrentBorderColor { get; set; } = SKColors.Red;

    /// <summary>
    /// Current fill color for new shapes.
    /// </summary>
    public SKColor CurrentFillColor { get; set; } = SKColors.Transparent;

    /// <summary>
    /// Current border thickness for new shapes.
    /// </summary>
    public float CurrentBorderThickness { get; set; } = 2f;

    /// <summary>
    /// Loads an image for editing.
    /// </summary>
    public void LoadImage(SKBitmap image)
    {
        BackgroundImage?.Dispose();
        BackgroundImage = image;
        CanvasSize = new SKSize(image.Width, image.Height);
        ShapeManager.Clear();
        CommandStack.Clear();
    }

    /// <summary>
    /// Loads an image from file path.
    /// </summary>
    public bool LoadImageFromFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                LoadImage(bitmap);
                return true;
            }
        }
        catch
        {
            // Ignore load errors
        }
        return false;
    }

    /// <summary>
    /// Creates a new shape based on the current tool.
    /// </summary>
    public BaseShape? CreateShape()
    {
        BaseDrawingShape? shape = CurrentTool switch
        {
            ShapeType.DrawingRectangle => new RectangleDrawingShape(),
            ShapeType.DrawingEllipse => new EllipseDrawingShape(),
            ShapeType.DrawingLine => new LineDrawingShape(),
            ShapeType.DrawingArrow => new ArrowDrawingShape(),
            ShapeType.DrawingText => new TextDrawingShape(),
            _ => null
        };

        if (shape != null)
        {
            shape.BorderColor = CurrentBorderColor;
            shape.FillColor = CurrentFillColor;
            shape.BorderThickness = CurrentBorderThickness;
        }

        return shape;
    }

    /// <summary>
    /// Renders the entire editor canvas.
    /// </summary>
    public void Draw(SKCanvas canvas)
    {
        // Draw background
        if (BackgroundImage != null)
        {
            canvas.DrawBitmap(BackgroundImage, 0, 0);
        }
        else
        {
            canvas.Clear(SKColors.White);
        }

        // Draw effect shapes (apply to background)
        foreach (var shape in ShapeManager.EffectShapes)
        {
            shape.OnDraw(canvas);
        }

        // Draw drawing shapes
        foreach (var shape in ShapeManager.DrawingShapes)
        {
            shape.OnDraw(canvas);
        }

        // Draw shape being created
        if (ShapeManager.IsCreating && ShapeManager.CurrentShape != null)
        {
            ShapeManager.CurrentShape.OnDraw(canvas);
        }

        // Draw selection
        DrawSelection(canvas);
    }

    private void DrawSelection(SKCanvas canvas)
    {
        if (ShapeManager.SelectedShape == null) return;

        var rect = ShapeManager.SelectedShape.Rectangle;
        using var paint = new SKPaint
        {
            Color = SKColors.DodgerBlue,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
        };
        canvas.DrawRect(rect.Left - 2, rect.Top - 2, rect.Width + 4, rect.Height + 4, paint);

        // Draw resize handles
        DrawResizeHandles(canvas, rect);
    }

    private void DrawResizeHandles(SKCanvas canvas, SKRect rect)
    {
        const float handleSize = 6;
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill
        };
        using var borderPaint = new SKPaint
        {
            Color = SKColors.DodgerBlue,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };

        var handles = new[]
        {
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.MidX, rect.Top),
            new SKPoint(rect.Right, rect.Top),
            new SKPoint(rect.Right, rect.MidY),
            new SKPoint(rect.Right, rect.Bottom),
            new SKPoint(rect.MidX, rect.Bottom),
            new SKPoint(rect.Left, rect.Bottom),
            new SKPoint(rect.Left, rect.MidY)
        };

        foreach (var handle in handles)
        {
            var handleRect = new SKRect(
                handle.X - handleSize / 2,
                handle.Y - handleSize / 2,
                handle.X + handleSize / 2,
                handle.Y + handleSize / 2
            );
            canvas.DrawRect(handleRect, paint);
            canvas.DrawRect(handleRect, borderPaint);
        }
    }

    /// <summary>
    /// Exports the final image with all annotations applied.
    /// </summary>
    public SKBitmap? ExportImage()
    {
        if (BackgroundImage == null) return null;

        var result = new SKBitmap(BackgroundImage.Width, BackgroundImage.Height);
        using var canvas = new SKCanvas(result);

        // Draw background
        canvas.DrawBitmap(BackgroundImage, 0, 0);

        // Apply effects
        foreach (var shape in ShapeManager.EffectShapes)
        {
            if (shape is BlurEffectShape blur)
                blur.ApplyEffect(result);
            else if (shape is PixelateEffectShape pixelate)
                pixelate.ApplyEffect(result);
        }

        // Draw annotations on top
        foreach (var shape in ShapeManager.DrawingShapes)
        {
            shape.OnDraw(canvas);
        }

        return result;
    }

    /// <summary>
    /// Saves the exported image to a file.
    /// </summary>
    public bool SaveToFile(string filePath)
    {
        using var bitmap = ExportImage();
        if (bitmap == null) return false;

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
        return true;
    }

    public void Dispose()
    {
        BackgroundImage?.Dispose();
        ShapeManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
