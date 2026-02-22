using SkiaSharp;
using System.Drawing;

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Represents a snapshot of the mouse cursor.
    /// </summary>
    public class CursorInfo
    {
        public SKBitmap Image { get; set; }
        public Point Position { get; set; }
        public Point Hotspot { get; set; }

        public CursorInfo(SKBitmap image, Point position, Point hotspot)
        {
            Image = image;
            Position = position;
            Hotspot = hotspot;
        }
    }
}
