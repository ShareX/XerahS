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

namespace XerahS.Common
{
    /// <summary>
    /// HDR → SDR tonemapping mode selected for a capture. Declared in the platform
    /// abstractions assembly (like <c>AfterCaptureTasks</c>) so the After Capture window
    /// contract can carry it, while keeping the <c>XerahS.Common</c> namespace stable for
    /// existing consumers (HDR_CAPTURE_PLAN §6.4).
    /// </summary>
    public enum HdrToneMappingMode // Localized
    {
        None = 0,
        // Retained for settings backward compatibility; no longer selectable in the UI.
        Auto = 1,
        TonemapReinhard = 2,
        TonemapAces = 3,
        TonemapBt2446C = 4,
        // Filmic (Hable) curve anchored at the capture's estimated SDR white level. Chosen by
        // visual assessment of real HDR photographs (best sky/highlight rendering with
        // near-reference colors); also behaves like a mild contrast curve on SDR desktops.
        TonemapHable = 5
    }
}