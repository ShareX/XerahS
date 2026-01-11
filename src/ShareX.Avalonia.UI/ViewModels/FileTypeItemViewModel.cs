using CommunityToolkit.Mvvm.ComponentModel;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for a file type item in the selector
/// </summary>
public partial class FileTypeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _extension = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isBlocked;

    [ObservableProperty]
    private string? _blockedByInstance;

    public string DisplayText => $".{Extension}";

    public string ToolTip => IsBlocked
        ? $"Already handled by '{BlockedByInstance}'"
        : $"Select to handle .{Extension} files with this instance";
}
