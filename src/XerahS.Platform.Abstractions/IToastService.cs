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
/// Service for displaying in-app toast notifications.
/// Unlike INotificationService which uses OS-native notifications,
/// IToastService displays custom Avalonia-based toast windows.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Shows a toast notification with the specified configuration.
    /// If a toast is already visible, it will be closed before showing the new one.
    /// </summary>
    /// <param name="config">Toast configuration</param>
    void ShowToast(ToastConfig config);

    /// <summary>
    /// Closes any currently visible toast notification.
    /// </summary>
    void CloseActiveToast();
}
