
using System.Diagnostics;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Services
{
    public class WindowsSystemService : ISystemService
    {
        public bool ShowFileInExplorer(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                string args = $"/select,\"{filePath.Replace('/', '\\')}\"";

                Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
                return true;
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
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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
                 Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
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
