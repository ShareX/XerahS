using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks.Processors;
using XerahS.Core.Tasks.Pipeline;
using XerahS.Platform.Abstractions;
using XerahS.Tests.Xip0052;
using XerahS.UI.Services;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using HostEditorWindow = XerahS.UI.Views.EditorWindow;
using Point = Avalonia.Point;

namespace XerahS.Tests.Editor;

[TestFixture, NonParallelizable]
public class EditorWorkflowSnapshotTests
{
    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public async Task Cancel_StopsWorkflowBeforeSavingOrUploadingOriginal(bool closeWindow)
    {
        string folder = Path.Combine(Path.GetTempPath(), "xerahs-annotation-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var info = new TaskInfo(new TaskSettings
        {
            AfterCaptureJob = AfterCaptureTasks.AnnotateMedia | AfterCaptureTasks.SaveImageToFile | AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.UploadImageToHost,
            OverrideScreenshotsFolder = true, ScreenshotsFolder = folder
        });
        info.Metadata.Image = new SKBitmap(80, 60);
        info.Metadata.Image.Erase(SKColors.White);
        PlatformServices.RegisterUIService(new AvaloniaUIService(new FakeDesktopTaskManager()));
        using var opened = Window.WindowOpenedEvent.AddClassHandler<HostEditorWindow>((window, _) =>
            Dispatcher.UIThread.Post(() =>
            {
                if (closeWindow) window.Close();
                else ((MainViewModel)window.DataContext!).CancelCommand.Execute(null);
            }));
        try
        {
            var context = new PipelineContext { Info = info };
            var workflow = new FinalizationStage().ExecuteAsync(context, CancellationToken.None);
            Dispatcher.UIThread.RunJobs();
            Assert.That(await workflow, Is.EqualTo(PipelineStageResult.Stop));
            Assert.That(context.Status, Is.EqualTo(XerahS.Core.TaskStatus.Canceled));
            Assert.That(Directory.GetFiles(folder), Is.Empty);
            Assert.That(info.Result.URL, Is.Null.Or.Empty);
        }
        finally
        {
            PlatformServices.Reset();
            info.Metadata.Dispose();
            Directory.Delete(folder, true);
        }
    }

    [AvaloniaTest]
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task Upload_DoesNotFallBackToCleanPreview(bool snapshotCallback, bool hasAnnotations)
    {
        using var image = new SKBitmap(80, 60);
        image.Erase(SKColors.White);
        using var vm = new MainViewModel();
        vm.UpdatePreview(image);
        vm.HasAnnotations = hasAnnotations;
        var taskManager = new FakeDesktopTaskManager();
        MainViewModelHelper.WireUploadRequested(vm, taskManager, snapshotCallback ? () => null : null);

        await vm.UploadCommand.ExecuteAsync(null);

        Assert.That(taskManager.StartImageUploadTaskCalls, Is.Zero, "An annotated export cannot use the clean preview as a fallback.");
    }

