#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using System.Diagnostics;
using XerahS.Common;
using XerahS.Services.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Simple notification service using notify-send when available.
/// </summary>
public sealed class LinuxNotificationService : INotificationService
{
    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        if (!TryNotifySend(title, message))
        {
            DebugHelper.WriteLine($"[Notification] {title}: {message}");
        }
    }

    public void ShowNotification(string title, string message, string actionText, Action action, NotificationType type = NotificationType.Info)
    {
        if (!TryNotifySend(title, $"{message} ({actionText})"))
        {
            DebugHelper.WriteLine($"[Notification] {title}: {message} (Action: {actionText})");
        }
    }

    private static bool TryNotifySend(string title, string message)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = $"\"{title}\" \"{message}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
