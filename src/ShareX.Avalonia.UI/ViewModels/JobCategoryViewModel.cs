using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Ava.Core;
using ShareX.Ava.Core.Hotkeys;

namespace ShareX.Ava.UI.ViewModels;

public class JobCategoryViewModel
{
    public string Name { get; }
    public ObservableCollection<HotkeyItemViewModel> Jobs { get; }

    public JobCategoryViewModel(string name, IEnumerable<HotkeyType> jobs)
    {
        Name = name;
        Jobs = new ObservableCollection<HotkeyItemViewModel>(
            jobs.Select(j => new HotkeyItemViewModel(new HotkeySettings(j, Key.None)))
        );
    }
}
