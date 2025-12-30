using System.Threading.Tasks;
using System.Drawing;

namespace ShareX.Avalonia.Platform.Abstractions
{
    public interface IScreenCaptureService
    {
        /// <summary>
        /// Captures a region of the screen.
        /// </summary>
        /// <returns>System.Drawing.Image if successful, null otherwise.</returns>
        Task<System.Drawing.Image?> CaptureRegionAsync();
    }
}
