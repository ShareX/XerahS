using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Avalonia.UI.Views
{
    public partial class ApplicationSettingsView : UserControl
    {
        public ApplicationSettingsView()
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
