using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Ava.UI.Views
{
    public partial class DestinationSettingsView : UserControl
    {
        public DestinationSettingsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.DestinationSettingsViewModel();
            
            // Call async Initialize when the view is loaded
            Loaded += async (s, e) =>
            {
                if (DataContext is ViewModels.DestinationSettingsViewModel vm)
                {
                    await vm.Initialize();
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
