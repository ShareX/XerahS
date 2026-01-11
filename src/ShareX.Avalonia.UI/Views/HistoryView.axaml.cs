using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using XerahS.Common;
using XerahS.History;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views
{
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();
            DataContext = new HistoryViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Right-click is handled natively by Avalonia ContextMenu
            var point = e.GetCurrentPoint(sender as Visual);

            if (!point.Properties.IsLeftButtonPressed)
                return;

            if (sender is not Border border || border.DataContext is not HistoryItem item)
                return;

            if (DataContext is not HistoryViewModel vm)
                return;

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+Click: Open file with default application
                DebugHelper.WriteLine($"HistoryView - Ctrl+Click: Opening file {item.FileName}");
                vm.OpenFileCommand.Execute(item);
                e.Handled = true;
            }
            else if (e.ClickCount == 1)
            {
                // Single-click: Open in Editor
                DebugHelper.WriteLine($"HistoryView - Click: Opening in editor {item.FileName}");
                await vm.EditImageCommand.ExecuteAsync(item);
                e.Handled = true;
            }
        }
    }
}
