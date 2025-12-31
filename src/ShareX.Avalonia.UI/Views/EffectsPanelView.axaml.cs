using Avalonia.Controls;
using Avalonia.Interactivity;
using ShareX.Avalonia.UI.ViewModels;

namespace ShareX.Avalonia.UI.Views
{
    public partial class EffectsPanelView : UserControl
    {
        public EffectsPanelView()
        {
            InitializeComponent();
        }

        private void OnCategoryClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string category && DataContext is EffectsPanelViewModel vm)
            {
                vm.SelectCategoryCommand.Execute(category);
            }
        }

        private void OnResetClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is EffectsPanelViewModel vm)
            {
                vm.ResetEffectCommand.Execute(null);
            }
        }

        private void OnApplyClicked(object? sender, RoutedEventArgs e)
        {
            // Notify parent that an effect should be applied
            // This will be handled by the parent view (EditorView or MainWindow)
            if (DataContext is EffectsPanelViewModel vm && vm.SelectedEffect != null)
            {
                // TODO: Implement effect application logic
                // For now, this is a placeholder for parent view integration
            }
        }
    }
}
