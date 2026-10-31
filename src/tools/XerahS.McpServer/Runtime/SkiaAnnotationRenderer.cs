using System.Globalization;
using System.Text.Json.Nodes;
using SkiaSharp;
using XerahS.Core.Services.Privacy;

namespace XerahS.McpServer.Runtime;

internal static class SkiaAnnotationRenderer
{
    public static IReadOnlyList<string> ApplyAnnotations(SKBitmap bitmap, JsonArray annotations)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(annotations);

        List<string> applied = [];

        using var canvas = new SKCanvas(bitmap);
        canvas.Save();

        foreach (var annotationNode in annotations)
        {
            if (annotationNode is not JsonObject annotation)
            {
                continue;
            }

            var type = ReadString(annotation, "type")?.Trim().ToLowerInvariant();
            var parameters = annotation["params"] as JsonObject ?? [];
            if (string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            switch (type)
            {
                case "arrow":
                    DrawArrow(canvas, parameters);
                    applied.Add(type);
                    break;
                case "rectangle":
                    DrawRectangle(canvas, parameters);
                    applied.Add(type);
                    break;
                case "ellipse":
                    DrawEllipse(canvas, parameters);
                    applied.Add(type);
                    break;
                case "line":
                    DrawLine(canvas, parameters);
                    applied.Add(type);
                    break;
                case "text":
                    DrawText(canvas, parameters);
                    applied.Add(type);
                    break;
                case "freehand":
                    DrawFreehand(canvas, parameters);
                    applied.Add(type);
                    break;
                case "blur":
                    ApplyBlur(bitmap, parameters);
                    applied.Add(type);
                    break;
                case "pixelate":
                    ApplyPixelate(bitmap, parameters);
                    applied.Add(type);
                    break;
                case "step":
                    DrawStep(canvas, parameters);
                    applied.Add(type);
                    break;
            }
        }

        canvas.Restore();
        return applied;
    }

    private static void DrawArrow(SKCanvas canvas, JsonObject parameters)
    {
        var start = ReadPoint(parameters, "x1", "y1");
        var end = ReadPoint(parameters, "x2", "y2");
        var color = ReadColor(parameters, "color", SKColors.Red);
        var thickness = ReadFloat(parameters, "thickness", 4f);

        using var linePaint = CreateStrokePaint(color, thickness);
        canvas.DrawLine(start, end, linePaint);

        var direction = new SKPoint(end.X - start.X, end.Y - start.Y);
        var length = MathF.Max(1f, MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
        var unit = new SKPoint(direction.X / length, direction.Y / length);
        var perpendicular = new SKPoint(-unit.Y, unit.X);
        var headLength = MathF.Max(12f, thickness * 4f);
        var headWidth = MathF.Max(8f, thickness * 2.5f);

        var basePoint = new SKPoint(end.X - unit.X * headLength, end.Y - unit.Y * headLength);
        var left = new SKPoint(basePoint.X + perpendicular.X * headWidth, basePoint.Y + perpendicular.Y * headWidth);
        var right = new SKPoint(basePoint.X - perpendicular.X * headWidth, basePoint.Y - perpendicular.Y * headWidth);

        using var headPaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(end);
        pathBuilder.LineTo(left);
        pathBuilder.LineTo(right);
        pathBuilder.Close();
        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, headPaint);
    }

    private static void DrawRectangle(SKCanvas canvas, JsonObject parameters)
    {
        var rect = ReadRect(parameters);
        var color = ReadColor(parameters, "color", SKColors.Red);
        var thickness = ReadFloat(parameters, "thickness", 4f);
        var fill = ReadBool(parameters, "fill");

        using var paint = fill
            ? new SKPaint { Color = color.WithAlpha(64), IsAntialias = true, Style = SKPaintStyle.Fill }
            : CreateStrokePaint(color, thickness);

        canvas.DrawRect(rect, paint);
    }

    private static void DrawEllipse(SKCanvas canvas, JsonObject parameters)
    {
        var rect = ReadRect(parameters);
        var color = ReadColor(parameters, "color", SKColors.Red);
        var thickness = ReadFloat(parameters, "thickness", 4f);
        var fill = ReadBool(parameters, "fill");

        using var paint = fill
            ? new SKPaint { Color = color.WithAlpha(64), IsAntialias = true, Style = SKPaintStyle.Fill }
            : CreateStrokePaint(color, thickness);

        canvas.DrawOval(rect, paint);
    }

    private static void DrawLine(SKCanvas canvas, JsonObject parameters)
    {
        var start = ReadPoint(parameters, "x1", "y1");
        var end = ReadPoint(parameters, "x2", "y2");
        var color = ReadColor(parameters, "color", SKColors.Red);
        var thickness = ReadFloat(parameters, "thickness", 4f);

        using var paint = CreateStrokePaint(color, thickness);
        canvas.DrawLine(start, end, paint);
    }

    private static void DrawText(SKCanvas canvas, JsonObject parameters)
    {
        var x = ReadFloat(parameters, "x");
        var y = ReadFloat(parameters, "y");
        var text = ReadString(parameters, "text") ?? string.Empty;
        var color = ReadColor(parameters, "color", SKColors.Red);
        var fontSize = ReadFloat(parameters, "font_size", 18f);
        var fontFamily = ReadString(parameters, "font_family");
        using var typeface = string.IsNullOrWhiteSpace(fontFamily) ? SKTypeface.Default : SKTypeface.FromFamilyName(fontFamily);
        using var font = new SKFont(typeface, fontSize);

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true
        };

