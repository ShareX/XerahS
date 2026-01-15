using CommunityToolkit.Mvvm.ComponentModel;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for displaying validation warnings or conflict messages
/// </summary>
public partial class ConflictWarningViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private string _icon = "⚠️";

    public void SetWarning(string? validationError)
    {
        if (!string.IsNullOrEmpty(validationError))
        {
            HasConflict = true;
            Message = validationError;
        }
        else
        {
            HasConflict = false;
            Message = string.Empty;
        }
    }
}
