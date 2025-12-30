using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Core.Managers;
using ShareX.Avalonia.Core.Tasks;
using ShareX.Avalonia.Core;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq; // For now
using System;
using ShareX.Avalonia.Common;

using ShareX.Avalonia.Uploaders;

namespace ShareX.Avalonia.UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        public ObservableCollection<WorkerTask> Tasks { get; } = new ObservableCollection<WorkerTask>();

        public MainViewModel()
        {
            // Sync with TaskManager. 
            // In a real app we'd bind to an event or ObservableCollection from TaskManager.
            // For now, let's just poll or link manually.
            // TaskManager doesn't expose ObservableCollection yet. 
            // Let's modify TaskManager later to support this, or just wrap it.
        }

        [RelayCommand]
        public async Task CaptureRegion()
        {
            var settings = new TaskSettings(); // Create fresh instance
            
            // Just start a task to test the pipeline
            settings.Job = HotkeyType.RectangleRegion;
            settings.AfterCaptureJob = AfterCaptureTasks.SaveImageToFile;
            
            await TaskManager.Instance.StartTask(settings);
            
            // Refresh list (temporary until we have events)
            UpdateTaskList();
        }

        [RelayCommand]
        public async Task CaptureAndUpload()
        {
             var settings = new TaskSettings();
             settings.Job = HotkeyType.RectangleRegion;
             settings.AfterCaptureJob = AfterCaptureTasks.SaveImageToFile | AfterCaptureTasks.UploadImageToHost;
             settings.ImageDestination = ImageDestination.Imgur;
             
             await TaskManager.Instance.StartTask(settings);
             UpdateTaskList();
        }

        private void UpdateTaskList() 
        {
            Tasks.Clear();
            foreach(var task in TaskManager.Instance.Tasks)
            {
                Tasks.Add(task);
            }
        }
    }
}
