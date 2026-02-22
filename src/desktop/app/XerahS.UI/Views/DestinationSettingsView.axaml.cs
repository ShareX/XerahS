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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using XerahS.Core;

namespace XerahS.UI.Views
{
    public partial class DestinationSettingsView : UserControl
    {
        public DestinationSettingsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.DestinationSettingsViewModel();

            if (DataContext is ViewModels.DestinationSettingsViewModel vm)
            {
                vm.ShowMessageDialog += ShowMessageDialog;
            }

            // Call async Initialize when the view is loaded
            Loaded += async (s, e) =>
            {
                if (DataContext is ViewModels.DestinationSettingsViewModel vm)
                {
                    await vm.Initialize();
                }
            };

            // Save uploaders config when navigating away from this view
            Unloaded += (s, e) =>
            {
                SettingsManager.SaveUploadersConfigAsync();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task ShowMessageDialog(string title, string message)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 500,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460
            };

            var buttonPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Padding = new Thickness(30, 8)
            };

            okButton.Click += (s, e) => messageBox.Close();

            buttonPanel.Children.Add(okButton);
            panel.Children.Add(messageText);
            panel.Children.Add(buttonPanel);
            messageBox.Content = panel;

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                messageBox.Show();
                return;
            }

            await messageBox.ShowDialog(owner);
        }
    }
}
