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
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using XerahS.Mobile.UI.ViewModels;
using XerahS.Mobile.UI.Views;

namespace XerahS.Mobile.UI;

public partial class MobileApp : Application
{
    /// <summary>
    /// Callback for platform heads to push shared file paths into the Avalonia UI.
    /// Set after the framework initialization completes.
    /// </summary>
    public static Action<string[]>? OnFilesReceived { get; set; }

    private MobileUploadViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
        {
            _viewModel = new MobileUploadViewModel();
            singleView.MainView = new MobileUploadView
            {
                DataContext = _viewModel
            };

            OnFilesReceived = (paths) =>
            {
                Dispatcher.UIThread.Post(() => _viewModel.ProcessFiles(paths));
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
