
using System.Diagnostics;
using System.IO;
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
                if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri))
                {
                    if (TryShowItemsViaDbus(uri.AbsoluteUri))
                    {
                        return true;
                    }
                }

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

        private static bool TryShowItemsViaDbus(string fileUri)
        {
            try
            {
                string escaped = fileUri.Replace("\"", "\\\"");
                string args = $"--session --print-reply --type=method_call --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"{escaped}\" string:\"\"";

                using var process = Process.Start(new ProcessStartInfo("dbus-send", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process == null)
                {
                    return false;
                }

                if (!process.WaitForExit(2000))
                {
                    process.Kill();
                    process.WaitForExit();
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
    }
}
