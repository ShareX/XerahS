#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Platform-specific service responsible for registering the application at login.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Checks whether the platform entry is currently configured to run at startup.
    /// </summary>
    bool IsRunAtStartupEnabled();

    /// <summary>
    /// Adds or removes the run-at-startup entry.
    /// </summary>
    /// <param name="enable">True to enable auto-start, false to disable.</param>
    /// <returns>True if the platform state matches the requested option.</returns>
    bool SetRunAtStartup(bool enable);
}

public sealed class UnsupportedStartupService : IStartupService
{
    public bool IsRunAtStartupEnabled() => false;
    public bool SetRunAtStartup(bool enable) => false;
}
