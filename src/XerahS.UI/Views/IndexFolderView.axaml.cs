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
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Diagnostics;
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
    private bool _webViewInitAttempted;

    public IndexFolderView()
    {
        InitializeComponent();
        _htmlPreviewHost = this.FindControl<ContentControl>("HtmlPreviewHost");
        _ = TryInitializeWebViewAsync();
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

    private async System.Threading.Tasks.Task TryInitializeWebViewAsync()
    {
        if (_webViewInitAttempted)
        {
            return;
        }

        _webViewInitAttempted = true;

        if (_htmlPreviewHost == null)
        {
            return;
        }

        if (OperatingSystem.IsLinux() && !string.Equals(Environment.GetEnvironmentVariable("XERAHS_ENABLE_WEBVIEW"), "1", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            // Run WebView discovery and instantiation fully in background without blocking UI
            var webViewType = await System.Threading.Tasks.Task.Run(FindWebViewType).ConfigureAwait(false);
            if (webViewType == null || !typeof(Control).IsAssignableFrom(webViewType))
            {
                return;
            }

            // Instantiate WebView on UI thread without timeout/blocking
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    _webViewControl = (Control?)Activator.CreateInstance(webViewType);
                    _webViewSourceProperty = webViewType.GetProperty("Source") ?? webViewType.GetProperty("Url");

                    if (_webViewControl != null && _htmlPreviewHost != null)
                    {
                        _htmlPreviewHost.Content = _webViewControl;
                    }
                }
                catch
                {
                    // Silently fail - WebView is optional
                    _webViewControl = null;
                }
            }, DispatcherPriority.Background);
        }
        catch
        {
            // Silently fail - WebView is optional
        }
    }

    private static Type? FindWebViewType()
    {
        // Try expected types first (fast path)
        var type = Type.GetType("WebView.Avalonia.WebView, WebView.Avalonia");
        if (type != null && typeof(Control).IsAssignableFrom(type))
        {
            return type;
        }

        type = Type.GetType("Avalonia.Controls.WebView, Avalonia.WebView");
        if (type != null && typeof(Control).IsAssignableFrom(type))
        {
            return type;
        }

        type = Type.GetType("AvaloniaWebView.WebView, AvaloniaWebView");
        if (type != null && typeof(Control).IsAssignableFrom(type))
        {
            return type;
        }

        // Fallback: scan WebView assemblies only (avoid scanning all assemblies)
        try
        {
            var webViewAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.Contains("WebView", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var assembly in webViewAssemblies)
            {
                try
                {
                    var foundType = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == "WebView" &&
                                           t.IsPublic &&
                                           !t.IsAbstract &&
                                           typeof(Control).IsAssignableFrom(t));
                    if (foundType != null)
                    {
                        return foundType;
                    }
                }
                catch
                {
                    // Skip assemblies that can't be scanned
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return null;
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
        catch (Exception ex)
        {
             Console.WriteLine($"IndexFolderView: Error in UpdateWebViewSource: {ex}");
        }
    }
}
