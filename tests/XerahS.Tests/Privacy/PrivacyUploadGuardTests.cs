using NUnit.Framework;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
using XerahS.Core.Tasks.Processors;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Privacy;

[TestFixture, NonParallelizable]
public class PrivacyUploadGuardTests
{
    private string _folder = null!;
    private Func<IPrivacyAnalysisProvider?>? _factory;
    private IPrivacyReviewService? _review;
    private readonly List<TaskInfo> _tasks = [];
    [SetUp] public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xerahs-privacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _factory = PrivacyServices.AnalysisProviderFactory;
        _review = PrivacyServices.ReviewService;
        PrivacyServices.AnalysisProviderFactory = null;
        PrivacyServices.ReviewService = null;
    }
    [TearDown] public void Cleanup()
    {
        PrivacyServices.AnalysisProviderFactory = _factory;
        PrivacyServices.ReviewService = _review;
        foreach (var task in _tasks) task.Metadata.Dispose();
        _tasks.Clear();
        Directory.Delete(_folder, true);
    }
    private TaskInfo Create(bool capture = false, bool upload = true)
    {
        var info = new TaskInfo(new TaskSettings
        {
            PrivacySettings = new() { Enabled = true, Effect = PrivacyRedactionEffect.Mask },
            Job = WorkflowType.PrintScreen, AfterUploadJob = AfterUploadTasks.None,
            AfterCaptureJob = upload ? AfterCaptureTasks.UploadImageToHost : AfterCaptureTasks.None,
            OverrideScreenshotsFolder = true, ScreenshotsFolder = _folder
        }) { Job = capture ? TaskJob.Job : TaskJob.FileUpload, DataType = EDataType.Image };
        info.Metadata.Image = new SKBitmap(20, 20);
        info.Metadata.Image.Erase(new SKColor(255, 0, 0, 100));
        info.Metadata.WindowTitle = "synthetic@example.test";
        info.Metadata.OcrText = "synthetic secret";
        _tasks.Add(info);
        return info;
    }
    private (Analyzer Analyzer, Reviewer Reviewer) Configure(PrivacyReviewDecision decision = PrivacyReviewDecision.Upload, bool consent = true)
    {
        var analyzer = new Analyzer();
        var reviewer = new Reviewer { Decision = decision, Consent = consent };
        PrivacyServices.AnalysisProviderFactory = () => analyzer;
        PrivacyServices.ReviewService = reviewer;
        return (analyzer, reviewer);
    }
    [TestCase(".mp4")]
    [TestCase(".pdf")]
    public async Task FileThumbnailDoesNotActivateImagePrivacy(string extension)
    {
        var info = Create();
        info.DataType = EDataType.File;
        info.FilePath = Path.Combine(_folder, "synthetic" + extension);
        await File.WriteAllTextAsync(info.FilePath, "synthetic non-image file");
        var originalImage = info.Metadata.Image;
        var (analyzer, reviewer) = Configure();
        Assert.That(PrivacyUploadGuard.IsRequired(info), Is.False);
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.True);
        Assert.That(analyzer.Calls + reviewer.Reviews + reviewer.ConsentCalls, Is.Zero);
        Assert.That(info.Metadata.Image, Is.SameAs(originalImage));
        Assert.That(Path.GetExtension(info.FilePath), Is.EqualTo(extension));
    }

    [Test]
    public async Task DefaultCaptureDataTypeStillReceivesPrivacyProtection()
    {
        var info = Create(capture: true);
        info.DataType = EDataType.Default;
        var (analyzer, _) = Configure();
        Assert.That(PrivacyUploadGuard.IsRequired(info), Is.True);
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.True);
        Assert.That(analyzer.Calls, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AnimatedSourceIsBlockedEvenWithPreloadedFirstFrame(bool preloaded)
    {
        var info = Create();
        info.DataType = EDataType.File;
        info.FilePath = Path.Combine(_folder, "synthetic-animation.gif");
        // Two one-pixel GIF frames, generated entirely from synthetic data.
        byte[] header = [71, 73, 70, 56, 57, 97, 1, 0, 1, 0, 128, 0, 0, 0, 0, 0, 255, 255, 255];
        byte[] frame = [33, 249, 4, 0, 10, 0, 0, 0, 44, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 68, 1, 0];
        await File.WriteAllBytesAsync(info.FilePath, header.Concat(frame).Concat(frame).Append((byte)59).ToArray());
        using (var codec = SKCodec.Create(info.FilePath)) Assert.That(codec.FrameCount, Is.EqualTo(2));
        if (!preloaded) { info.Metadata.Image!.Dispose(); info.Metadata.Image = null; }
        var (analyzer, reviewer) = Configure();
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.False);
        Assert.That(info.PrivacyReviewRequired, Is.True);
        Assert.That(analyzer.Calls + reviewer.Reviews + reviewer.ConsentCalls, Is.Zero);
        Assert.That(Directory.GetFiles(_folder, "private-*.png"), Is.Empty);
    }

    [Test]
    public void CancelledTaskSendsNoPixelsAndCreatesNoFile()
    {
        var info = Create();
        var (analyzer, reviewer) = Configure();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => PrivacyUploadGuard.PrepareAsync(info, cancellation.Token));
        Assert.That(analyzer.Calls + reviewer.Reviews, Is.Zero);
        Assert.That(Directory.GetFiles(_folder), Is.Empty);
    }

    [Test] public async Task CaptureWithoutUpload_DoesNotAnalyzeOrChangeImage()
    {
        var info = Create(capture: true, upload: false);
        var (analyzer, reviewer) = Configure();
        var original = info.Metadata.Image;
        Assert.That(await new CaptureJobProcessor().ProcessAsync(info, CancellationToken.None), Is.True);
        Assert.That(analyzer.Calls, Is.Zero);
        Assert.That(reviewer.ConsentCalls + reviewer.Reviews, Is.Zero);
        Assert.That(info.Metadata.Image, Is.SameAs(original));
    }
    [TestCase(true)] [TestCase(false)] public async Task BothUploadPaths_BlockHeadlessBeforeDispatch(bool capture)
    {
        var info = Create(capture);
        if (capture) Assert.That(await new CaptureJobProcessor().ProcessAsync(info, CancellationToken.None), Is.False);
        else await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);
        Assert.That(info.Status, Is.EqualTo("Review required"));
        Assert.That(info.Result.URL, Is.Null.Or.Empty);
        Assert.That(Directory.GetFiles(_folder), Is.Empty);
    }
    [Test] public async Task DeclinedCloudConsent_DoesNotSendPixelsAndAllowsManualReview()
    {
        var info = Create();
        var (analyzer, reviewer) = Configure(consent: false);
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.True);
        Assert.That(analyzer.Calls, Is.Zero);
        Assert.That(reviewer.LastStatus, Is.EqualTo(PrivacyScanStatus.Unsupported));
    }
    [Test] public async Task CancelledReview_PublishesNoDerivative()
    {
        var info = Create();
        Configure(PrivacyReviewDecision.Cancel);
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.False);
        Assert.That(Directory.GetFiles(_folder), Is.Empty);
    }
    [Test] public async Task ApprovalPreservesSourceAndUsesOnlyFlattenedSanitizedBytes()
    {
        var info = Create();
        string source = Path.Combine(_folder, "synthetic-email@example.test.png");
        byte[] original = PrivacyUploadGuard.Encode(info.Metadata.Image!);
        await File.WriteAllBytesAsync(source, original);
        info.FilePath = source;
        var (analyzer, reviewer) = Configure();
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.True);
        Assert.That(await File.ReadAllBytesAsync(source), Is.EqualTo(original));
        Assert.That(info.FilePath, Is.Not.EqualTo(source));
        Assert.That(info.FileName, Does.StartWith("private-").And.Not.Contains("example"));
        Assert.That(info.Metadata.WindowTitle, Is.Null);
        Assert.That(info.Metadata.OcrText, Is.Null);
        Assert.That(info.Metadata.Image!.GetPixel(10, 10), Is.EqualTo(SKColors.Black));
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.True);
        Assert.That(analyzer.Calls, Is.EqualTo(1));
        Assert.That(reviewer.ConsentCalls, Is.EqualTo(1));
        var destination = new UploaderInstance { ProviderId = "test", DisplayName = "Test Host", SettingsJson = "{}" };
        Assert.That(await PrivacyUploadGuard.ConfirmDestinationAsync(info, destination, CancellationToken.None), Is.True);
        using var stream = PrivacyUploadGuard.OpenApprovedContent(info, destination);
        using var uploaded = SKBitmap.Decode(stream);
        Assert.That(uploaded.GetPixel(10, 10), Is.EqualTo(SKColors.Black));
        destination.SettingsJson = "changed";
        Assert.Throws<InvalidOperationException>(() => PrivacyUploadGuard.OpenApprovedContent(info, destination));
    }
    [TestCase("file")] [TestCase("pixels")] [TestCase("policy")] [TestCase("provider")]
    public async Task ChangedContentOrPolicy_InvalidatesApproval(string change)
    {
        var info = Create();
        var (analyzer, _) = Configure();
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.True);
        var destination = new UploaderInstance { ProviderId = "test" };
        Assert.That(await PrivacyUploadGuard.ConfirmDestinationAsync(info, destination, CancellationToken.None), Is.True);
        switch (change)
        {
            case "file": await File.WriteAllTextAsync(info.FilePath, "changed"); break;
            case "pixels": info.Metadata.Image!.SetPixel(0, 0, SKColors.Blue); break;
            case "policy": info.TaskSettings.PrivacySettings.MarginPixels++; break;
            case "provider": analyzer.Identity = "other model"; break;
        }
        Assert.Throws<InvalidOperationException>(() => PrivacyUploadGuard.OpenApprovedContent(info, destination));
    }
    [Test] public async Task FailedScan_IsPassedAsFailureToManualReview()
    {
        var info = Create();
        var (analyzer, reviewer) = Configure();
        analyzer.Status = PrivacyScanStatus.Error;
        Assert.That(await PrivacyUploadGuard.PrepareAsync(info, CancellationToken.None), Is.True);
        Assert.That(reviewer.LastStatus, Is.EqualTo(PrivacyScanStatus.Error));
    }
    [TestCase(true, UploaderCategory.Image)]
    [TestCase(false, UploaderCategory.Image)]
    [TestCase(false, UploaderCategory.File)]
    public async Task MockUploaderReceivesOnlyApprovedPixels(bool capture, UploaderCategory category)
    {
        var info = Create(capture);
        var (analyzer, reviewer) = Configure();
        SpyUploader.Bytes = null;
        const string providerId = "privacy-test-upload-spy";
        if (ProviderCatalog.GetProvider(providerId) == null) ProviderCatalog.RegisterProvider(new SpyProvider());
        var instance = new UploaderInstance { ProviderId = providerId, Category = category, DisplayName = "Synthetic Host",
            FileTypeRouting = new() { AllFileTypes = true } };
        InstanceManager.Instance.AddInstance(instance);
        info.TaskSettings.DestinationInstanceId = instance.InstanceId;
        try
        {
            if (capture) info.Result = (await new UploadJobProcessor().UploadCaptureImageAsync(info, CancellationToken.None))!;
            else await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);
            Assert.That(info.Result.URL, Is.EqualTo("https://example.test/private.png"));
            Assert.That(SpyUploader.Bytes, Is.Not.Null);
            using var received = SKBitmap.Decode(SpyUploader.Bytes);
            Assert.That(received.GetPixel(10, 10), Is.EqualTo(SKColors.Black));
            Assert.That(SpyUploader.Name, Does.StartWith("private-"));
            Assert.That(analyzer.Calls, Is.EqualTo(1));
            Assert.That(reviewer.Reviews, Is.EqualTo(1));
        }
        finally { InstanceManager.Instance.RemoveInstance(instance.InstanceId); }
    }
    [Test]
    public async Task CancelledFallbackReviewStopsFurtherDestinationAttempts()
    {
        var info = Create();
        var (_, reviewer) = Configure();
        reviewer.CancelFallback = true;
        SpyUploader.Calls = 0;
        if (ProviderCatalog.GetProvider("privacy-test-upload-spy") == null) ProviderCatalog.RegisterProvider(new SpyProvider());
        var primary = new UploaderInstance { ProviderId = "privacy-test-upload-spy", Category = UploaderCategory.Image, DisplayName = "Primary", SettingsJson = "fail" };
        var fallback = new UploaderInstance { ProviderId = "privacy-test-upload-spy", Category = UploaderCategory.Image, DisplayName = "Fallback" };
        InstanceManager.Instance.AddInstance(primary); InstanceManager.Instance.AddInstance(fallback);
        info.TaskSettings.DestinationInstanceId = primary.InstanceId;
        try
        {
            await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);
            Assert.That(SpyUploader.Calls, Is.EqualTo(1));
            Assert.That(info.PrivacyReviewRequired, Is.True);
            Assert.That(info.Result.URL, Is.Null.Or.Empty);
        }
        finally
        { InstanceManager.Instance.RemoveInstance(primary.InstanceId); InstanceManager.Instance.RemoveInstance(fallback.InstanceId); }
    }

    private sealed class SpyProvider : UploaderProviderBase
    {
        public override string ProviderId => "privacy-test-upload-spy";
        public override string Name => "Synthetic Upload Spy";
        public override string Description => "Synthetic test provider";
        public override Version Version => new(1, 0, 0);
        public override UploaderCategory[] SupportedCategories => [UploaderCategory.Image, UploaderCategory.File];
        public override Type ConfigModelType => typeof(object);
        public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes() => new()
        { [UploaderCategory.Image] = ["*"], [UploaderCategory.File] = ["*"] };
        public override Uploader CreateInstance(string settingsJson) => new SpyUploader(settingsJson == "fail");
    }
    private sealed class SpyUploader(bool fail = false) : FileUploader
    {
        public static int Calls;
        public static byte[]? Bytes;
        public static string? Name;
        public override UploadResult Upload(Stream stream, string fileName)
        {
            using var output = new MemoryStream();
            stream.CopyTo(output);
            Calls++;
            Bytes = output.ToArray(); Name = fileName;
            if (fail) return new UploadResult { IsSuccess = false, Response = "Synthetic failure" };
            return new UploadResult { IsSuccess = true, URL = "https://example.test/private.png" };
        }
    }

    private sealed class Analyzer : IPrivacyAnalysisProvider
    {
        public string Identity { get; set; } = "synthetic model";
        public string DisplayName => Identity;
        public bool RequiresCloudConsent => true;
        public int Calls;
        public PrivacyScanStatus Status = PrivacyScanStatus.Complete;
        public Task<PrivacyScanResult> AnalyzeAsync(SKBitmap image, TaskSettingsPrivacy policy, CancellationToken token)
        { Calls++; return Task.FromResult(new PrivacyScanResult(Status, Status == PrivacyScanStatus.Complete
            ? [new(PrivacyCategory.Email, 0.25, 0.25, 0.5, 0.5, 1)] : [])); }
    }
    private sealed class Reviewer : IPrivacyReviewService
    {
        public PrivacyReviewDecision Decision;
        public bool Consent;
        public int ConsentCalls, Reviews;
        public PrivacyScanStatus LastStatus;
        public bool CancelFallback;
        public Task<bool> ConfirmCloudAnalysisAsync(string provider, CancellationToken token)
        { ConsentCalls++; return Task.FromResult(Consent); }
        public Task<PrivacyReviewResult> ReviewAsync(SKBitmap original, SKBitmap candidate, PrivacyScanResult scan,
            string destination, TaskSettingsPrivacy policy, CancellationToken token)
        { Reviews++; LastStatus = scan.Status; var decision = CancelFallback && destination.Contains("Fallback") ? PrivacyReviewDecision.Cancel : Decision;
            return Task.FromResult(new PrivacyReviewResult(decision,
            decision == PrivacyReviewDecision.Cancel ? null : candidate.Copy())); }
    }
}
