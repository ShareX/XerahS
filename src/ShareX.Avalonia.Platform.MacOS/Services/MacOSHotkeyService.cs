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

using System;
using ShareX.Ava.Platform.Abstractions;
using DebugHelper = ShareX.Ava.Common.DebugHelper;

namespace ShareX.Ava.Platform.MacOS.Services
{
    /// <summary>
    /// macOS hotkey service stub (MVP).
    /// </summary>
    public class MacOSHotkeyService : IHotkeyService
    {
        private bool _isSuspended;
        private bool _loggedUnsupported;

        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

        public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
        {
            if (hotkeyInfo != null)
            {
                hotkeyInfo.Status = ShareX.Ava.Platform.Abstractions.HotkeyStatus.Failed;
            }

            LogUnsupported("RegisterHotkey");
            return false;
        }

        public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
        {
            if (hotkeyInfo != null)
            {
                hotkeyInfo.Status = ShareX.Ava.Platform.Abstractions.HotkeyStatus.NotConfigured;
            }

            LogUnsupported("UnregisterHotkey");
            return false;
        }

        public void UnregisterAll()
        {
            LogUnsupported("UnregisterAll");
        }

        public bool IsRegistered(HotkeyInfo hotkeyInfo)
        {
            LogUnsupported("IsRegistered");
            return false;
        }

        public bool IsSuspended
        {
            get => _isSuspended;
            set => _isSuspended = value;
        }

        public void Dispose()
        {
        }

        private void LogUnsupported(string member)
        {
            if (_loggedUnsupported)
            {
                return;
            }

            _loggedUnsupported = true;
            DebugHelper.WriteLine($"MacOSHotkeyService: {member} is not implemented yet.");
        }
    }
}
