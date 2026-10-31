using NUnit.Framework;
using XerahS.Core.Tasks.Processors;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
[NonParallelizable]
public class FileTypeRoutingTests
{
    [SetUp]
    public void SetUp() => ClearInstances();

    [TearDown]
    public void TearDown() => ClearInstances();

    [Test]
    public void SpecificTypes_CanCoexistWithAllFileTypesCatchAll()
    {
        var s3 = Add("S3", new FileTypeScope { AllFileTypes = true });
        var docs = Add("Docs", new FileTypeScope { FileExtensions = ["md"] });

        Assert.That(InstanceManager.Instance.ValidateFileTypeConfiguration(docs), Is.Null);
        Assert.That(InstanceManager.Instance.ValidateFileTypeConfiguration(s3), Is.Null);
        Assert.That(InstanceManager.Instance.CanAddFileType(UploaderCategory.File, docs.InstanceId, "txt"), Is.True);
        Assert.That(InstanceManager.Instance.GetBlockedFileTypes(UploaderCategory.File, docs.InstanceId), Is.Empty);
        Assert.That(InstanceManager.Instance.CanSetAllFileTypes(UploaderCategory.File, s3.InstanceId), Is.True);
    }

    [Test]
    public void SecondCatchAll_IsRejected()
    {
        Add("S3", new FileTypeScope { AllFileTypes = true });
        var other = Add("Other", new FileTypeScope { AllFileTypes = true });

        Assert.That(InstanceManager.Instance.ValidateFileTypeConfiguration(other), Does.Contain("S3"));
        Assert.That(InstanceManager.Instance.CanSetAllFileTypes(UploaderCategory.File, other.InstanceId), Is.False);
    }

    [Test]
    public void SecondTextFilesDestination_IsRejected()
    {
        Add("Docs", new FileTypeScope { TextFiles = true });
        var other = Add("Other", new FileTypeScope { TextFiles = true });

        Assert.That(InstanceManager.Instance.ValidateFileTypeConfiguration(other), Does.Contain("Docs"));
    }

