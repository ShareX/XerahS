using Microsoft.Win32;
using XerahS.Common;
using System.Runtime.InteropServices;

namespace XerahS.Core.Integration;

/// <summary>
/// Temporary helper for Windows integration until IIntegrationService is implemented
/// </summary>
public static class IntegrationHelper
{
    private const string ShellPluginExtensionPath = @"Software\Classes\.sxadp";
    private static readonly string ShellPluginExtensionValue = $"{SettingManager.AppName}.sxadp";
    private static readonly string ShellPluginAssociatePath = $@"Software\Classes\{ShellPluginExtensionValue}";
    private static readonly string ShellPluginAssociateValue = $"{SettingManager.AppName} plugin";
    private static readonly string ShellPluginIconPath = $@"{ShellPluginAssociatePath}\DefaultIcon";
    private static readonly string ShellPluginCommandPath = $@"{ShellPluginAssociatePath}\shell\open\command";

    private static readonly string ApplicationPath = $"\"{Environment.ProcessPath}\"";
    private static readonly string ShellPluginIconValue = $"{ApplicationPath},0"; // Extract icon from .exe
    private static readonly string ShellPluginCommandValue = $"{ApplicationPath} -InstallPlugin \"%1\"";

    /// <summary>
    /// Check if .sxadp file association is registered
    /// </summary>
    public static bool IsPluginExtensionRegistered()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            return CheckRegistryValue(ShellPluginExtensionPath, null, ShellPluginExtensionValue) &&
                   CheckRegistryValue(ShellPluginCommandPath, null, ShellPluginCommandValue);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
            return false;
        }
    }

    /// <summary>
    /// Register or unregister .sxadp file association
    /// </summary>
    public static void SetPluginExtensionRegistration(bool register)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            if (register)
            {
                UnregisterPluginExtension();
                RegisterPluginExtension();
            }
            else
            {
                UnregisterPluginExtension();
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    private static void RegisterPluginExtension()
    {
        CreateRegistryKey(ShellPluginExtensionPath, ShellPluginExtensionValue);
        CreateRegistryKey(ShellPluginAssociatePath, ShellPluginAssociateValue);
        CreateRegistryKey(ShellPluginIconPath, ShellPluginIconValue);
        CreateRegistryKey(ShellPluginCommandPath, ShellPluginCommandValue);

        // Notify Windows shell of file association change
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

        DebugHelper.WriteLine($"Registered .sxadp file association for {SettingManager.AppName}");
    }

    private static void UnregisterPluginExtension()
    {
        RemoveRegistryKey(ShellPluginExtensionPath);
        RemoveRegistryKey(ShellPluginAssociatePath);

        DebugHelper.WriteLine($"Unregistered .sxadp file association for {SettingManager.AppName}");
    }

    // Registry helper methods
    private static void CreateRegistryKey(string path, string value)
    {
        CreateRegistryKey(path, null, value);
    }

    private static void CreateRegistryKey(string path, string? name, string value)
    {
        try
        {
            using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(path))
            {
                if (rk != null)
                {
                    rk.SetValue(name ?? string.Empty, value, RegistryValueKind.String);
                }
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    private static void RemoveRegistryKey(string path)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    private static bool CheckRegistryValue(string path, string? name, string value)
    {
        try
        {
            using (RegistryKey? rk = Registry.CurrentUser.OpenSubKey(path))
            {
                if (rk != null)
                {
                    string? registryValue = rk.GetValue(name ?? string.Empty) as string;
                    return registryValue != null && registryValue.Equals(value, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }

        return false;
    }

    // P/Invoke for shell notification
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