    [AvaloniaTest]
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task Continue_SavesAndUploadsFlattenedAnnotations(bool existingFile, bool saveToFile)
    {
        string folder = Path.Combine(Path.GetTempPath(), "xerahs-annotation-workflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var provider = new SnapshotProvider();
        if (ProviderCatalog.GetProvider(provider.ProviderId) == null) ProviderCatalog.RegisterProvider(provider);
        var destination = new UploaderInstance { ProviderId = provider.ProviderId, Category = UploaderCategory.Image };
        InstanceManager.Instance.AddInstance(destination);
        var info = new TaskInfo(new TaskSettings
        {
            Job = WorkflowType.PrintScreen,
            AfterCaptureJob = AfterCaptureTasks.AnnotateMedia | AfterCaptureTasks.UploadImageToHost |
                (saveToFile ? AfterCaptureTasks.SaveImageToFile : AfterCaptureTasks.None),
            AfterUploadJob = AfterUploadTasks.None,
            DestinationInstanceId = destination.InstanceId,
            OverrideScreenshotsFolder = true, ScreenshotsFolder = folder
        }) { DataType = EDataType.Image };
        info.Metadata.Image = new SKBitmap(80, 60);
        info.Metadata.Image.Erase(SKColors.White);
        string originalPath = Path.Combine(folder, "original.png");
        if (existingFile)
        {
            using var data = info.Metadata.Image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(originalPath, data.ToArray());
            info.FilePath = originalPath;
        }
        SnapshotUploader.Bytes = null;
        var previousUi = PlatformServices.IsInitialized ? PlatformServices.UI : null;
        PlatformServices.RegisterUIService(new AvaloniaUIService(new FakeDesktopTaskManager()));
        using var opened = AnnotateAndContinue();
        try
        {
            var workflow = new CaptureJobProcessor().ProcessAsync(info, CancellationToken.None);
            Dispatcher.UIThread.RunJobs();
            Assert.That(await workflow, Is.True);
            Assert.That(info.Result.URL, Is.EqualTo("https://example.test/annotated.png"));
            Assert.That(SnapshotUploader.Bytes, Is.Not.Null);
            using var uploaded = SKBitmap.Decode(SnapshotUploader.Bytes);
            using var saved = SKBitmap.Decode(info.FilePath);
            Assert.Multiple(() =>
            {
                Assert.That(uploaded.GetPixel(30, 30), Is.EqualTo(SKColors.Red), "Upload bytes must contain the annotation.");
                Assert.That(saved.GetPixel(30, 30), Is.EqualTo(SKColors.Red), "The workflow file must contain the annotation.");
            });
            if (existingFile)
            {
                using var original = SKBitmap.Decode(originalPath);
                Assert.That(original.GetPixel(30, 30), Is.EqualTo(SKColors.White), "Editing creates a derivative and preserves the input file.");
            }
        }
        finally
        {
            if (previousUi != null) PlatformServices.RegisterUIService(previousUi);
            else PlatformServices.Reset();
            InstanceManager.Instance.RemoveInstance(destination.InstanceId);
            info.Metadata.Dispose();
            Directory.Delete(folder, true);
        }
    }

    [AvaloniaTest]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Continue_ReturnsFlattenedAnnotations(bool taskMode)
    {
        using var image = new SKBitmap(80, 60);
        image.Erase(SKColors.White);
        HostEditorWindow? window = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<HostEditorWindow>((sender, _) =>
        {
            window = sender;
            Dispatcher.UIThread.Post(() =>
            {
                var editor = sender.FindControl<EditorView>("EditorViewControl")!;
                editor.RestoreAnnotations([new RectangleAnnotation
                {
                    StartPoint = new SKPoint(10, 10), EndPoint = new SKPoint(50, 50),
                    FillColor = "#FFFF0000", StrokeColor = "#FFFF0000", StrokeWidth = 2
                }]);
                Dispatcher.UIThread.Post(() =>
                    ((MainViewModel)sender.DataContext!).ContinueCommand.Execute(null), DispatcherPriority.Background);
            });
        });

        var session = new AvaloniaUIService(new FakeDesktopTaskManager())
            .ShowEditorSessionAsync(image, taskMode: taskMode);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var result = await session;
            Assert.That(result, Is.Not.Null);
            using var rendered = result!.RenderedImage;
            using var source = result.SourceImage;
            Assert.Multiple(() =>
            {
                Assert.That(rendered.GetPixel(30, 30), Is.EqualTo(SKColors.Red), "The continued image must include annotations.");
                Assert.That(source!.GetPixel(30, 30), Is.EqualTo(SKColors.White), "The editable source stays separate.");
                Assert.That(result.Annotations, Has.Count.EqualTo(1));
            });
        }
        finally
        {
            if (window?.IsVisible == true)
                ((MainViewModel)window.DataContext!).ContinueCommand.Execute(null);
        }
    }

    private static IDisposable AnnotateAndContinue() =>
        Window.WindowOpenedEvent.AddClassHandler<HostEditorWindow>((window, _) =>
            Dispatcher.UIThread.Post(() =>
            {
                window.Width = 1200;
                window.Height = 800;
                var editor = window.FindControl<EditorView>("EditorViewControl")!;
                var vm = (MainViewModel)window.DataContext!;
                vm.ActiveTool = EditorTool.Rectangle;
                vm.FillColor = "#FFFF0000";
                vm.SelectedColor = "#FFFF0000";
                // Let toolbars and window sizing settle before translating image coordinates.
                Dispatcher.UIThread.Post(() =>
                {
                    window.UpdateLayout();
                    var canvas = editor.FindControl<Canvas>("AnnotationCanvas")!;
                    var start = canvas.TranslatePoint(new Point(10, 10), window)!.Value;
                    var end = canvas.TranslatePoint(new Point(50, 50), window)!.Value;
                    window.MouseDown(start, MouseButton.Left);
                    window.MouseMove(end);
                    window.MouseUp(end, MouseButton.Left);
                    Assert.That(editor.GetAnnotationSnapshot(), Has.Count.EqualTo(1));
                    Assert.That(editor.GetAnnotationSnapshot()[0].GetBounds(), Is.EqualTo(new SKRect(10, 10, 50, 50)));

                    Dispatcher.UIThread.Post(() =>
                    {
                        window.UpdateLayout();
                        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Command == vm.ContinueCommand);
                        var position = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                        window.MouseDown(position, MouseButton.Left);
                        window.MouseUp(position, MouseButton.Left);
                    }, DispatcherPriority.Background);
                }, DispatcherPriority.Background);
            }));

    private sealed class SnapshotProvider : UploaderProviderBase
    {
        public override string ProviderId => "annotation-workflow-test";
        public override string Name => "Annotation workflow test";
        public override string Description => "Captures upload bytes locally";
        public override Version Version => new(1, 0, 0);
        public override UploaderCategory[] SupportedCategories => [UploaderCategory.Image];
        public override Type ConfigModelType => typeof(object);
        public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes() => new() { [UploaderCategory.Image] = ["*"] };
        public override Uploader CreateInstance(string settingsJson) => new SnapshotUploader();
    }

    private sealed class SnapshotUploader : FileUploader
    {
        public static byte[]? Bytes;
        public override UploadResult Upload(Stream stream, string fileName)
        {
            using var output = new MemoryStream();
            stream.CopyTo(output);
            Bytes = output.ToArray();
            return new UploadResult { IsSuccess = true, URL = "https://example.test/annotated.png" };
        }
    }
}
