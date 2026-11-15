using NUnit.Framework;
using XerahS.RegionCapture.ScreenRecording;
using XerahS.UI;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class TrayIconPathTests
{
    [TestCase(false, false, "icon_16x16.png")]
    [TestCase(false, true, "icon_32x32@2x.png")]
    [TestCase(true, false, "tray-default-white.png")]
    [TestCase(true, true, "tray-default-white-64.png")]
    public void IdleIconUsesLargeSourceOnlyWhenHighRes(bool white, bool highRes, string expectedFile)
    {
        string path = TrayIconHelper.ResolveIconPath(RecordingStatus.Idle, white, highRes);

        Assert.That(path, Does.EndWith(expectedFile));
    }

    [TestCase(RecordingStatus.Recording, false, "tray-recording.png")]
    [TestCase(RecordingStatus.Recording, true, "tray-recording-64.png")]
    [TestCase(RecordingStatus.Paused, false, "tray-recording-paused.png")]
    [TestCase(RecordingStatus.Paused, true, "tray-recording-paused-64.png")]
    public void RecordingIconsHaveLargeVariants(RecordingStatus status, bool highRes, string expectedFile)
    {
        Assert.That(TrayIconHelper.ResolveIconPath(status, true, highRes), Does.EndWith(expectedFile));
    }

    [Test]
    public void EveryResolvedIconIsAnEmbeddedAsset()
    {
        string assets = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "src", "desktop", "app", "XerahS.UI", "Assets"));
        if (!Directory.Exists(assets))
        {
            Assert.Ignore("Source tree not available.");
        }

        foreach (var status in Enum.GetValues<RecordingStatus>())
        foreach (bool white in new[] { false, true })
        foreach (bool hi in new[] { false, true })
        {
            string rel = TrayIconHelper.ResolveIconPath(status, white, hi).Replace("avares://XerahS.UI/Assets/", "");
            Assert.That(File.Exists(Path.Combine(assets, rel)), Is.True, rel);
        }
    }
}
