using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XerahS.UI.Views
{
    public partial class EditorWindow : Window
    {
        public EditorWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
