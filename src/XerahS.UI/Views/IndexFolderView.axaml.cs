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
        if (webViewType == null) 
        {
            Console.WriteLine("IndexFolderView: webViewType is null - WebView type found");
            return;
        }
        
        if (!typeof(Control).IsAssignableFrom(webViewType))
        {
            Console.WriteLine($"IndexFolderView: webViewType {webViewType.FullName} is not assignable to Control");
            return;
        }

        try 
        {
             Console.WriteLine($"IndexFolderView: Attempting to instantiate {webViewType.FullName}");
            _webViewControl = (Control?)Activator.CreateInstance(webViewType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IndexFolderView: Failed to instantiate WebView: {ex}");
            return;
        }

        _webViewSourceProperty = webViewType.GetProperty("Source") ?? webViewType.GetProperty("Url");
        
        if (_webViewControl != null)
        {
            _htmlPreviewHost.Content = _webViewControl;
        }
    }

    private static Type? FindWebViewType()
    {
        // Debug: Log all WebView-related types in loaded assemblies
        Console.WriteLine("IndexFolderView: Searching for WebView types in loaded assemblies...");
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name?.Contains("WebView") == true))
        {
            Console.WriteLine($"  Assembly: {asm.GetName().Name}");
            try
            {
                foreach (var t in asm.GetTypes().Where(t => t.Name.Contains("WebView") && t.IsPublic && !t.IsAbstract))
                {
                    Console.WriteLine($"    Type: {t.FullName}, IsControl: {typeof(Avalonia.Controls.Control).IsAssignableFrom(t)}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"    Error: {ex.Message}"); }
        }

        // Try expected types
        var type = Type.GetType("WebView.Avalonia.WebView, WebView.Avalonia");
        if (type != null) { Console.WriteLine($"  Found via Type.GetType: {type.FullName}"); return type; }
        
        type = Type.GetType("Avalonia.Controls.WebView, Avalonia.WebView");
        if (type != null) { Console.WriteLine($"  Found via Type.GetType: {type.FullName}"); return type; }

        // Fallback: scan all assemblies
        var foundType = AppDomain.CurrentDomain.GetAssemblies()
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
            .FirstOrDefault(t => t?.Name == "WebView" && typeof(Avalonia.Controls.Control).IsAssignableFrom(t));
            
        if (foundType != null)
            Console.WriteLine($"  Found via scan: {foundType.FullName}");
        else
            Console.WriteLine("  No WebView type found");
            
        return foundType;
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
