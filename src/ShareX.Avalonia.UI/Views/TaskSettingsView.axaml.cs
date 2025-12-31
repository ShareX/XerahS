using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Avalonia.UI.Views
{
    public partial class TaskSettingsView : UserControl
    {
        public TaskSettingsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.SettingsViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
