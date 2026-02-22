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

using System.Globalization;

namespace XerahS.Common.Utilities;

public static class SystemInfo
{
    public static T ParseEnum<T>(string value, T defaultValue = default) where T : struct, Enum
    {
        if (Enum.TryParse(value, out T result))
        {
            return result;
        }

        return defaultValue;
    }

    public static string GetTimestamp(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
    }

    public static string GetTimestamp()
    {
        return GetTimestamp(DateTime.Now);
    }

    public static bool IsWindows10OrGreater()
    {
        return OperatingSystem.IsWindowsVersionAtLeast(10, 0);
    }

    public static string GetApplicationVersion(bool includeRevision = false)
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;

        if (version == null)
        {
            return "1.0.0.0";
        }

        if (includeRevision)
        {
            return version.ToString();
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public static int CompareVersion(Version version1, Version version2, bool ignoreRevision = false)
    {
        if (version1 == null && version2 == null)
        {
            return 0;
        }

        if (version1 == null)
        {
            return -1;
        }

        if (version2 == null)
        {
            return 1;
        }

        if (ignoreRevision)
        {
            var v1 = new Version(version1.Major, version1.Minor, version1.Build);
            var v2 = new Version(version2.Major, version2.Minor, version2.Build);
            return v1.CompareTo(v2);
        }

        return version1.CompareTo(version2);
    }

    public static int CompareVersion(string version1, string version2, bool ignoreRevision = false)
    {
        if (Version.TryParse(version1, out Version? v1) && Version.TryParse(version2, out Version? v2))
        {
            return CompareVersion(v1, v2, ignoreRevision);
        }

        return string.Compare(version1, version2, StringComparison.Ordinal);
    }
}
