using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Core;
using ShareX.Ava.Common;
using ShareX.Ava.Core.Tasks;
using ShareX.Ava.History;

namespace ShareX.Ava.UI.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase
    {
        // Converter for view toggle button text
        public static IValueConverter ViewToggleConverter { get; } = new FuncValueConverter<bool, string>(
            isGrid => isGrid ? "📋 List View" : "🔲 Grid View");

        [ObservableProperty]
        private ObservableCollection<HistoryItem> _historyItems;

        [ObservableProperty]
        private bool _isGridView = true;

        private readonly HistoryManager _historyManager;

        public HistoryViewModel()
        {
            HistoryItems = new ObservableCollection<HistoryItem>();
            
            // Create history manager with centralized path and debug logging
            var historyPath = Path.Combine(SettingManager.SettingsFolder, ShareXResources.HistoryFileName);
            
            System.Diagnostics.Debug.WriteLine($"Trace: HistoryViewModel - Initializing with path: {historyPath}");

            _historyManager = new HistoryManagerXML(historyPath);
            
            LoadHistory();
        }

        [RelayCommand]
        private void LoadHistory()
        {
            System.Diagnostics.Debug.WriteLine("Trace: HistoryViewModel - Loading history items...");
            
            // Load from HistoryManager
            var items = _historyManager.GetHistoryItems();
            System.Diagnostics.Debug.WriteLine($"Trace: HistoryViewModel - Items loaded from manager: {items.Count}");

            HistoryItems.Clear();
            foreach (var item in items)
            {
                HistoryItems.Add(item);
            }
            System.Diagnostics.Debug.WriteLine($"Trace: HistoryViewModel - Items added to ObservableCollection: {HistoryItems.Count}");
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

        [RelayCommand]
        private async Task EditImage(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!System.IO.File.Exists(item.FilePath)) return;

            try
            {
                // Load the image from file
                using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);
                var image = System.Drawing.Image.FromStream(fs);
                
                // Open in Editor using the platform service
                await ShareX.Ava.Platform.Abstractions.PlatformServices.UI.ShowEditorAsync(image);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open image in editor: {ex.Message}");
            }
        }
    }
}
