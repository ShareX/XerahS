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
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views
{
    public partial class DebugView : PageView
    {
        public DebugView()
            : this(0)
        {
        }

        public DebugView(int initialTabIndex)
        {
            InitializeComponent();
            DataContext = new DebugViewModel();

            var tabs = this.FindControl<TabControl>("DebugTabs");
            if (tabs != null && initialTabIndex >= 0 && initialTabIndex < tabs.Items.Count)
            {
                tabs.SelectedIndex = initialTabIndex;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnDebugTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl { SelectedIndex: 1 })
            {
                this.FindControl<DisplayDiagnosticsView>("DisplayDiagnosticsPanel")?.RefreshReport();
            }
        }

        private async void OnShareLogsClick(object? sender, RoutedEventArgs e)
        {
            // Modal overlay in the main window, like the app's other dialogs.
            await UiViewModelFactoryAccessor.GetRequired().ViewDialogService.ShowShareLogsAsync(new ShareLogsViewModel());
        }
    }
}
