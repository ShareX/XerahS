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
using Avalonia.Media;
using Avalonia.Platform.Storage;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class HashCheckWindow : Window
{
    public HashCheckWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is HashCheckViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(vm.IsMatch) or nameof(vm.IsWorking))
                {
                    UpdateUI(vm);
                }
            };
        }
    }

    private void UpdateUI(HashCheckViewModel vm)
    {
        // Update button text
        var buttonText = CheckButton.Content as TextBlock
            ?? CheckButton.FindControl<TextBlock>(null!);
        if (buttonText != null)
        {
            buttonText.Text = vm.IsWorking ? "Stop" : "Check";
        }

        // Update match indicator
        if (vm.IsMatch == true)
        {
            MatchIndicator.Text = "Match";
            MatchIndicator.Foreground = new SolidColorBrush(Color.Parse("#22C55E"));
            MatchIndicator.IsVisible = true;
        }
        else if (vm.IsMatch == false)
        {
            MatchIndicator.Text = "Mismatch";
            MatchIndicator.Foreground = new SolidColorBrush(Color.Parse("#EF4444"));
            MatchIndicator.IsVisible = true;
        }
        else
        {
            MatchIndicator.IsVisible = false;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not HashCheckViewModel vm) return;

        foreach (var item in e.DataTransfer.Items)
        {
            if (item.TryGetRaw(DataFormat.File) is IStorageFile storageFile)
            {
                vm.FilePath = storageFile.Path.LocalPath;
                break;
            }
        }
    }
}
