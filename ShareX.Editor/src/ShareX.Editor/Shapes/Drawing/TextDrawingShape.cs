using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// A text drawing shape.
/// </summary>
public class TextDrawingShape : BaseDrawingShape
{
    public override ShapeType ShapeType => ShapeType.DrawingText;

    /// <summary>
    /// The text content to display.
    /// </summary>
    public string Text { get; set; } = "Text";

    /// <summary>
    /// Font family name.
    /// </summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>
    /// Font size in points.
    /// </summary>
    public float FontSize { get; set; } = 24f;

    /// <summary>
    /// Whether the text is bold.
    /// </summary>
    public bool IsBold { get; set; } = false;

    /// <summary>
    /// Whether the text is italic.
    /// </summary>
    public bool IsItalic { get; set; } = false;

    /// <summary>
    /// Text color.
    /// </summary>
    public SKColor TextColor { get; set; } = SKColors.Black;

    private SKTypeface? _typeface;

    public override void OnDraw(SKCanvas canvas)
    {
        if (string.IsNullOrEmpty(Text)) return;

        _typeface ??= CreateTypeface();

        using var font = new SKFont(_typeface, FontSize);
        using var paint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true
        };

        // Draw fill background if enabled
        if (IsFilled)
        {
            using var bgPaint = CreateFillPaint();
            canvas.DrawRect(Rectangle, bgPaint);
        }

        // Draw text using SKFont
        using var blob = SKTextBlob.Create(Text, font);
        if (blob != null)
        {
            canvas.DrawText(blob, StartPosition.X, StartPosition.Y + FontSize, paint);
        }
    }

    private SKTypeface CreateTypeface()
    {
        SKFontStyle style = new SKFontStyle(
            IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright
        );

        return SKTypeface.FromFamilyName(FontFamily, style) ?? SKTypeface.Default;
    }

    public override void Dispose()
    {
        _typeface?.Dispose();
        _typeface = null;
        base.Dispose();
    }

    public override BaseShape Duplicate()
    {
        return new TextDrawingShape
        {
            Rectangle = this.Rectangle,
            Text = this.Text,
            FontFamily = this.FontFamily,
            FontSize = this.FontSize,
            IsBold = this.IsBold,
            IsItalic = this.IsItalic,
            TextColor = this.TextColor,
            FillColor = this.FillColor,
            InitialSize = this.InitialSize
        };
    }
}
