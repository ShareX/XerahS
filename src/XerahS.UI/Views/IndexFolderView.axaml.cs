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
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class IndexFolderView : UserControl
{
    private ContentControl? _htmlPreviewHost;
    private Control? _webViewControl;
    private PropertyInfo? _webViewSourceProperty;
    private IndexFolderViewModel? _viewModel;

    public IndexFolderView()
    {
        InitializeComponent();
        _htmlPreviewHost = this.FindControl<ContentControl>("HtmlPreviewHost");
        TryInitializeWebView();
        DataContextChanged += OnDataContextChanged;

        if (DataContext == null)
        {
            DataContext = new IndexFolderViewModel();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as IndexFolderViewModel;

        if (_viewModel != null)
        {
            _viewModel.IsWebViewAvailable = _webViewControl != null;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateWebViewSource(_viewModel.HtmlPreviewPath);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IndexFolderViewModel.HtmlPreviewPath))
        {
            UpdateWebViewSource(_viewModel?.HtmlPreviewPath);
        }
    }

    private void TryInitializeWebView()
    {
        if (_htmlPreviewHost == null)
        {
            return;
        }

        var webViewType = FindWebViewType();
        if (webViewType == null || !typeof(Control).IsAssignableFrom(webViewType))
        {
            return;
        }

        _webViewControl = (Control?)Activator.CreateInstance(webViewType);
        _webViewSourceProperty = webViewType.GetProperty("Source") ?? webViewType.GetProperty("Url");

        if (_webViewControl != null)
        {
            _htmlPreviewHost.Content = _webViewControl;
        }
    }

    private static Type? FindWebViewType()
    {
        var type = Type.GetType("WebView.Avalonia.WebView, WebView.Avalonia")
            ?? Type.GetType("WebView.Avalonia.Controls.WebView, WebView.Avalonia");

        if (type != null)
        {
            return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType("WebView.Avalonia.WebView")
                ?? assembly.GetType("WebView.Avalonia.Controls.WebView");
            if (type != null)
            {
                return type;
            }
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null)!;
                }
            })
            .FirstOrDefault(t => t?.Name == "WebView" && t.Namespace == "Avalonia.Controls");
    }

    private void UpdateWebViewSource(string? htmlPath)
    {
        if (_webViewControl == null || _webViewSourceProperty == null || string.IsNullOrWhiteSpace(htmlPath))
        {
            return;
        }

        try
        {
            object? value = htmlPath;
            if (_webViewSourceProperty.PropertyType == typeof(Uri))
            {
                value = new Uri(htmlPath);
            }
            else if (_webViewSourceProperty.PropertyType != typeof(string) && _webViewSourceProperty.PropertyType != typeof(object))
            {
                return;
            }

            _webViewSourceProperty.SetValue(_webViewControl, value);
        }
        catch
        {
            // Ignore failures to keep preview optional.
        }
    }
}
