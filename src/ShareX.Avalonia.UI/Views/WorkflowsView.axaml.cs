using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShareX.Avalonia.UI.Views
{
    public partial class WorkflowsView : UserControl
    {
        public WorkflowsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
