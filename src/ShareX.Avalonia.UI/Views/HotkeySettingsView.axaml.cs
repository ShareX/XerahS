using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Avalonia.UI.Views
{
    public partial class HotkeySettingsView : UserControl
    {
        public HotkeySettingsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.HotkeySettingsViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
