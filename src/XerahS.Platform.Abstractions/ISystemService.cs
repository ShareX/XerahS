
namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Service for system-level operations like file explorer, URL opening, etc.
    /// </summary>
    public interface ISystemService
    {
        /// <summary>
        /// Opens the file explorer with the specified file selected.
        /// </summary>
        /// <param name="filePath">The full path to the file.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ShowFileInExplorer(string filePath);

        /// <summary>
        /// Opens the specified URL in the default browser.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool OpenUrl(string url);

        /// <summary>
        /// Opens the specified file using the default application.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool OpenFile(string filePath);
    }
}
