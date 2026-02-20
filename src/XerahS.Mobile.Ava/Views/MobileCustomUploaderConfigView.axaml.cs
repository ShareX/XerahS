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
using XerahS.Mobile.Core;

namespace Ava.Views;

public partial class MobileCustomUploaderConfigView : UserControl
{
    public MobileCustomUploaderConfigView()
    {
        InitializeComponent();
        var vm = new XerahS.Mobile.Core.MobileCustomUploaderConfigViewModel();
        vm.ScrollToFirstError = ScrollToFirstError;
        DataContext = vm;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ScrollToFirstError()
    {
        var vm = DataContext as XerahS.Mobile.Core.MobileCustomUploaderConfigViewModel;
        if (vm == null) return;

        Control? target = null;

        if (vm.HasNameError || vm.HasDestinationError)
            target = this.FindControl<Border>("BasicInfoSection");
        else if (vm.HasUrlError)
            target = this.FindControl<Border>("HttpRequestSection");

        target?.BringIntoView();
    }
}
