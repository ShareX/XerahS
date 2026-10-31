using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Tasks.Pipeline;

namespace XerahS.Tests.Tasks;

[TestFixture]
public class CaptureStageUploadContentTests
{
    private static PipelineContext CreateContext(WorkflowType job, string? text = null, string? filePath = null)
    {
        var info = new TaskInfo(new TaskSettings { Job = job });
        info.TextContent = text;
        info.FilePath = filePath ?? string.Empty;
        return new PipelineContext { Info = info };
    }

    [Test]
    public void HasPreloadedUploadContent_TrueForQueuedText()
    {
        Assert.That(CaptureStage.HasPreloadedUploadContent(
            CreateContext(WorkflowType.ClipboardUploadWithContentViewer, text: "hello")), Is.True);
    }

    [Test]
    public void HasPreloadedUploadContent_TrueForQueuedFile()
    {
        Assert.That(CaptureStage.HasPreloadedUploadContent(
            CreateContext(WorkflowType.ClipboardUploadWithContentViewer, filePath: "/tmp/a.txt")), Is.True);
    }

    [Test]
    public void HasPreloadedUploadContent_FalseForHotkeyLaunch()
    {
        Assert.That(CaptureStage.HasPreloadedUploadContent(
            CreateContext(WorkflowType.ClipboardUploadWithContentViewer)), Is.False);
    }

    [Test]
    public void HasPreloadedUploadContent_FalseForOtherToolWorkflows()
    {
        Assert.That(CaptureStage.HasPreloadedUploadContent(
            CreateContext(WorkflowType.ClipboardViewer, text: "hello")), Is.False);
    }
}
