using ShareX.Editor.Services;
using SkiaSharp;

namespace XerahS.UI.Services;

/// <summary>
/// Adapter that connects the Editor's clipboard abstraction to the platform clipboard service.
/// </summary>
public class EditorClipboardAdapter : IClipboardService
{
    public void SetImage(SKBitmap bitmap)
    {
        Platform.Abstractions.PlatformServices.Clipboard.SetImage(bitmap);
    }

    public SKBitmap? GetImage()
    {
        return Platform.Abstractions.PlatformServices.Clipboard.GetImage();
    }
}
