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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Optional one-time setup that gives XerahS direct access to the keyboard so global hotkeys work
/// everywhere (Linux: read access to keyboard event devices through a polkit prompt).
/// </summary>
public interface IHotkeyAccessSetupService
{
    /// <summary>
    /// True when hotkey delivery is degraded and this setup can fix it on the current machine.
    /// Cheap to call; performs no privileged work.
    /// </summary>
    bool IsSetupRecommended { get; }

    /// <summary>Plain-language description of what the setup will do, shown before the user runs it.</summary>
    string SetupDescription { get; }

    /// <summary>
    /// Runs the setup, which may show a system authentication prompt, and switches hotkeys to direct
    /// keyboard access when it succeeds.
    /// </summary>
    Task<HotkeyAccessSetupResult> RunSetupAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IHotkeyAccessSetupService.RunSetupAsync"/>.</summary>
public sealed record HotkeyAccessSetupResult(bool Success, string Message);
