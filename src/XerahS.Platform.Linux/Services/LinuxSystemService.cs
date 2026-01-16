
using System.Diagnostics;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services
{
    public class LinuxSystemService : ISystemService
    {
        public bool ShowFileInExplorer(string filePath)
        {
             if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                // Linux selecting file is not standardized. Open parent dir.
                string? folderPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    Process.Start(new ProcessStartInfo("xdg-open", $"\"{folderPath}\"") { UseShellExecute = true });
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return false;
        }

         public bool OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            try
            {
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{url}\"") { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            return false;
        }

        public bool OpenFile(string filePath)
        {
             if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

             try
             {
                 Process.Start(new ProcessStartInfo("xdg-open", $"\"{filePath}\"") { UseShellExecute = true });
                 return true;
             }
             catch (Exception ex)
             {
                 Debug.WriteLine(ex);
             }
             return false;
        }
    }
}
