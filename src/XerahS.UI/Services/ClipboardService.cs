using XerahS.Platform.Abstractions;
using SkiaSharp;

namespace XerahS.UI.Services
{
    public class ClipboardService : IClipboardService
    {
        public void Clear()
        {
            // Avalonia requires TopLevel access for clipboard
            // This is a stub - full implementation needs window reference
        }

        public bool ContainsText()
        {
            // Avalonia clipboard is async-only, so we return false for sync methods
            // Full implementation would need platform-specific APIs
            return false;
        }

        public bool ContainsImage()
        {
            return false;
        }

        public bool ContainsFileDropList()
        {
            return false;
        }

        public string? GetText()
        {
            // Sync method - would need platform-specific synchronous API
            return null;
        }

        public void SetText(string text)
        {
            // Fire and forget for sync API
            _ = SetTextAsync(text);
        }

        public SKBitmap? GetImage()
        {
            return null;
        }

        public void SetImage(SKBitmap image)
        {
            // TODO: Implement
        }

        public string[]? GetFileDropList()
        {
            return null;
        }

        public void SetFileDropList(string[] files)
        {
            // TODO: Implement
        }

        public object? GetData(string format)
        {
            return null;
        }

        public void SetData(string format, object data)
        {
            // TODO: Implement
        }

        public bool ContainsData(string format)
        {
            return false;
        }

        public async Task<string?> GetTextAsync()
        {
            // TODO: Implement with proper TopLevel/Window reference
            await Task.CompletedTask;
            return null;
        }

        public async Task SetTextAsync(string text)
        {
            // TODO: Implement with proper TopLevel/Window reference
            await Task.CompletedTask;
        }
    }
}
