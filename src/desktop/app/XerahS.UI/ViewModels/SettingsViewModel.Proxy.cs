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
using XerahS.Common;
using XerahS.Core;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        // Proxy Settings
        [ObservableProperty]
        private ProxyMethod _proxyMethod;

        [ObservableProperty]
        private string _proxyHost = string.Empty;

        [ObservableProperty]
        private int _proxyPort = 8080;

        [ObservableProperty]
        private string _proxyUsername = string.Empty;

        [ObservableProperty]
        private string _proxyPassword = string.Empty;

        public ProxyMethod[] ProxyMethods => (ProxyMethod[])Enum.GetValues(typeof(ProxyMethod));

        public bool IsManualProxy => ProxyMethod == ProxyMethod.Manual;

        partial void OnProxyMethodChanged(ProxyMethod value)
        {
            if (_isLoading) return;
            OnPropertyChanged(nameof(IsManualProxy));
            ApplyProxyAndResetClient();
        }

        partial void OnProxyHostChanged(string value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

        partial void OnProxyPortChanged(int value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

        partial void OnProxyUsernameChanged(string value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

        partial void OnProxyPasswordChanged(string value)
        {
            if (_isLoading) return;
            ApplyProxyAndResetClient();
        }

        private void ApplyProxyAndResetClient()
        {
            var settings = SettingsManager.Settings;

            // Update ApplicationConfig
            settings.ProxySettings.ProxyMethod = ProxyMethod;
            settings.ProxySettings.Host = ProxyHost;
            settings.ProxySettings.Port = ProxyPort;
            settings.ProxySettings.Username = ProxyUsername;
            settings.ProxySettings.Password = ProxyPassword;

            // Sync to HelpersOptions
            HelpersOptions.SyncProxyFromConfig(settings.ProxySettings);

            // Reset HttpClient to pick up new proxy
            HttpClientFactory.Reset();
        }
    }
}
