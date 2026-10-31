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
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using XerahS.History;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views
{
    public partial class HistoryView : PageView
    {
        private readonly ListBox? _gridHistoryListBox;
        private readonly ListBox? _listHistoryListBox;
        private bool _isSynchronizingSelection;

        public HistoryView()
        {
            InitializeComponent();
            _gridHistoryListBox = this.FindControl<ListBox>("GridHistoryListBox");
            _listHistoryListBox = this.FindControl<ListBox>("ListHistoryListBox");
            DataContext = UiViewModelFactoryAccessor.GetRequired().CreateHistoryViewModel();
        }

        private void OnStatisticsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            (TopLevel.GetTopLevel(this) as MainWindow)?.NavigateToStatistics();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnHistorySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isSynchronizingSelection || sender is not ListBox sourceListBox || DataContext is not HistoryViewModel vm)
            {
                return;
            }

            var selectedItems = sourceListBox.SelectedItems?.OfType<HistoryItem>().ToList() ?? new List<HistoryItem>();
            vm.SetSelectedHistoryItems(selectedItems);

            _isSynchronizingSelection = true;

            try
            {
                SyncSelection(_gridHistoryListBox, sourceListBox, selectedItems);
                SyncSelection(_listHistoryListBox, sourceListBox, selectedItems);
            }
            finally
            {
                _isSynchronizingSelection = false;
            }
        }

        private async void OnItemDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Border border || border.DataContext is not HistoryItem item || DataContext is not HistoryViewModel vm)
            {
                return;
            }

            await vm.EditImageCommand.ExecuteAsync(item);
            e.Handled = true;
        }

        private static void SyncSelection(ListBox? targetListBox, ListBox sourceListBox, IReadOnlyCollection<HistoryItem> selectedItems)
        {
            if (targetListBox == null || ReferenceEquals(targetListBox, sourceListBox) || targetListBox.SelectedItems == null)
            {
                return;
            }

            targetListBox.SelectedItems.Clear();

            foreach (var selectedItem in selectedItems)
            {
                targetListBox.SelectedItems.Add(selectedItem);
            }
        }
    }
}
