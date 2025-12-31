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

        public static readonly RoutedEvent<RoutedEventArgs> ApplyRequestedEvent =
            RoutedEvent.Register<EffectsPanelView, RoutedEventArgs>(nameof(ApplyRequested), RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> ApplyRequested
        {
            add => AddHandler(ApplyRequestedEvent, value);
            remove => RemoveHandler(ApplyRequestedEvent, value);
        }

        private void OnApplyClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is EffectsPanelViewModel vm && vm.SelectedEffect != null)
            {
                RaiseEvent(new RoutedEventArgs(ApplyRequestedEvent));
            }
        }
    }
}
