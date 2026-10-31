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
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.ViewModels;

namespace XerahS.UI.Views
{
    public partial class MainWindow
    {
        /// <summary>
        /// Esc dismisses the dialog in the modal overlay (ModalContent), like clicking
        /// the backdrop. Hosts waiting in ModalDialogHost complete with their dismiss result.
        /// </summary>
        private void OnModalEscapeKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None) return;
            if (DataContext is not MainViewModel vm || !vm.IsModalOpen) return;
            if (this.FindControl<Grid>("MainWindowModalOverlay") is not { IsVisible: true }) return;

            vm.CloseModalCommand.Execute(null);
            e.Handled = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            // Skip if typing in a text input
            if (e.Source is TextBox) return;

            // Forward Crop action to EditorView if active
            if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter && vm.ActiveTool == EditorTool.Crop)
            {
                if (_editorView != null && _editorView.IsVisible)
                {
                    _editorView.PerformCrop();
                    e.Handled = true;
                    return;
                }
            }

            // Handle Ctrl key combinations
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                switch (e.Key)
                {
                    case Key.Z:
                        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        {
                            vm.UndoCommand.Execute(null);
                            e.Handled = true;
                        }
                        return;
                    case Key.Y:
                        vm.RedoCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.C:
                        vm.CopyCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.S:
                        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                            vm.SaveAsCommand.Execute(null);
                        else
                            vm.SaveCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.O:
                        _ = OpenImageFromFileAsync();
                        e.Handled = true;
                        return;
                    case Key.OemPlus:
                    case Key.Add:
                        vm.ZoomInCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.OemMinus:
                    case Key.Subtract:
                        vm.ZoomOutCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.D0:
                        vm.ResetZoomCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.D9:
                        vm.ZoomToFitCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }

            // Tool selection shortcuts (single keys without modifiers)
            if (e.KeyModifiers == KeyModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.V:
                        vm.SelectToolCommand.Execute(EditorTool.Select);
                        e.Handled = true;
                        break;
                    case Key.R:
                        vm.SelectToolCommand.Execute(EditorTool.Rectangle);
                        e.Handled = true;
                        break;
                    case Key.E:
                        vm.SelectToolCommand.Execute(EditorTool.Ellipse);
                        e.Handled = true;
                        break;
                    case Key.A:
                        vm.SelectToolCommand.Execute(EditorTool.Arrow);
                        e.Handled = true;
                        break;
                    case Key.L:
                        vm.SelectToolCommand.Execute(EditorTool.Line);
                        e.Handled = true;
                        break;
                    case Key.T:
                        vm.SelectToolCommand.Execute(EditorTool.Text);
                        e.Handled = true;
                        break;
                    case Key.N:
                        vm.SelectToolCommand.Execute(EditorTool.Step);
                        e.Handled = true;
                        break;
                    case Key.S:
                        vm.SelectToolCommand.Execute(EditorTool.Spotlight);
                        e.Handled = true;
                        break;
                    case Key.C:
                        vm.SelectToolCommand.Execute(EditorTool.Crop);
                        e.Handled = true;
                        break;
                    case Key.Delete:
                    case Key.Back:
                        vm.DeleteSelectedCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        if (vm.IsModalOpen)
                        {
                            vm.CloseModalCommand.Execute(null);
                        }
                        else
                        {
                            vm.SelectToolCommand.Execute(EditorTool.Select);
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        private void OnBackdropPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CloseModalCommand.Execute(null);
            }
        }
    }
}
