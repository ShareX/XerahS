using Avalonia.Input;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

public class JobCategoryViewModel
{
    public string Name { get; }
    public ObservableCollection<HotkeyItemViewModel> Jobs { get; }

    public JobCategoryViewModel(string name, IEnumerable<HotkeyType> jobs)
    {
        Name = name;
        Jobs = new ObservableCollection<HotkeyItemViewModel>(
            jobs.Select(j => new HotkeyItemViewModel(new WorkflowSettings(j, Key.None)))
        );
    }
}
