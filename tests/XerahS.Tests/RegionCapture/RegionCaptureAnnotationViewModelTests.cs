using Avalonia.Media;
using NUnit.Framework;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Hosting;
using SkiaSharp;
using XerahS.RegionCapture.ViewModels;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class RegionCaptureAnnotationViewModelTests
{
    [Test]
    public void EmojiTool_IsHiddenAndCannotBeSelected()
    {
        var viewModel = new RegionCaptureAnnotationViewModel
        {
            ActiveTool = EditorTool.Rectangle
        };

        viewModel.SelectToolCommand.Execute(EditorTool.Emoji);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.VisibleToolbarItems.Any(item => item.Tool == EditorTool.Emoji), Is.False);
            Assert.That(viewModel.ActiveTool, Is.EqualTo(EditorTool.Rectangle));
        });
    }

    [Test]
    public void RectangleTool_Shows_RectangleOptions()
    {
        var viewModel = new RegionCaptureAnnotationViewModel
        {
            ActiveTool = EditorTool.Rectangle
        };

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowBorderColor, Is.True);
            Assert.That(viewModel.ShowFillColor, Is.True);
            Assert.That(viewModel.ShowThickness, Is.True);
            Assert.That(viewModel.ShowCornerRadius, Is.True);
            Assert.That(viewModel.ShowTextColor, Is.False);
            Assert.That(viewModel.ShowTextStyle, Is.False);
            Assert.That(viewModel.ShowStrength, Is.False);
        });
    }

    [Test]
    public void HighlightTool_OnlyShows_FillColor()
    {
        var viewModel = new RegionCaptureAnnotationViewModel
        {
            ActiveTool = EditorTool.Highlight
        };

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowBorderColor, Is.False);
            Assert.That(viewModel.ShowFillColor, Is.True);
            Assert.That(viewModel.ShowThickness, Is.False);
            Assert.That(viewModel.ShowStrength, Is.False);
        });
    }

    [Test]
    public void SelectTool_Mirrors_SelectedAnnotationOptions()
    {
        var viewModel = new RegionCaptureAnnotationViewModel
        {
            ActiveTool = EditorTool.Select
        };

        viewModel.SelectedAnnotation = new TextAnnotation
        {
            TextColor = "#FF00FF00",
            FontSize = 32,
            IsBold = true,
            IsItalic = true,
            StrokeWidth = 7,
            StrokeColor = "#FF112233"
        };

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ActiveToolName, Is.EqualTo("Text"));
            Assert.That(viewModel.ShowBorderColor, Is.True);
            Assert.That(viewModel.ShowTextColor, Is.True);
            Assert.That(viewModel.ShowThickness, Is.True);
            Assert.That(viewModel.ShowFontSize, Is.True);
            Assert.That(viewModel.ShowTextStyle, Is.True);
            Assert.That(viewModel.ShowFillColor, Is.False);
            Assert.That(viewModel.TextBold, Is.True);
            Assert.That(viewModel.TextItalic, Is.True);
            Assert.That(viewModel.TextColor, Is.EqualTo("#FF00FF00"));
            Assert.That(viewModel.StrokeWidth, Is.EqualTo(7));
        });
    }

    [Test]
    public void ToolSwitch_Loads_ToolSpecificDefaults()
    {
        var options = new ImageEditorOptions
        {
            BorderColor = Colors.Red,
            FillColor = Colors.Transparent,
            TextBorderColor = Colors.Blue,
            TextTextColor = Colors.Yellow,
            TextThickness = 9,
            TextFontSize = 54,
            TextBold = false,
            TextItalic = true,
            HighlightFillColor = Colors.Lime,
            StepBorderColor = Colors.Black,
            StepFillColor = Colors.White,
            StepTextColor = Colors.Orange,
            StepThickness = 6,
            StepFontSize = 28,
            SpotlightStrength = 42
        };

        var viewModel = new RegionCaptureAnnotationViewModel();
        viewModel.LoadOptions(options);

        viewModel.ActiveTool = EditorTool.Text;
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedColor, Is.EqualTo("#FF0000FF"));
            Assert.That(viewModel.TextColor, Is.EqualTo("#FFFFFF00"));
            Assert.That(viewModel.StrokeWidth, Is.EqualTo(9));
            Assert.That(viewModel.FontSize, Is.EqualTo(54));
            Assert.That(viewModel.TextBold, Is.False);
            Assert.That(viewModel.TextItalic, Is.True);
        });

        viewModel.ActiveTool = EditorTool.Highlight;
        Assert.That(viewModel.FillColor, Is.EqualTo("#FF00FF00"));

        viewModel.ActiveTool = EditorTool.Step;
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedColor, Is.EqualTo("#FF000000"));
            Assert.That(viewModel.FillColor, Is.EqualTo("#FFFFFFFF"));
            Assert.That(viewModel.TextColor, Is.EqualTo("#FFFFA500"));
            Assert.That(viewModel.StrokeWidth, Is.EqualTo(6));
            Assert.That(viewModel.FontSize, Is.EqualTo(28));
        });

        viewModel.ActiveTool = EditorTool.Spotlight;
        Assert.That(viewModel.EffectStrength, Is.EqualTo(42));
    }

    [Test]
    public void SelectTool_ChangingSelectedEffectStrength_RegeneratesEffectBitmap()
    {
        var viewModel = new RegionCaptureAnnotationViewModel
        {
            ActiveTool = EditorTool.Select
        };

        var sourceBitmap = new SKBitmap(24, 24);
        using (var canvas = new SKCanvas(sourceBitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        viewModel.LoadBackgroundImage(sourceBitmap);

        var blurAnnotation = new BlurAnnotation
        {
            StartPoint = new SKPoint(2, 2),
            EndPoint = new SKPoint(18, 18),
            Amount = 8
        };

        viewModel.SelectedAnnotation = blurAnnotation;
        viewModel.EffectStrength = 20;

        Assert.Multiple(() =>
        {
            Assert.That(blurAnnotation.Amount, Is.EqualTo(20));
            Assert.That(blurAnnotation.EffectBitmap, Is.Not.Null);
        });
    }
}
