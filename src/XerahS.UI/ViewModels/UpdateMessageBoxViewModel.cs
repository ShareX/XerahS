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
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;

namespace XerahS.UI.ViewModels;

public partial class UpdateMessageBoxViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private bool _isPreRelease;

    [ObservableProperty]
    private bool _isPortable;

    [ObservableProperty]
    private bool _isDev;

    public Action<bool?>? RequestClose { get; set; }

    public string TitleText => $"Update available for {AppResources.AppName}";

    public string CurrentVersionText => $"Current version: {CurrentVersion}";

    public string LatestVersionText
    {
        get
        {
            var text = $"Latest version: {LatestVersion}";
            if (IsDev) text += " Dev";
            if (IsPreRelease) text += " (Pre-release)";
            return text;
        }
    }

    public string PortableNotice => IsPortable
        ? $"Since you are using the portable version of {AppResources.AppName}, the download link will open in your browser."
        : "Would you like to download and install this update?";

    [RelayCommand]
    private void Yes()
    {
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void No()
    {
        RequestClose?.Invoke(false);
    }
}
