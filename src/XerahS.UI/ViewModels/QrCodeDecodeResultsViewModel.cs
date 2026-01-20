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

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class QrCodeDecodeResultsViewModel : ViewModelBase, IDisposable
{
    private Bitmap? _capturedImage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopySelectedCommand))]
    private string? _selectedResult;

    [ObservableProperty]
    private string _statusMessage;

    public ObservableCollection<string> Results { get; }

    public Bitmap? CapturedImage
    {
        get => _capturedImage;
        private set
        {
            if (ReferenceEquals(_capturedImage, value))
            {
                return;
            }

            _capturedImage?.Dispose();
            _capturedImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCapturedImage));
        }
    }

    public bool HasResults => Results.Count > 0;

    public bool HasCapturedImage => CapturedImage != null;

    public event Action? RequestClose;

    public QrCodeDecodeResultsViewModel(IReadOnlyList<string> results, Bitmap? capturedImage)
    {
        Results = new ObservableCollection<string>(results ?? Array.Empty<string>());
        CapturedImage = capturedImage;
        _statusMessage = Results.Count == 0
            ? "No QR code data found."
            : $"Found {Results.Count} QR code(s).";

        Results.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasResults));
            CopyAllCommand.NotifyCanExecuteChanged();
        };
    }

    private bool CanCopyAll() => HasResults;

    private bool CanCopySelected() => !string.IsNullOrWhiteSpace(SelectedResult);

    [RelayCommand(CanExecute = nameof(CanCopyAll))]
    private async Task CopyAllAsync()
    {
        if (!PlatformServices.IsInitialized)
        {
            StatusMessage = "Clipboard is not available.";
            return;
        }

        var combined = string.Join(Environment.NewLine, Results);
        await PlatformServices.Clipboard.SetTextAsync(combined);
        StatusMessage = "All decoded text copied to clipboard.";
    }

    [RelayCommand(CanExecute = nameof(CanCopySelected))]
    private async Task CopySelectedAsync()
    {
        if (!PlatformServices.IsInitialized || string.IsNullOrWhiteSpace(SelectedResult))
        {
            StatusMessage = "Clipboard is not available.";
            return;
        }

        await PlatformServices.Clipboard.SetTextAsync(SelectedResult);
        StatusMessage = "Selected text copied to clipboard.";
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    public void Dispose()
    {
        _capturedImage?.Dispose();
        _capturedImage = null;
    }
}
