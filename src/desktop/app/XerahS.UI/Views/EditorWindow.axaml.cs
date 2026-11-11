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
using ShareX.ImageEditor.Presentation.ViewModels;

namespace XerahS.UI.Views
{
    public partial class EditorWindow : SurfaceWindow
    {
        private MainViewModel? _viewModel;
        private bool _allowClose;
        internal bool IsCloseRequestedByViewModel { get; private set; }

        /// <summary>
        /// When true, closing the window from its title bar runs the editor's Continue action
        /// instead of Cancel, so the after-capture tasks still save and copy the image, as in ShareX (#316).
        /// The explicit Cancel action still stops the workflow.
        /// </summary>
        internal bool CloseContinuesWorkflow { get; set; }

        public EditorWindow()
        {
            InitializeComponent();
            Classes.Remove("xerahs-surface");
            Classes.Add("xerahs-editor-host");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= OnViewModelCloseRequested;
            }

            base.OnDataContextChanged(e);

            IsCloseRequestedByViewModel = false;
            _allowClose = false;
            _viewModel = DataContext as MainViewModel;
            if (_viewModel != null)
            {
                _viewModel.CloseRequested += OnViewModelCloseRequested;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= OnViewModelCloseRequested;
                _viewModel = null;
            }

            _allowClose = false;
            base.OnClosed(e);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (_allowClose || _viewModel == null)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            if (CloseContinuesWorkflow && !_viewModel.IsModalOpen)
            {
                _viewModel.ContinueCommand.Execute(null);
                return;
            }

            _viewModel.RequestClose();
        }

        private void OnViewModelCloseRequested(object? sender, EventArgs e)
        {
            _allowClose = true;
            IsCloseRequestedByViewModel = true;
            Close();
        }
    }
}
