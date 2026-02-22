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
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace XerahS.UI.Views
{
    public partial class HotkeySettingsView : UserControl
    {
        private TextBox? _debugTextBox;

        public HotkeySettingsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.HotkeySettingsViewModel();

            // Find debug TextBox and connect it to the HotkeySelectionControl's static debug log
            Loaded += (s, e) =>
            {
                _debugTextBox = this.FindControl<TextBox>("DebugLogTextBox");
                if (_debugTextBox != null)
                {
                    // Set up the debug log callback
                    Controls.HotkeySelectionControl.SetDebugCallback((msg) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _debugTextBox.Text = (_debugTextBox.Text ?? "") + msg + "\n";
                            _debugTextBox.CaretIndex = _debugTextBox.Text?.Length ?? 0;
                        });
                    });

                    _debugTextBox.Text = "Debug log initialized. Try clicking a hotkey button...\n";
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
