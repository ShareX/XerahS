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
using XerahS.UI.Controls;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class TaskImageSettingsPanel : UserControl
{
    private HdrComparisonWindow? _hdrComparisonWindow;

    public TaskImageSettingsPanel()
    {
        InitializeComponent();

        var propertyGrid = this.FindControl<PropertyGrid>("EffectPropertyGrid");
        if (propertyGrid != null)
        {
            propertyGrid.PropertyValueChanged += (_, _) =>
            {
                if (DataContext is TaskSettingsViewModel vm)
                {
                    vm.ImageEffects.UpdatePreview();
                    vm.ImageEffects.NotifyEffectSettingsChanged();
                }
            };
        }
    }

    /// <summary>
    /// The comparison needs more room than the settings panel has, so it lives in its own window;
    /// pressing the button again raises the existing one instead of stacking windows.
    /// </summary>
    private void OnOpenHdrComparison(object? sender, RoutedEventArgs e)
    {
        if (_hdrComparisonWindow is { IsVisible: true })
        {
            _hdrComparisonWindow.Activate();
            return;
        }

        _hdrComparisonWindow = new HdrComparisonWindow();
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            _hdrComparisonWindow.Show(owner);
        }
        else
        {
            _hdrComparisonWindow.Show();
        }
    }
}
