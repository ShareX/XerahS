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
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using XerahS.Common;
using XerahS.History;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views
{
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();
            DataContext = new HistoryViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Right-click is handled natively by Avalonia ContextMenu
            var point = e.GetCurrentPoint(sender as Visual);

            if (!point.Properties.IsLeftButtonPressed)
                return;

            if (sender is not Border border || border.DataContext is not HistoryItem item)
                return;

            if (DataContext is not HistoryViewModel vm)
                return;

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+Click: Open file with default application
                DebugHelper.WriteLine($"HistoryView - Ctrl+Click: Opening file {item.FileName}");
                vm.OpenFileCommand.Execute(item);
                e.Handled = true;
            }
            else if (e.ClickCount == 1)
            {
                // Single-click: Open in Editor
                DebugHelper.WriteLine($"HistoryView - Click: Opening in editor {item.FileName}");
                await vm.EditImageCommand.ExecuteAsync(item);
                e.Handled = true;
            }
        }
    }
}
