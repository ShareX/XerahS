using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using XerahS.Core;

namespace XerahS.UI.Views
{
    public partial class DestinationSettingsView : UserControl
    {
        public DestinationSettingsView()
        {
            InitializeComponent();
            DataContext = new ViewModels.DestinationSettingsViewModel();

            if (DataContext is ViewModels.DestinationSettingsViewModel vm)
            {
                vm.ShowMessageDialog += ShowMessageDialog;
            }

            // Call async Initialize when the view is loaded
            Loaded += async (s, e) =>
            {
                if (DataContext is ViewModels.DestinationSettingsViewModel vm)
                {
                    await vm.Initialize();
                }
            };

            // Save uploaders config when navigating away from this view
            Unloaded += (s, e) =>
            {
                SettingsManager.SaveUploadersConfigAsync();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task ShowMessageDialog(string title, string message)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 500,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460
            };

            var buttonPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Padding = new Thickness(30, 8)
            };

            okButton.Click += (s, e) => messageBox.Close();

            buttonPanel.Children.Add(okButton);
            panel.Children.Add(messageText);
            panel.Children.Add(buttonPanel);
            messageBox.Content = panel;

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                messageBox.Show();
                return;
            }

            await messageBox.ShowDialog(owner);
        }
    }
}
