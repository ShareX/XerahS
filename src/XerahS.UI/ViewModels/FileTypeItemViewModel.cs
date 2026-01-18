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
using CommunityToolkit.Mvvm.ComponentModel;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for a file type item in the selector
/// </summary>
public partial class FileTypeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _extension = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isBlocked;

    [ObservableProperty]
    private string? _blockedByInstance;

    public string DisplayText => $".{Extension}";

    public string ToolTip => IsBlocked
        ? $"Already handled by '{BlockedByInstance}'"
        : $"Select to handle .{Extension} files with this instance";
}
