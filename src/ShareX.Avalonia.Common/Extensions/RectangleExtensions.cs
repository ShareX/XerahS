using DrawingPoint = System.Drawing.Point;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSize = System.Drawing.Size;
using DrawingSizeF = System.Drawing.SizeF;

namespace XerahS.Common
{
    public static class RectangleExtensions
    {
        public static bool IsValid(this DrawingRectangle rect)
        {
            return rect.Width > 0 && rect.Height > 0;
        }

        public static bool IsValid(this DrawingRectangleF rect)
        {
            return rect.Width > 0 && rect.Height > 0;
        }

        public static DrawingRectangle Offset(this DrawingRectangle rect, int offset)
        {
            return new DrawingRectangle(rect.X - offset, rect.Y - offset, rect.Width + (offset * 2), rect.Height + (offset * 2));
        }

        public static DrawingRectangleF Offset(this DrawingRectangleF rect, float offset)
        {
            return new DrawingRectangleF(rect.X - offset, rect.Y - offset, rect.Width + (offset * 2), rect.Height + (offset * 2));
        }

        public static DrawingRectangleF Scale(this DrawingRectangleF rect, float scaleFactor)
        {
            return new DrawingRectangleF(rect.X * scaleFactor, rect.Y * scaleFactor, rect.Width * scaleFactor, rect.Height * scaleFactor);
        }

        public static DrawingRectangle Round(this DrawingRectangleF rect)
        {
            return DrawingRectangle.Round(rect);
        }

        public static DrawingRectangle LocationOffset(this DrawingRectangle rect, int x, int y)
        {
            return new DrawingRectangle(rect.X + x, rect.Y + y, rect.Width, rect.Height);
        }

        public static DrawingRectangleF LocationOffset(this DrawingRectangleF rect, float x, float y)
        {
            return new DrawingRectangleF(rect.X + x, rect.Y + y, rect.Width, rect.Height);
        }

        public static DrawingRectangleF LocationOffset(this DrawingRectangleF rect, DrawingPointF offset)
        {
            return rect.LocationOffset(offset.X, offset.Y);
        }

        public static DrawingRectangle LocationOffset(this DrawingRectangle rect, DrawingPoint offset)
        {
            return rect.LocationOffset(offset.X, offset.Y);
        }

        public static DrawingRectangle LocationOffset(this DrawingRectangle rect, int offset)
        {
            return rect.LocationOffset(offset, offset);
        }

        public static DrawingRectangle SizeOffset(this DrawingRectangle rect, int width, int height)
        {
            return new DrawingRectangle(rect.X, rect.Y, rect.Width + width, rect.Height + height);
        }

        public static DrawingRectangleF SizeOffset(this DrawingRectangleF rect, float width, float height)
        {
            return new DrawingRectangleF(rect.X, rect.Y, rect.Width + width, rect.Height + height);
        }

        public static DrawingRectangle SizeOffset(this DrawingRectangle rect, int offset)
        {
            return rect.SizeOffset(offset, offset);
        }

        public static DrawingRectangleF SizeOffset(this DrawingRectangleF rect, float offset)
        {
            return rect.SizeOffset(offset, offset);
        }

        public static DrawingRectangle Combine(this IEnumerable<DrawingRectangle> rects)
        {
            DrawingRectangle result = DrawingRectangle.Empty;

            foreach (DrawingRectangle rect in rects)
            {
                if (result.IsEmpty)
                {
                    result = rect;
                }
                else
                {
                    result = DrawingRectangle.Union(result, rect);
                }
            }

            return result;
        }

        public static DrawingRectangleF Combine(this IEnumerable<DrawingRectangleF> rects)
        {
            DrawingRectangleF result = DrawingRectangleF.Empty;

            foreach (DrawingRectangleF rect in rects)
            {
                if (result.IsEmpty)
                {
                    result = rect;
                }
                else
                {
                    result = DrawingRectangleF.Union(result, rect);
                }
            }

            return result;
        }

        public static DrawingRectangleF AddPoint(this DrawingRectangleF rect, DrawingPointF point)
        {
            return DrawingRectangleF.Union(rect, new DrawingRectangleF(point, new DrawingSizeF(1, 1)));
        }

        public static DrawingRectangleF CreateRectangle(this IEnumerable<DrawingPointF> points)
        {
            DrawingRectangleF result = DrawingRectangle.Empty;

            foreach (DrawingPointF point in points)
            {
                if (result.IsEmpty)
                {
                    result = new DrawingRectangleF(point, new DrawingSize(1, 1));
                }
                else
                {
                    result = result.AddPoint(point);
                }
            }

            return result;
        }

        public static DrawingPoint Center(this DrawingRectangle rect)
        {
            return new DrawingPoint(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        }

        public static DrawingPointF Center(this DrawingRectangleF rect)
        {
            return new DrawingPointF(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        }

        public static float Area(this DrawingRectangleF rect)
        {
            return rect.Width * rect.Height;
        }

        public static float Perimeter(this DrawingRectangleF rect)
        {
            return 2 * (rect.Width + rect.Height);
        }

        public static DrawingPointF Restrict(this DrawingPointF point, DrawingRectangleF rect)
        {
            point.X = Math.Max(point.X, rect.X);
            point.Y = Math.Max(point.Y, rect.Y);
            point.X = Math.Min(point.X, rect.X + rect.Width - 1);
            point.Y = Math.Min(point.Y, rect.Y + rect.Height - 1);
            return point;
        }

        public static string ToStringProper(this DrawingRectangle rect)
        {
            return $"X: {rect.X}, Y: {rect.Y}, Width: {rect.Width}, Height: {rect.Height}";
        }
    }
}
