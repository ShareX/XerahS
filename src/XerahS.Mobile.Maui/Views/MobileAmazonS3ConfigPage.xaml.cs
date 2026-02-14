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

using XerahS.Mobile.Maui.ViewModels;

namespace XerahS.Mobile.Maui.Views;

public partial class MobileAmazonS3ConfigPage : ContentPage
{
    public MobileAmazonS3ConfigPage()
    {
        InitializeComponent();
        var vm = new MobileAmazonS3ConfigViewModel();
        vm.ScrollToFirstError = ScrollToFirstError;
        BindingContext = vm;
    }

    private async void ScrollToFirstError()
    {
        var vm = BindingContext as MobileAmazonS3ConfigViewModel;
        if (vm == null) return;

        View? target = null;

        if (vm.HasAccessKeyError || vm.HasSecretKeyError)
            target = AuthSection;
        else if (vm.HasBucketError || vm.HasRegionError)
            target = BucketSection;

        if (target != null)
            await MainScrollView.ScrollToAsync(target, ScrollToPosition.MakeVisible, true);
    }
}
