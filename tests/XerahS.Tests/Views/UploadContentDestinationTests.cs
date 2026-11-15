using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Views;

[TestFixture]
public sealed class UploadContentDestinationTests
{
    private const string InstanceId = "0123456789abcdef0123456789abcdef01234567";

    [TestCase(EDataType.File)]
    [TestCase(EDataType.Image)]
    [TestCase(EDataType.Text)]
    public void ChosenUploaderOverridesDestination(EDataType dataType)
    {
        var settings = new TaskSettings { Job = WorkflowType.FileUpload };

        UploadContentViewModel.ApplyDestinationOverride(settings, dataType, InstanceId);

        Assert.That(settings.DestinationInstanceId, Is.EqualTo(InstanceId));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void WorkflowDefaultLeavesDestinationUnchanged(string? chosen)
    {
        var settings = new TaskSettings { Job = WorkflowType.FileUpload, DestinationInstanceId = "keep" };

        UploadContentViewModel.ApplyDestinationOverride(settings, EDataType.File, chosen);

        Assert.That(settings.DestinationInstanceId, Is.EqualTo("keep"));
    }

    [Test]
    public void UrlItemsKeepTheirShortener()
    {
        var settings = new TaskSettings { Job = WorkflowType.ClipboardUploadWithContentViewer, DestinationInstanceId = "keep" };

        UploadContentViewModel.ApplyDestinationOverride(settings, EDataType.URL, InstanceId);

        Assert.That(settings.DestinationInstanceId, Is.EqualTo("keep"));
    }
}
