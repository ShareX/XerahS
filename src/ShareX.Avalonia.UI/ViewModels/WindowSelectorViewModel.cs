using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Platform.Abstractions;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

public partial class WindowSelectorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<WindowInfo> _windows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private WindowInfo? _selectedWindow;

    public Action<WindowInfo>? OnWindowSelected { get; set; }
    public Action? OnCancelled { get; set; }

    public WindowSelectorViewModel()
    {
        RefreshWindows();
    }

    [RelayCommand]
    public void RefreshWindows()
    {
        Windows.Clear();
        if (PlatformServices.IsInitialized && PlatformServices.Window != null)
        {
            var myHandle = PlatformServices.Window.GetForegroundWindow(); // Assuming this is mostly correct context or handled elsewhere

            try
            {
                var allWindows = PlatformServices.Window.GetAllWindows();
                // Filter visible windows with titles
                // ShareX also filters "Progman", "Button" but usually Title check covers emptiness.
                var filtered = allWindows
                    .Where(w => w.IsVisible && !string.IsNullOrWhiteSpace(w.Title))
                    .OrderBy(w => w.Title);

                foreach (var w in filtered)
                {
                    Windows.Add(w);
                }
            }
            catch (Exception)
            {
                // Log?
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedWindow != null)
        {
            OnWindowSelected?.Invoke(SelectedWindow);
        }
    }

    private bool CanConfirm() => SelectedWindow != null;

    [RelayCommand]
    private void Cancel()
    {
        OnCancelled?.Invoke();
    }
}
