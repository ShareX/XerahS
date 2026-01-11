using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace XerahS.UI.Views
{
    public partial class ApplicationSettingsView : UserControl
    {
        private TextBox? _debugTextBox;

        public ApplicationSettingsView()
        {
            InitializeComponent();
            var vm = new ViewModels.SettingsViewModel();
            DataContext = vm;

            // Wire up the edit requester
            vm.HotkeySettings.EditHotkeyRequester = async (settings) =>
            {
                var editVm = new ViewModels.WorkflowEditorViewModel(settings);
                var dialog = new WorkflowEditorView
                {
                    DataContext = editVm
                };

                if (VisualRoot is Window window)
                {
                    return await dialog.ShowDialog<bool>(window);
                }

                return false;
            };

            // Find debug TextBox and connect it to the HotkeySelectionControl's static debug log
            Loaded += (s, e) =>
            {
                _debugTextBox = this.FindControl<TextBox>("DebugLogTextBox");
                if (_debugTextBox != null)
                {
                    // Set up the debug log callback
                    Controls.HotkeySelectionControl.SetDebugCallback((msg) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _debugTextBox.Text = (_debugTextBox.Text ?? "") + msg + "\n";
                            _debugTextBox.CaretIndex = _debugTextBox.Text?.Length ?? 0;
                        });
                    });

                    _debugTextBox.Text = "Debug log initialized. Try clicking a hotkey button...\n";
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