    [Test]
    public void GetDestinationForFile_PrefersExplicitThenTextFilesThenCatchAll()
    {
        var s3 = Add("S3", new FileTypeScope { AllFileTypes = true });
        var docs = Add("Docs", new FileTypeScope { TextFiles = true });
        var logs = Add("Logs", new FileTypeScope { FileExtensions = ["log"] });

        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.File, ".log")?.InstanceId, Is.EqualTo(logs.InstanceId));
        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.File, ".MD")?.InstanceId, Is.EqualTo(docs.InstanceId));
        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.File, ".png")?.InstanceId, Is.EqualTo(s3.InstanceId));
        Assert.That(InstanceManager.Instance.GetSpecificDestinationForFile(UploaderCategory.File, ".png"), Is.Null);
    }

    [TestCase("html")]
    [TestCase(".HTM")]
    [TestCase("xhtml")]
    public void WebPages_AreExceptionsToTextFilesByDefault(string extension)
    {
        var s3 = Add("S3", new FileTypeScope { AllFileTypes = true });
        Add("Docs", new FileTypeScope { TextFiles = true });

        Assert.That(FileTypeScope.IsTextFileExtension(extension), Is.True);
        Assert.That(InstanceManager.Instance.GetSpecificDestinationForFile(UploaderCategory.File, extension), Is.Null);
        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.File, extension)?.InstanceId, Is.EqualTo(s3.InstanceId));
        Assert.That(Route(s3, "architecture." + extension.TrimStart('.'))?.InstanceId, Is.EqualTo(s3.InstanceId));
    }

    [Test]
    public void TextFileExceptions_AreConfigurable()
    {
        var s3 = Add("S3", new FileTypeScope { AllFileTypes = true });
        var docs = Add("Docs", new FileTypeScope { TextFiles = true, TextFileExceptions = [] });

        // An empty list removes the default exception, so html is a text file again.
        Assert.That(Route(s3, "page.html")?.InstanceId, Is.EqualTo(docs.InstanceId));

        docs.FileTypeRouting!.TextFileExceptions = ["md"];
        Assert.That(Route(s3, "notes.md")?.InstanceId, Is.EqualTo(s3.InstanceId));
        Assert.That(Route(s3, "page.html")?.InstanceId, Is.EqualTo(docs.InstanceId));
        Assert.That(docs.FileTypeRouting.HandlesExtension("md"), Is.False);
    }

    [Test]
    public void TextFileExceptions_DefaultsWhenMissingFromSavedSettings()
    {
        var scope = Newtonsoft.Json.JsonConvert.DeserializeObject<FileTypeScope>("{\"TextFiles\":true,\"FileExtensions\":[]}")!;

        Assert.That(scope.TextFileExceptions, Is.Null);
        Assert.That(scope.EffectiveTextFileExceptions, Is.EquivalentTo(FileTypeScope.DefaultTextFileExceptions));
        Assert.That(scope.ClaimsAsTextFile("html"), Is.False);
        Assert.That(scope.ClaimsAsTextFile("md"), Is.True);
    }

    [Test]
    public void CopiedInstance_KeepsTextFileExceptions()
    {
        var docs = Add("Docs", new FileTypeScope { TextFiles = true, TextFileExceptions = ["md", "txt"] });

        var copy = InstanceManager.Instance.DuplicateInstance(docs.InstanceId, "Docs 2");

        Assert.That(copy?.FileTypeRouting?.TextFileExceptions, Is.EqualTo(new[] { "md", "txt" }));
    }

    [Test]
    public void ApplyFileTypeRoute_HandsTextFileFromDefaultToSpecialist()
    {
        var s3 = Add("S3", new FileTypeScope { AllFileTypes = true });
        var docs = Add("Docs", new FileTypeScope { TextFiles = true });

        Assert.That(Route(s3, "notes.md")?.InstanceId, Is.EqualTo(docs.InstanceId));
        Assert.That(Route(s3, "photo.png")?.InstanceId, Is.EqualTo(s3.InstanceId));
        Assert.That(Route(s3, "archive.zip")?.InstanceId, Is.EqualTo(s3.InstanceId));
    }

    [Test]
    public void ApplyFileTypeRoute_KeepsDestinationThatClaimsTheTypeItself()
    {
        var docs = Add("Docs", new FileTypeScope { TextFiles = true });
        var mdOnly = Add("Markdown", new FileTypeScope { FileExtensions = ["md"] });

        Assert.That(Route(docs, "notes.txt")?.InstanceId, Is.EqualTo(docs.InstanceId));
        Assert.That(Route(mdOnly, "notes.md")?.InstanceId, Is.EqualTo(mdOnly.InstanceId));
    }

    [Test]
    public void ApplyFileTypeRoute_IgnoresUnavailableSpecialistAndOtherCategories()
    {
        var s3 = Add("S3", new FileTypeScope { AllFileTypes = true });
        Add("Docs", new FileTypeScope { TextFiles = true }, isAvailable: false);

        Assert.That(Route(s3, "notes.md")?.InstanceId, Is.EqualTo(s3.InstanceId));
        Assert.That(UploadJobProcessor.ApplyFileTypeRoute(InstanceManager.Instance, UploaderCategory.Image, s3, "notes.md"), Is.SameAs(s3));
    }

    private static UploaderInstance? Route(UploaderInstance target, string fileName) =>
        UploadJobProcessor.ApplyFileTypeRoute(InstanceManager.Instance, UploaderCategory.File, target, fileName);

    private static UploaderInstance Add(string name, FileTypeScope routing, bool isAvailable = true)
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.File,
            DisplayName = name,
            SettingsJson = "{}",
            IsAvailable = isAvailable,
            FileTypeRouting = routing
        };
        InstanceManager.Instance.AddInstance(instance);
        return instance;
    }

    private static void ClearInstances()
    {
        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }
}
