using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.UI.Helpers;

namespace XerahS.Tests.Helpers;

/// <summary>
/// "Start minimized to tray" may only hide the window when something can show the tray icon back.
/// Without a tray host the app would keep running with no reachable window.
/// </summary>
public class SilentRunStartupPolicyTests
{
    [SetUp]
    [TearDown]
    public void RestoreTrayHost()
    {
        PlatformServices.TrayHostSupported = true;
    }

    [Test]
    public void HidesOnlyOnFirstStartWhenEnabled()
    {
        Assert.That(SilentRunStartupPolicy.ShouldHideMainWindowToTray(true, isExiting: false, alreadyApplied: false), Is.True);
        Assert.That(SilentRunStartupPolicy.ShouldHideMainWindowToTray(false, isExiting: false, alreadyApplied: false), Is.False);
        Assert.That(SilentRunStartupPolicy.ShouldHideMainWindowToTray(true, isExiting: true, alreadyApplied: false), Is.False);
        Assert.That(SilentRunStartupPolicy.ShouldHideMainWindowToTray(true, isExiting: false, alreadyApplied: true), Is.False);
    }

    [Test]
    public void NeverHidesWhenTheDesktopHasNoTrayHost()
    {
        PlatformServices.TrayHostSupported = false;

        Assert.That(SilentRunStartupPolicy.ShouldHideMainWindowToTray(true, isExiting: false, alreadyApplied: false), Is.False);
    }
}
