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
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Shows how global hotkeys are delivered on Linux and offers the one-time keyboard access setup
/// (<see cref="IHotkeyAccessSetupService"/>) when delivery is degraded.
/// </summary>
public partial class HotkeyDeliveryViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _backendName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWarning))]
    private string? _warningText;

    [ObservableProperty]
    private bool _showAccessSetup;

    [ObservableProperty]
    private string? _accessSetupDescription;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccessSetupStatus))]
    private string? _accessSetupStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GrantKeyboardAccessCommand))]
    private bool _isAccessSetupRunning;

    public HotkeyDeliveryViewModel()
    {
        Refresh();
    }

    public bool ShowWarning => !string.IsNullOrWhiteSpace(WarningText);

    public bool HasAccessSetupStatus => !string.IsNullOrWhiteSpace(AccessSetupStatus);

    /// <summary>Reads the current backend and whether keyboard access setup should be offered.</summary>
    public void Refresh()
    {
        if (!OperatingSystem.IsLinux() || !PlatformServices.IsInitialized)
        {
            BackendName = null;
            WarningText = null;
            ShowAccessSetup = false;
            AccessSetupDescription = null;
            return;
        }

        HotkeyDiagnostics diagnostics = PlatformServices.Hotkey.GetDiagnostics();
        BackendName = diagnostics.BackendName;
        WarningText = diagnostics.UserFacingWarning;

        IHotkeyAccessSetupService? setup = PlatformServices.HotkeyAccessSetup;
        ShowAccessSetup = setup?.IsSetupRecommended == true;
        AccessSetupDescription = ShowAccessSetup ? setup!.SetupDescription : null;
    }

    private bool CanGrantKeyboardAccess() => !IsAccessSetupRunning;

    [RelayCommand(CanExecute = nameof(CanGrantKeyboardAccess))]
    private async Task GrantKeyboardAccessAsync()
    {
        IHotkeyAccessSetupService? setup = PlatformServices.HotkeyAccessSetup;
        if (setup == null)
        {
            return;
        }

        IsAccessSetupRunning = true;
        AccessSetupStatus = "Waiting for authentication…";
        try
        {
            HotkeyAccessSetupResult result = await setup.RunSetupAsync();
            AccessSetupStatus = result.Message;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "HotkeyDelivery: keyboard access setup failed");
            AccessSetupStatus = $"Keyboard access setup failed: {ex.Message}";
        }
        finally
        {
            IsAccessSetupRunning = false;
            Refresh();
        }
    }
}
