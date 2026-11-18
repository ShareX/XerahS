using Newtonsoft.Json;
using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks.Processors;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
[NonParallelizable]
public class FileTypeOverrideTests
{
    [SetUp]
    public void SetUp() => Clear();

    [TearDown]
    public void TearDown() => Clear();

    [Test]
    public void Override_WinsOverEveryCategoryRoute()
    {
        Add("S3", UploaderCategory.File, new FileTypeScope { AllFileTypes = true });
        Add("Paste", UploaderCategory.Text, new FileTypeScope { AllFileTypes = true });
        var docs = Add("Docs", UploaderCategory.File, new FileTypeScope());
        InstanceManager.Instance.SetFileTypeOverride(".MD", docs.InstanceId);

        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("notes.md")?.InstanceId, Is.EqualTo(docs.InstanceId));
        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("/tmp/NOTES.MD")?.InstanceId, Is.EqualTo(docs.InstanceId));
        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("notes.markdown"), Is.Null);
        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("md"), Is.Null, "a bare name is not an extension");

        // Text uploads and file uploads of the same file both take the override.
        Assert.That(UploadJobProcessor.ResolveFileTypeOverride(InstanceManager.Instance, TextTask("notes.md"))?.InstanceId, Is.EqualTo(docs.InstanceId));
        Assert.That(UploadJobProcessor.ResolveFileTypeOverride(InstanceManager.Instance, FileTask("/tmp/notes.md"))?.InstanceId, Is.EqualTo(docs.InstanceId));
    }

    [Test]
    public void LongestExtensionWins()
    {
        var gz = Add("Gz", UploaderCategory.File, new FileTypeScope());
        var tgz = Add("Tgz", UploaderCategory.File, new FileTypeScope());
        InstanceManager.Instance.SetFileTypeOverride("gz", gz.InstanceId);
        InstanceManager.Instance.SetFileTypeOverride("tar.gz", tgz.InstanceId);

        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("backup.tar.gz")?.InstanceId, Is.EqualTo(tgz.InstanceId));
        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("log.gz")?.InstanceId, Is.EqualTo(gz.InstanceId));
    }

    [Test]
    public void SettingAnExtensionAgain_ReplacesIt()
    {
        var a = Add("A", UploaderCategory.File, new FileTypeScope());
        var b = Add("B", UploaderCategory.File, new FileTypeScope());
        InstanceManager.Instance.SetFileTypeOverride("md", a.InstanceId);
        InstanceManager.Instance.SetFileTypeOverride("MD", b.InstanceId);

        var all = InstanceManager.Instance.GetFileTypeOverrides();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].InstanceId, Is.EqualTo(b.InstanceId));
        Assert.That(InstanceManager.Instance.RemoveFileTypeOverride(".md"), Is.True);
        Assert.That(InstanceManager.Instance.RemoveFileTypeOverride("md"), Is.False);
    }

    [Test]
    public void RemovingAnInstance_RemovesItsOverrides_AndUnavailableTargetsAreIgnored()
    {
        var docs = Add("Docs", UploaderCategory.File, new FileTypeScope());
        var offline = Add("Offline", UploaderCategory.File, new FileTypeScope(), isAvailable: false);
        InstanceManager.Instance.SetFileTypeOverride("md", docs.InstanceId);
        InstanceManager.Instance.SetFileTypeOverride("pdf", offline.InstanceId);

        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("a.pdf"), Is.Null, "unavailable instance is ignored");
        InstanceManager.Instance.RemoveInstance(docs.InstanceId);
        Assert.That(InstanceManager.Instance.GetFileTypeOverrides().Select(o => o.Extension), Is.EqualTo(new[] { "pdf" }));
    }

    [Test]
    public void ChosenForThisUpload_BeatsTheOverride()
    {
        var docs = Add("Docs", UploaderCategory.File, new FileTypeScope());
        InstanceManager.Instance.SetFileTypeOverride("md", docs.InstanceId);
        var task = FileTask("/tmp/notes.md");
        task.TaskSettings.DestinationChosenForThisUpload = true;

        Assert.That(UploadJobProcessor.ResolveFileTypeOverride(InstanceManager.Instance, task), Is.Null);
    }

    [Test]
    public void ClipboardText_UsesTheTextExtension()
    {
        var txt = Add("Txt", UploaderCategory.Text, new FileTypeScope());
        InstanceManager.Instance.SetFileTypeOverride("txt", txt.InstanceId);
        var task = new TaskInfo(new TaskSettings()) { DataType = EDataType.Text };
        task.TaskSettings.AdvancedSettings.TextFileExtension = "txt";

        Assert.That(UploadJobProcessor.GetRoutingFileName(task), Is.EqualTo("text.txt"));
        Assert.That(UploadJobProcessor.ResolveFileTypeOverride(InstanceManager.Instance, task)?.InstanceId, Is.EqualTo(txt.InstanceId));
    }

    [Test]
    public void RenamingKeepsAMultiPartOverrideExtension()
    {
        var tgz = Add("Tgz", UploaderCategory.File, new FileTypeScope());
        Assert.That(InstanceManager.Instance.GetExtensionToKeep("/tmp/backup.tar.gz"), Is.EqualTo(".gz"), "no override: the usual extension");
        InstanceManager.Instance.SetFileTypeOverride("tar.gz", tgz.InstanceId);
        Assert.That(InstanceManager.Instance.GetExtensionToKeep("/tmp/backup.TAR.GZ"), Is.EqualTo(".TAR.GZ"));
        Assert.That(InstanceManager.Instance.GetExtensionToKeep("notes.md"), Is.EqualTo(".md"));
    }

    [Test]
    public void OverridesWrittenByAnotherProcess_AreReadAndKept()
    {
        var docs = Add("Docs", UploaderCategory.File, new FileTypeScope());
        string path = InstanceManager.ConfigFilePath;

        // Another process (the CLI) adds an override to the file.
        var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
        json["FileTypeOverrides"] = Newtonsoft.Json.Linq.JArray.FromObject(new[] { new { Extension = "md", InstanceId = docs.InstanceId } });
        File.WriteAllText(path, json.ToString());
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));

        Assert.That(InstanceManager.Instance.GetFileTypeOverrideInstance("a.md")?.InstanceId, Is.EqualTo(docs.InstanceId));

        // This process then saves something else: the other process's override survives.
        Add("Other", UploaderCategory.File, new FileTypeScope());
        Assert.That(File.ReadAllText(path), Does.Contain("\"Extension\": \"md\""));
    }

    [Test]
    public void ImportingABackup_KeepsTheBackupsOverrides_EvenWhenTheFileOnDiskIsNewer()
    {
        var docs = Add("Docs", UploaderCategory.File, new FileTypeScope());
        InstanceManager.Instance.SetFileTypeOverride("md", docs.InstanceId);
        string backup = InstanceManager.Instance.ExportConfigurationJson();

        // Another process then changes the overrides on disk.
        InstanceManager.Instance.SetFileTypeOverride("pdf", docs.InstanceId);
        File.SetLastWriteTimeUtc(InstanceManager.ConfigFilePath, DateTime.UtcNow.AddSeconds(10));

        InstanceManager.Instance.ImportConfigurationJson(backup);
        Assert.That(InstanceManager.Instance.GetFileTypeOverrides().Select(o => o.Extension), Is.EqualTo(new[] { "md" }));
    }

    [Test]
    public void PrivacyReview_MatchesThePngTheApprovedUploadCarries()
    {
        var jpgHost = Add("JpgHost", UploaderCategory.File, new FileTypeScope());
        var pngHost = Add("PngHost", UploaderCategory.File, new FileTypeScope());
        InstanceManager.Instance.SetFileTypeOverride("jpg", jpgHost.InstanceId);
        InstanceManager.Instance.SetFileTypeOverride("png", pngHost.InstanceId);
        var task = FileTask("/tmp/shot.jpg");

        Assert.That(UploadJobProcessor.ResolveFileTypeOverride(InstanceManager.Instance, task, "private.png")?.InstanceId, Is.EqualTo(pngHost.InstanceId));
        Assert.That(UploadJobProcessor.ResolveFileTypeOverride(InstanceManager.Instance, task)?.InstanceId, Is.EqualTo(jpgHost.InstanceId));
    }

    [Test]
    public void ListedOverride_IsReportedEvenWhenItsUploaderIsUnavailable()
    {
        var offline = Add("Offline", UploaderCategory.File, new FileTypeScope(), isAvailable: false);
        InstanceManager.Instance.SetFileTypeOverride("md", offline.InstanceId);
        Assert.That(InstanceManager.Instance.FindFileTypeOverride("a.md"), Is.Null);
        Assert.That(InstanceManager.Instance.GetListedFileTypeOverride("a.md")?.InstanceId, Is.EqualTo(offline.InstanceId));
    }

    [TestCase(UploaderCategory.Image, "md", false)]
    [TestCase(UploaderCategory.Image, "PNG", true)]
    [TestCase(UploaderCategory.Image, "webp", true)]
    [TestCase(UploaderCategory.Text, "md", true)]
    [TestCase(UploaderCategory.Text, "json", true)]
    [TestCase(UploaderCategory.Text, "png", false)]
    [TestCase(UploaderCategory.File, "md", true)]
    [TestCase(UploaderCategory.File, "tar.gz", true)]
    public void OnlyFittingUploadersCanTakeAnExtension(UploaderCategory category, string extension, bool fits)
    {
        var instance = Add("Target", category, new FileTypeScope());
        Assert.That(InstanceManager.CanTakeExtension(instance, extension, out var reason), Is.EqualTo(fits), reason);
        if (!fits) Assert.That(reason, Does.Contain("cannot take"));
    }

    [TestCase(UploaderCategory.File, "iso", true, TestName = "File uploader takes a type its provider does not list (S3 + iso)")]
    [TestCase(UploaderCategory.Text, "py", true, TestName = "Text uploader takes a text type its provider does not list (Gist + py)")]
    [TestCase(UploaderCategory.Image, "mp4", true, TestName = "Image uploader takes a non-image type its provider lists (Imgur + mp4)")]
    [TestCase(UploaderCategory.Image, "md", false, TestName = "Image uploader refuses a type neither its category nor its provider covers")]
    public void ProviderLists_WidenButNeverNarrow(UploaderCategory category, string extension, bool fits)
    {
        RegisterListsProvider();
        var instance = Add("Listed", category, new FileTypeScope(), ListsProviderId);
        Assert.That(InstanceManager.CanTakeExtension(instance, extension, out var reason), Is.EqualTo(fits), reason);
    }

    [TestCase(UploaderCategory.Text, "png")]
    [TestCase(UploaderCategory.Image, "pdf")]
    public void AStarInTheProviderList_MeansAnything(UploaderCategory category, string extension)
    {
        RegisterListsProvider();
        var instance = Add("Star", category, new FileTypeScope(), StarProviderId);
        Assert.That(InstanceManager.CanTakeExtension(instance, extension, out var reason), Is.True, reason);
    }

    [TestCase("KovaForge Docs (File)", UploaderCategory.File, "KovaForge Docs (File)")]
    [TestCase("KovaForge Docs (file)", UploaderCategory.File, "KovaForge Docs (file)")]
    [TestCase("Amazon S3", UploaderCategory.File, "Amazon S3 (File)")]
    [TestCase("Paste2 (Text)", UploaderCategory.File, "Paste2 (Text) (File)")]
    public void InstanceLabels_DoNotRepeatTheCategory(string name, UploaderCategory category, string expected)
    {
        Assert.That(InstanceManager.DescribeInstance(name, category), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase(".")]
    [TestCase("a/b")]
    [TestCase("*.md")]
    [TestCase("md.")]
    public void BadExtensions_AreRejected(string extension)
    {
        Assert.That(InstanceManager.NormalizeOverrideExtension(extension), Is.Null);
    }

    [Test]
    public void TextFileExceptions_DoNotGrowOnEverySave()
    {
        var scope = new FileTypeScope { TextFiles = true, TextFileExceptions = ["html", "htm"] };
        string json = JsonConvert.SerializeObject(scope);
        Assert.That(json, Does.Not.Contain("EffectiveTextFileExceptions"));

        // A file written before the fix still carries the computed list; reading it must not double the exceptions.
        string old = "{\"TextFiles\":true,\"TextFileExceptions\":[\"html\",\"htm\"],\"EffectiveTextFileExceptions\":[\"html\",\"htm\"]}";
        var back = JsonConvert.DeserializeObject<FileTypeScope>(old)!;
        var instance = Add("Docs", UploaderCategory.File, back);
        Assert.That(InstanceManager.Instance.GetInstance(instance.InstanceId)!.FileTypeRouting.TextFileExceptions, Is.EquivalentTo(new[] { "html", "htm" }));
    }

    private static TaskInfo TextTask(string fileName)
    {
        var task = new TaskInfo(new TaskSettings()) { DataType = EDataType.Text, TextContent = "# notes" };
        task.SetFileName(fileName);
        return task;
    }

    private static TaskInfo FileTask(string path) => new(new TaskSettings()) { DataType = EDataType.File, FilePath = path };

    private const string ListsProviderId = "override-test-lists";
    private const string StarProviderId = "override-test-star";

    private static void RegisterListsProvider()
    {
        if (ProviderCatalog.GetProvider(ListsProviderId) == null)
        {
            ProviderCatalog.RegisterProvider(new ListedTypesProvider(ListsProviderId, new()
            {
                [UploaderCategory.Image] = ["png", "jpg", "mp4"],
                [UploaderCategory.Text] = ["txt", "md"],
                [UploaderCategory.File] = ["png", "zip", "pdf"]
            }));
        }

        if (ProviderCatalog.GetProvider(StarProviderId) == null)
        {
            ProviderCatalog.RegisterProvider(new ListedTypesProvider(StarProviderId, new()
            {
                [UploaderCategory.Image] = ["*"],
                [UploaderCategory.Text] = ["*"],
                [UploaderCategory.File] = ["*"]
            }));
        }
    }

    private sealed class ListedTypesProvider(string id, Dictionary<UploaderCategory, string[]> types) : UploaderProviderBase
    {
        public override string ProviderId => id;
        public override string Name => id;
        public override string Description => "Test-only provider with a supported file type list.";
        public override Version Version { get; } = new(1, 0, 0);
        public override UploaderCategory[] SupportedCategories { get; } = [UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File];
        public override Type ConfigModelType => typeof(object);
        public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes() => types;
        public override XerahS.Uploaders.Uploader CreateInstance(string settingsJson) => throw new NotSupportedException();
    }

    private static UploaderInstance Add(string name, UploaderCategory category, FileTypeScope routing, string providerId) =>
        Add(name, category, routing, true, providerId);

    private static UploaderInstance Add(string name, UploaderCategory category, FileTypeScope routing, bool isAvailable = true, string providerId = "test-provider")
    {
        var instance = new UploaderInstance
        {
            ProviderId = providerId,
            Category = category,
            DisplayName = name,
            SettingsJson = "{}",
            IsAvailable = isAvailable,
            FileTypeRouting = routing
        };
        InstanceManager.Instance.AddInstance(instance);
        return instance;
    }

    private static void Clear()
    {
        foreach (var o in InstanceManager.Instance.GetFileTypeOverrides())
        {
            InstanceManager.Instance.RemoveFileTypeOverride(o.Extension);
        }

        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }
}
