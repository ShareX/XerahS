using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
using XerahS.UI;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.Tests.Privacy;
public class PrivacyReviewViewTests
{
    [AvaloniaTest]
    public void IncompleteScanDisablesUploadAndShowsLocalAnnotationChoice()
    {
        using var image = new SKBitmap(640, 300);
        image.Erase(SKColors.White);
        using (var canvas = new SKCanvas(image))
        using (var font = new SKFont(SKTypeface.Default, 24))
        using (var paint = new SKPaint { Color = SKColors.Black })
            canvas.DrawText("synthetic@example.test", 20, 60, SKTextAlign.Left, font, paint);
        using var vm = new PrivacyReviewViewModel(image, image, new(PrivacyScanStatus.Incomplete, []), "Synthetic Host",
            new(), new XerahS.CLI.Services.HeadlessUIService());
        var view = new ViewLocator().Build(vm);
        Assert.That(view, Is.TypeOf<PrivacyReviewView>());
        var window = new Window { Width = 1000, Height = 750, Content = view };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
            Assert.That(buttons.Single(b => b.Content?.ToString() == "Approve image and upload").IsEnabled, Is.False);
            Assert.That(buttons.Any(b => b.Content?.ToString() == "Annotate / review manually"), Is.True);
            Assert.That(vm.CanUpload, Is.False);
            string? screenshot = Environment.GetEnvironmentVariable("XERAHS_PRIVACY_SCREENSHOT");
            if (screenshot != null) window.CaptureRenderedFrame()?.Save(screenshot,
                global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        finally { window.Close(); }
    }
}
