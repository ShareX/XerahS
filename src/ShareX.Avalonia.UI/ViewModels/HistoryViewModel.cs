using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Core.Tasks;
using ShareX.Avalonia.History;

namespace ShareX.Avalonia.UI.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<HistoryItem> _historyItems;

        [ObservableProperty]
        private bool _isGridView = true;

        private readonly HistoryManager _historyManager;

        public HistoryViewModel()
        {
            HistoryItems = new ObservableCollection<HistoryItem>();
            
            // Create history manager with default path
            var historyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ShareX", "History.xml");
            
            _historyManager = new HistoryManagerXML(historyPath);
            
            LoadHistory();
        }

        [RelayCommand]
        private void LoadHistory()
        {
            // Load from HistoryManager
            var items = _historyManager.GetHistoryItems();
            
            HistoryItems.Clear();
            foreach (var item in items)
            {
                HistoryItems.Add(item);
            }
        }

        [RelayCommand]
        private void ToggleView()
        {
            IsGridView = !IsGridView;
        }

        [RelayCommand]
        private void RefreshHistory()
        {
            LoadHistory();
        }
    }
}
