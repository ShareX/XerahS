using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.MacOS;

namespace XerahS.Tests.Platform.MacOS;

[TestFixture]
public class MacOSRegionSelectorPreferenceTests
{
    [Test]
    public void NativeRegionCaptureArguments_DefaultToSelectionOnlyInteractiveCaptureWithSound()
    {
        string arguments = MacOSScreenshotService.BuildNativeRegionCaptureArguments(new CaptureOptions());

        Assert.That(arguments, Does.Contain("-i"));
        Assert.That(arguments, Does.Contain("-s"));
        Assert.That(arguments, Does.Not.Contain("-x"));
        Assert.That(arguments, Does.Contain("-t png"));
    }

    [Test]
    public void NativeRegionCaptureArguments_SuppressSound_WhenMacOSCaptureSoundIsDisabled()
    {
        string arguments = MacOSScreenshotService.BuildNativeRegionCaptureArguments(new CaptureOptions
        {
            MacOSPlayCaptureSound = false
        });

        Assert.That(arguments, Does.Contain("-x"));
    }

    [Test]
    public void IsInteractiveCaptureBusyError_DetectsNativeScreencaptureConcurrencyFailure()
    {
        bool isBusy = MacOSScreenshotService.IsInteractiveCaptureBusyError(
            "screencapture: cannot run two interactive screen captures at a time");

        Assert.That(isBusy, Is.True);
    }

    [TestCase(MacOSInteractiveRegionSelectorPreference.Automatic)]
    [TestCase(MacOSInteractiveRegionSelectorPreference.XerahSOverlay)]
    public async Task CaptureRegionAsync_SkipsNativeCrosshair_WhenNativeCrosshairIsNotRequested(
        MacOSInteractiveRegionSelectorPreference preference)
    {
        var service = new MacOSScreenshotService();

        var bitmap = await service.CaptureRegionAsync(new CaptureOptions
        {
            MacOSRegionSelectorPreference = preference
        });

        Assert.That(bitmap, Is.Null);
    }
}
