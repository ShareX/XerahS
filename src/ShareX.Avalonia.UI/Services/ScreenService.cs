using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ShareX.Ava.Platform.Abstractions;

namespace ShareX.Ava.UI.Services
{
    /// <summary>
    /// UI-layer stub for ScreenService - delegates to platform implementation
    /// This is kept as a fallback for when PlatformServices might not be fully initialized
    /// </summary>
    public class ScreenService : IScreenService
    {
        private readonly IScreenService? _platformImpl;

        public ScreenService(IScreenService? platformImpl = null)
        {
            _platformImpl = platformImpl;
        }

        private IScreenService GetImpl() => _platformImpl ?? throw new System.InvalidOperationException("No platform screen service registered");

        public Rectangle GetVirtualScreenBounds() => GetImpl().GetVirtualScreenBounds();
        public Rectangle GetWorkingArea() => GetImpl().GetWorkingArea();
        public Rectangle GetActiveScreenBounds() => GetImpl().GetActiveScreenBounds();
        public Rectangle GetActiveScreenWorkingArea() => GetImpl().GetActiveScreenWorkingArea();
        public Rectangle GetPrimaryScreenBounds() => GetImpl().GetPrimaryScreenBounds();
        public Rectangle GetPrimaryScreenWorkingArea() => GetImpl().GetPrimaryScreenWorkingArea();
        public ScreenInfo[] GetAllScreens() => GetImpl().GetAllScreens();
        public ScreenInfo GetScreenFromPoint(Point point) => GetImpl().GetScreenFromPoint(point);
        public ScreenInfo GetScreenFromRectangle(Rectangle rectangle) => GetImpl().GetScreenFromRectangle(rectangle);
    }
}
