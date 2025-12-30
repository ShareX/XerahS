
#if NET6_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace ShareX.Avalonia.Common.Helpers
{
    public static class ClipboardHelpers
    {
        // TODO: [Avalonia] Clipboard APIs are async (GetTextAsync, SetTextAsync). 
        // ShareX original code is synchronous (Clipboard.GetText, SetText).
        // This class needs a major refactor to support async patterns up the stack.
        // For now, we provide stubs or limited functionality.

        // TODO: [Avalonia] 'IDataObject' is different in Avalonia (Avalonia.Input.IDataObject).
        
        // TODO: [Avalonia] System.Windows.Forms.Clipboard.SetAudio, SetData, SetFileDropList need Avalonia equivalents (some exist, some don't).

        public static void SetText(string text)
        {
            // Requires async context. Fire and forget or block? Blocking is dangerous on UI thread.
            // TODO: Implement async clipboard setting
        }

        public static string GetText()
        {
            // TODO: Implement async clipboard getting
            return null;
        }

        public static bool ContainsText()
        {
            // TODO: Check if Avalonia clipboard contains text
            return false;
        }

        public static bool ContainsImage()
        {
            // TODO: Check if Avalonia clipboard contains image
            return false;         
        }

        public static bool ContainsFileDropList()
        {
            // TODO: Check if Avalonia clipboard contains files
            return false;
        }

        public static string[] GetFileDropList()
        {
             // TODO: Get file drop list async
             return null;
        }

        // ... Add other methods as TODOs
    }
}
