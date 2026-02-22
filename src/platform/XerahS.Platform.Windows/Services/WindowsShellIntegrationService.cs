#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Microsoft.Win32;
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Services;

/// <summary>
/// Windows implementation of shell integration services (file extension registration, etc.)
/// </summary>
public sealed class WindowsShellIntegrationService : IShellIntegrationService
{
    private const string ShellPluginExtensionPath = @"Software\Classes\.xsdp";
    private readonly string ShellPluginExtensionValue = $"{AppResources.AppName}.xsdp";
    private readonly string ShellPluginAssociatePath;
    private readonly string ShellPluginAssociateValue;
    private readonly string ShellPluginIconPath;
    private readonly string ShellPluginCommandPath;

    private readonly string ApplicationPath;
    private readonly string ShellPluginIconValue;
    private readonly string ShellPluginCommandValue;

    public WindowsShellIntegrationService()
    {
        ShellPluginAssociatePath = $@"Software\Classes\{ShellPluginExtensionValue}";
        ShellPluginAssociateValue = $"{AppResources.AppName} plugin";
        ShellPluginIconPath = $@"{ShellPluginAssociatePath}\DefaultIcon";
        ShellPluginCommandPath = $@"{ShellPluginAssociatePath}\shell\open\command";

        ApplicationPath = $"\"{Environment.ProcessPath}\"";
        ShellPluginIconValue = $"{ApplicationPath},0"; // Extract icon from .exe
        ShellPluginCommandValue = $"{ApplicationPath} -InstallPlugin \"%1\"";
    }

    /// <summary>
    /// Check if .xsdp file association is registered
    /// </summary>
    public bool IsPluginExtensionRegistered()
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
    /// Register or unregister .xsdp file association
    /// </summary>
    public void SetPluginExtensionRegistration(bool register)
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

    private void RegisterPluginExtension()
    {
        CreateRegistryKey(ShellPluginExtensionPath, ShellPluginExtensionValue);
        CreateRegistryKey(ShellPluginAssociatePath, ShellPluginAssociateValue);
        CreateRegistryKey(ShellPluginIconPath, ShellPluginIconValue);
        CreateRegistryKey(ShellPluginCommandPath, ShellPluginCommandValue);

        // Notify Windows shell of file association change
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

        DebugHelper.WriteLine($"Registered .xsdp file association for {AppResources.AppName}");
    }

    private void UnregisterPluginExtension()
    {
        RemoveRegistryKey(ShellPluginExtensionPath);
        RemoveRegistryKey(ShellPluginAssociatePath);

        DebugHelper.WriteLine($"Unregistered .xsdp file association for {AppResources.AppName}");
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
