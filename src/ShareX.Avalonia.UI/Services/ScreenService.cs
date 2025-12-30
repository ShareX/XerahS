using System.Collections.Generic;
using System.Drawing;
using ShareX.Avalonia.Platform.Abstractions;

namespace ShareX.Avalonia.UI.Services
{
    public class ScreenService : IScreenService
    {
        private Rectangle DefaultScreen => new Rectangle(0, 0, 1920, 1080);

        public Rectangle GetVirtualScreenBounds()
        {
            // TODO: Implement using Avalonia's screen API
            return DefaultScreen;
        }

        public Rectangle GetWorkingArea()
        {
            // TODO: Implement using Avalonia's screen API
            return DefaultScreen;
        }

        public Rectangle GetActiveScreenBounds()
        {
            // TODO: Implement using Avalonia's screen API
            return DefaultScreen;
        }

        public Rectangle GetActiveScreenWorkingArea()
        {
            // TODO: Implement using Avalonia's screen API
            return DefaultScreen;
        }

        public Rectangle GetPrimaryScreenBounds()
        {
            // TODO: Implement using Avalonia's screen API
            return DefaultScreen;
        }

        public Rectangle GetPrimaryScreenWorkingArea()
        {
            // TODO: Implement using Avalonia's screen API
            return DefaultScreen;
        }

        public ScreenInfo[] GetAllScreens()
        {
            // TODO: Implement using Avalonia's screen API
            return new[]
            {
                new ScreenInfo
                {
                    Bounds = DefaultScreen,
                    WorkingArea = DefaultScreen,
                    IsPrimary = true,
                    DeviceName = "Primary",
                    BitsPerPixel = 32
                }
            };
        }

        public ScreenInfo GetScreenFromPoint(Point point)
        {
            // TODO: Implement using Avalonia's screen API
            return GetAllScreens()[0];
        }

        public ScreenInfo GetScreenFromRectangle(Rectangle rectangle)
        {
            // TODO: Implement using Avalonia's screen API
            return GetAllScreens()[0];
        }
    }
}
