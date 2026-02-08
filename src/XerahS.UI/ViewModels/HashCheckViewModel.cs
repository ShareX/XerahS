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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class HashCheckViewModel : ViewModelBase
{
    private readonly HashChecker _hashChecker = new();

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _filePath2 = string.Empty;

    [ObservableProperty]
    private HashType _selectedHashType = HashType.SHA256;

    [ObservableProperty]
    private string _resultHash = string.Empty;

    [ObservableProperty]
    private string _targetHash = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isWorking;

    [ObservableProperty]
    private string _statusText = "Select a file and click Check.";

    [ObservableProperty]
    private bool? _isMatch;

    [ObservableProperty]
    private bool _compareTwoFiles;

    /// <summary>
    /// Callback to open a file picker. Set by the tool service.
    /// </summary>
    public Func<Task<string?>>? BrowseFileRequested { get; set; }

    public HashType[] HashTypes { get; } = Enum.GetValues<HashType>();

    public HashCheckViewModel()
    {
        _hashChecker.FileCheckProgressChanged += percentage =>
        {
            Progress = Math.Clamp(percentage, 0, 100);
        };
    }

    public HashCheckViewModel(string? initialFilePath) : this()
    {
        if (!string.IsNullOrEmpty(initialFilePath))
        {
            FilePath = initialFilePath;
        }
    }

    [RelayCommand]
    private async Task BrowseFileAsync()
    {
        if (BrowseFileRequested == null) return;

        var path = await BrowseFileRequested();
        if (!string.IsNullOrEmpty(path))
        {
            FilePath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseFile2Async()
    {
        if (BrowseFileRequested == null) return;

        var path = await BrowseFileRequested();
        if (!string.IsNullOrEmpty(path))
        {
            FilePath2 = path;
        }
    }

    [RelayCommand]
    private async Task CheckHashAsync()
    {
        if (IsWorking)
        {
            _hashChecker.Stop();
            return;
        }

        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
        {
            StatusText = "File not found.";
            return;
        }

        IsWorking = true;
        IsMatch = null;
        ResultHash = string.Empty;
        Progress = 0;
        StatusText = "Computing hash...";

        try
        {
            var result = await _hashChecker.Start(FilePath, SelectedHashType);

            if (result != null)
            {
                ResultHash = result.ToUpperInvariant();

                if (CompareTwoFiles)
                {
                    await ComputeSecondFileAsync();
                }
                else
                {
                    UpdateMatchStatus();
                    StatusText = "Done.";
                }
            }
            else
            {
                StatusText = "Hash computation cancelled.";
                Progress = 0;
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Hash check");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task ComputeSecondFileAsync()
    {
        if (string.IsNullOrEmpty(FilePath2) || !File.Exists(FilePath2))
        {
            StatusText = "Second file not found.";
            UpdateMatchStatus();
            return;
        }

        Progress = 0;
        StatusText = "Computing hash for second file...";

        var result2 = await _hashChecker.Start(FilePath2, SelectedHashType);

        if (result2 != null)
        {
            TargetHash = result2.ToUpperInvariant();
            UpdateMatchStatus();
            StatusText = "Done.";
        }
        else
        {
            StatusText = "Second file hash cancelled.";
        }
    }

    [RelayCommand]
    private async Task CopyResultAsync()
    {
        if (string.IsNullOrEmpty(ResultHash)) return;

        try
        {
            await PlatformServices.Clipboard.SetTextAsync(ResultHash);
            StatusText = "Hash copied to clipboard.";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Hash copy");
            StatusText = "Failed to copy.";
        }
    }

    private void UpdateMatchStatus()
    {
        if (string.IsNullOrEmpty(ResultHash) || string.IsNullOrEmpty(TargetHash))
        {
            IsMatch = null;
            return;
        }

        IsMatch = ResultHash.Equals(TargetHash, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnResultHashChanged(string value) => UpdateMatchStatus();
    partial void OnTargetHashChanged(string value) => UpdateMatchStatus();
}