        using var outline = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(160),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(2f, fontSize / 8f)
        };

        canvas.DrawText(text, x, y, SKTextAlign.Left, font, outline);
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    private static void DrawFreehand(SKCanvas canvas, JsonObject parameters)
    {
        if (parameters["points"] is not JsonArray points || points.Count < 2)
        {
            return;
        }

        var color = ReadColor(parameters, "color", SKColors.Red);
        var thickness = ReadFloat(parameters, "thickness", 4f);

        using var paint = CreateStrokePaint(color, thickness);
        using var pathBuilder = new SKPathBuilder();

        var first = points[0] as JsonObject;
        if (first == null)
        {
            return;
        }

        pathBuilder.MoveTo(ReadFloat(first, "x"), ReadFloat(first, "y"));

        for (var index = 1; index < points.Count; index++)
        {
            if (points[index] is JsonObject point)
            {
                pathBuilder.LineTo(ReadFloat(point, "x"), ReadFloat(point, "y"));
            }
        }

        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, paint);
    }

    private static void ApplyBlur(SKBitmap bitmap, JsonObject parameters) =>
        ApplyRedaction(bitmap, parameters, PrivacyRedactionEffect.Blur, ReadFloat(parameters, "radius", 12f));

    private static void ApplyPixelate(SKBitmap bitmap, JsonObject parameters) =>
        ApplyRedaction(bitmap, parameters, PrivacyRedactionEffect.Pixelate, ReadFloat(parameters, "pixel_size", 10f));

    private static void ApplyRedaction(SKBitmap bitmap, JsonObject parameters, PrivacyRedactionEffect effect, float strength)
    {
        var rect = ClampRect(ReadRect(parameters), bitmap.Width, bitmap.Height);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        if (!float.IsFinite(strength)) throw new ArgumentException("Invalid redaction strength.");
        RegionRedactionRenderer.Apply(bitmap, new SKRectI((int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
            (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom)), effect, (int)Math.Clamp(strength, 1, 100));
    }

    private static void DrawStep(SKCanvas canvas, JsonObject parameters)
    {
        var x = ReadFloat(parameters, "x");
        var y = ReadFloat(parameters, "y");
        var number = ReadInt(parameters, "number", 1);
        var color = ReadColor(parameters, "color", SKColors.Red);
        var radius = ReadFloat(parameters, "radius", 16f);
        using var font = new SKFont(SKTypeface.Default, radius);

        using var fillPaint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = MathF.Max(2f, radius / 4f) };
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        canvas.DrawCircle(x, y, radius, fillPaint);
        canvas.DrawCircle(x, y, radius, strokePaint);

        var metrics = font.Metrics;
        var baseline = y - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(number.ToString(CultureInfo.InvariantCulture), x, baseline, SKTextAlign.Center, font, textPaint);
    }

    private static SKPaint CreateStrokePaint(SKColor color, float thickness)
    {
        return new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = thickness,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };
    }

    private static SKRect ReadRect(JsonObject parameters)
    {
        var x = ReadFloat(parameters, "x");
        var y = ReadFloat(parameters, "y");
        var width = ReadFloat(parameters, "width");
        var height = ReadFloat(parameters, "height");
        return new SKRect(x, y, x + width, y + height);
    }

    private static SKRect ClampRect(SKRect rect, int maxWidth, int maxHeight)
    {
        var left = Math.Clamp(rect.Left, 0, maxWidth);
        var top = Math.Clamp(rect.Top, 0, maxHeight);
        var right = Math.Clamp(rect.Right, 0, maxWidth);
        var bottom = Math.Clamp(rect.Bottom, 0, maxHeight);
        return new SKRect(left, top, right, bottom);
    }

    private static SKPoint ReadPoint(JsonObject parameters, string xName, string yName) =>
        new(ReadFloat(parameters, xName), ReadFloat(parameters, yName));

    private static SKColor ReadColor(JsonObject parameters, string name, SKColor fallback)
    {
        var value = ReadString(parameters, name);
        return !string.IsNullOrWhiteSpace(value) && SKColor.TryParse(value, out var color)
            ? color
            : fallback;
    }

    private static string? ReadString(JsonObject parameters, string name)
    {
        if (parameters[name] is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue.ToString(CultureInfo.InvariantCulture);
            }

            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue.ToString(CultureInfo.InvariantCulture);
            }

            if (value.TryGetValue<float>(out var floatValue))
            {
                return floatValue.ToString(CultureInfo.InvariantCulture);
            }

            if (value.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            }

            if (value.TryGetValue<bool>(out var boolValue))
            {
                return boolValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static int ReadInt(JsonObject parameters, string name, int fallback = 0)
    {
        if (parameters[name] is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<long>(out var longValue) && longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                return (int)longValue;
            }

            if (value.TryGetValue<double>(out var doubleValue) && doubleValue >= int.MinValue && doubleValue <= int.MaxValue)
            {
                return (int)doubleValue;
            }

            if (value.TryGetValue<string>(out var stringValue) &&
                int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static float ReadFloat(JsonObject parameters, string name, float fallback = 0f)
    {
        if (parameters[name] is JsonValue value)
        {
            if (value.TryGetValue<float>(out var floatValue))
            {
                return floatValue;
            }

            if (value.TryGetValue<double>(out var doubleValue))
            {
                return (float)doubleValue;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<string>(out var stringValue) &&
                float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static bool ReadBool(JsonObject parameters, string name, bool fallback = false)
    {
        if (parameters[name] is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (value.TryGetValue<string>(out var stringValue) &&
                bool.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }
}
