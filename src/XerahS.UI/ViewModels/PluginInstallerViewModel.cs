#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.ViewModels;

public partial class PluginInstallerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _packageFilePath = string.Empty;

    [ObservableProperty]
    private PluginManifest? _manifest;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isInstalling;

    public Action<bool?>? RequestClose { get; set; }

    public bool CanInstall => Manifest != null && !IsInstalling;

    public string ManifestVersionAuthor =>
        Manifest != null ? $"Version {Manifest.Version} by {Manifest.Author}" : string.Empty;

    public string ManifestCategories =>
        Manifest != null ? $"Categories: {string.Join(", ", Manifest.SupportedCategories)}" : string.Empty;

    [RelayCommand]
    private async Task BrowsePackage()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel == null)
        {
            ErrorMessage = "Unable to find the main window.";
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Plugin Package",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType($"{SettingsManager.AppName} Plugin")
                {
                    Patterns = new[] { "*.sxadp" }
                }
            }
        });

        if (files.Count > 0)
        {
            PackageFilePath = files[0].Path.LocalPath;
            await LoadManifestPreview();
        }
    }

    private async Task LoadManifestPreview()
    {
        ErrorMessage = null;
        Manifest = null;

        try
        {
            Manifest = PluginPackager.PreviewPackage(PackageFilePath);
            if (Manifest == null)
            {
                ErrorMessage = "Invalid package: plugin.json not found";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load package: {ex.Message}";
        }

        OnPropertyChanged(nameof(CanInstall));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Install()
    {
        if (Manifest == null || string.IsNullOrWhiteSpace(PackageFilePath))
        {
            ErrorMessage = "Please select a valid package.";
            return;
        }

        IsInstalling = true;
        ErrorMessage = null;

        try
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            Directory.CreateDirectory(pluginsDir);

            var metadata = PluginPackager.InstallPackage(PackageFilePath, pluginsDir);

            if (metadata != null)
            {
                ProviderCatalog.LoadPlugins(pluginsDir, forceReload: true);
                RequestClose?.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Installation failed: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
