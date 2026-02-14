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

public partial class MobileCustomUploaderConfigPage : ContentPage
{
    private readonly MobileCustomUploaderConfigViewModel _viewModel;

    public MobileCustomUploaderConfigPage()
    {
        InitializeComponent();
        _viewModel = new MobileCustomUploaderConfigViewModel();
        _viewModel.ScrollToFirstError = ScrollToFirstError;
        BindingContext = _viewModel;
    }

    private void OnUploaderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CustomUploaderListItem item)
        {
            _viewModel.SelectedUploaderIndex = _viewModel.CustomUploaders.IndexOf(item);
        }
    }

    private async void ScrollToFirstError()
    {
        if (_viewModel.HasNameError || _viewModel.HasDestinationError)
            await EditorScrollView.ScrollToAsync(BasicInfoSection, ScrollToPosition.MakeVisible, true);
        else if (_viewModel.HasUrlError)
            await EditorScrollView.ScrollToAsync(HttpRequestSection, ScrollToPosition.MakeVisible, true);
    }
}
