using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ShareX.Ava.UI.ViewModels;

namespace ShareX.Ava.UI.Views
{
    public partial class DebugView : UserControl
    {
        public DebugView()
        {
            InitializeComponent();
            DataContext = new DebugViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
