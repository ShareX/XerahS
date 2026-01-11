using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views
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
