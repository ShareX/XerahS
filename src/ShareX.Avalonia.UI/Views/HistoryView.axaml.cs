using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ShareX.Ava.History;
using ShareX.Ava.UI.ViewModels;

namespace ShareX.Ava.UI.Views
{
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Double-click to open in editor
            if (e.ClickCount == 2 && sender is Border border && border.DataContext is HistoryItem item)
            {
                if (DataContext is HistoryViewModel vm)
                {
                    await vm.EditImageCommand.ExecuteAsync(item);
                }
            }
        }
    }
}
