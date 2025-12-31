using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Common;
using ShareX.Avalonia.Uploaders;

namespace ShareX.Avalonia.UI.ViewModels;

public partial class DestinationSettingsViewModel : ViewModelBase
{
    public ObservableCollection<DestinationCategory> Categories { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfigureCommand))]
    private DestinationCategory? _selectedCategory;

    [ObservableProperty]
    private UploaderItemViewModel? _selectedUploader;

    public DestinationSettingsViewModel()
    {
        LoadCategories();
    }

    private void LoadCategories()
    {
        // Image Uploaders
        var imageCategory = new DestinationCategory("Image Uploaders");
        foreach (ImageDestination dest in Enum.GetValues(typeof(ImageDestination)))
        {
            imageCategory.Uploaders.Add(new UploaderItemViewModel(dest.ToString(), dest));
        }
        Categories.Add(imageCategory);

        // Text Uploaders
        var textCategory = new DestinationCategory("Text Uploaders");
        foreach (TextDestination dest in Enum.GetValues(typeof(TextDestination)))
        {
            textCategory.Uploaders.Add(new UploaderItemViewModel(dest.ToString(), dest));
        }
        Categories.Add(textCategory);

        // File Uploaders
        var fileCategory = new DestinationCategory("File Uploaders");
        foreach (FileDestination dest in Enum.GetValues(typeof(FileDestination)))
        {
            fileCategory.Uploaders.Add(new UploaderItemViewModel(dest.ToString(), dest));
        }
        Categories.Add(fileCategory);

        // URL Shorteners
        var urlCategory = new DestinationCategory("URL Shorteners");
        foreach (UrlShortenerType dest in Enum.GetValues(typeof(UrlShortenerType)))
        {
            urlCategory.Uploaders.Add(new UploaderItemViewModel(dest.ToString(), dest));
        }
        Categories.Add(urlCategory);

        // Select first category by default
        SelectedCategory = Categories.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanConfigure))]
    private void Configure()
    {
        // TODO: Open configuration dialog for selected uploader
    }

    private bool CanConfigure() => SelectedCategory != null;

    partial void OnSelectedCategoryChanged(DestinationCategory? value)
    {
        // Select first uploader when category changes
        SelectedUploader = value?.Uploaders.FirstOrDefault();
    }
}

public class DestinationCategory
{
    public string Name { get; set; }
    public ObservableCollection<UploaderItemViewModel> Uploaders { get; } = new();

    public DestinationCategory(string name)
    {
        Name = name;
    }
}

public partial class UploaderItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private Enum _enumValue;

    public UploaderItemViewModel(string name, Enum enumValue)
    {
        _name = name;
        _enumValue = enumValue;
    }
}
