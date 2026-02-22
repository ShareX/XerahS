namespace XerahS.Mobile.Maui.Views;

public partial class LoadingPage : ContentPage
{
    public LoadingPage()
    {
        InitializeComponent();
        
        var random = new Random();
        var c1 = (char)random.Next('A', 'Z' + 1);
        var c2 = (char)random.Next('A', 'Z' + 1);
        BuildIdLabel.Text = $"{c1}{c2}";
    }
}
