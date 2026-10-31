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

using System.Runtime.CompilerServices;
using XerahS.Common;

namespace XerahS.Tests;

/// <summary>
/// Points every settings, data, state and cache folder at a private temporary home before any test
/// or static constructor runs. Without this, tests that use singletons such as InstanceManager on
/// Linux read and write the user's real ~/.config/xerahs (UploadJobProcessorTests once emptied
/// uploader-instances.json). On other platforms the personal folder is redirected instead.
/// </summary>
internal static class TestSettingsIsolation
{
    internal static string RootFolder { get; private set; } = string.Empty;

    [ModuleInitializer]
    internal static void Initialize()
    {
        RootFolder = Path.Combine(Path.GetTempPath(), $"xerahs-test-home-{Environment.ProcessId}");

        if (OperatingSystem.IsLinux())
        {
            SetFolder("XDG_CONFIG_HOME", "config");
            SetFolder("XDG_DATA_HOME", "data");
            SetFolder("XDG_STATE_HOME", "state");
            SetFolder("XDG_CACHE_HOME", "cache");
        }
        else
        {
            string personal = Path.Combine(RootFolder, "XerahS");
            Directory.CreateDirectory(personal);
            PathsManager.PersonalFolder = personal;
        }
    }

    private static void SetFolder(string variable, string name)
    {
        string folder = Path.Combine(RootFolder, name);
        Directory.CreateDirectory(folder);
        Environment.SetEnvironmentVariable(variable, folder);
    }
}
