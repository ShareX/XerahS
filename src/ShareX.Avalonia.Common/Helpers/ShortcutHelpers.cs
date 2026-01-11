using System.Runtime.InteropServices;

namespace XerahS.Common.Helpers
{
    public static class ShortcutHelpers
    {
        public static bool SetShortcut(bool create, Environment.SpecialFolder specialFolder, string shortcutName, string targetPath, string arguments = "")
        {
            string shortcutPath = GetShortcutPath(specialFolder, shortcutName);
            return SetShortcut(create, shortcutPath, targetPath, arguments);
        }

        public static bool SetShortcut(bool create, string shortcutPath, string targetPath, string arguments = "")
        {
            try
            {
                if (create)
                {
                    return CreateShortcut(shortcutPath, targetPath, arguments);
                }
                else
                {
                    return DeleteShortcut(shortcutPath);
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
                e.ShowError();
            }

            return false;
        }

        public static bool CheckShortcut(Environment.SpecialFolder specialFolder, string shortcutName, string targetPath)
        {
            string shortcutPath = GetShortcutPath(specialFolder, shortcutName);
            return CheckShortcut(shortcutPath, targetPath);
        }

        public static bool CheckShortcut(string shortcutPath, string targetPath)
        {
            if (!string.IsNullOrEmpty(shortcutPath) && !string.IsNullOrEmpty(targetPath) && File.Exists(shortcutPath))
            {
                try
                {
                    string shortcutTargetPath = GetShortcutTargetPath(shortcutPath);
                    return !string.IsNullOrEmpty(shortcutTargetPath) && shortcutTargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception e)
                {
                    DebugHelper.WriteException(e);
                }
            }

            return false;
        }

        private static string GetShortcutPath(Environment.SpecialFolder specialFolder, string shortcutName)
        {
            string folderPath = Environment.GetFolderPath(specialFolder);

            if (!shortcutName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                shortcutName += ".lnk";
            }

            return Path.Combine(folderPath, shortcutName);
        }

        private static bool CreateShortcut(string shortcutPath, string targetPath, string arguments = "")
        {
            // TODO: [Avalonia] Shortcuts (.lnk) are Windows specific. 
            // Consider alternatives for Linux/macOS (.desktop files / aliases).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!string.IsNullOrEmpty(shortcutPath) && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                {
                    DeleteShortcut(shortcutPath);

                    try
                    {
                        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                        {
                            dynamic shell = Activator.CreateInstance(shellType);
                            dynamic shortcut = shell.CreateShortcut(shortcutPath);
                            shortcut.TargetPath = targetPath;
                            shortcut.Arguments = arguments;
                            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                            shortcut.Save();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex);
                    }
                }
            }

            return false;
        }

        private static string GetShortcutTargetPath(string shortcutPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        return shortcut.TargetPath;
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex);
                }
            }
            return null;
        }

        private static bool DeleteShortcut(string shortcutPath)
        {
            if (!string.IsNullOrEmpty(shortcutPath) && File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                return true;
            }

            return false;
        }
    }
}
