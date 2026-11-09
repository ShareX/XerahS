#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Detection;
using XerahS.Platform.Linux.Capture.OmaSnap;
using XerahS.Platform.Linux.Capture.Orchestration;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

/// <summary>
/// XIP0088: desktop profile matrix and the rule that nothing changes on systems that are not
/// Omarchy-like (golden test of the capture order against the pre-XIP0088 policy).
/// </summary>
[TestFixture]
public class LinuxDesktopProfileTests
{
    public sealed record Session(string Name, Dictionary<string, string?> Env, bool OmarchyInstalled = false, bool FlatpakInfo = false);

    private static readonly Session[] Sessions =
    [
        new("Omarchy", new() { ["XDG_SESSION_TYPE"] = "wayland", ["WAYLAND_DISPLAY"] = "wayland-1", ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc", ["XDG_CURRENT_DESKTOP"] = "Hyprland", ["OMARCHY_PATH"] = "/home/u/.local/share/omarchy" }),
        new("Omarchy (package)", new() { ["XDG_SESSION_TYPE"] = "wayland", ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc", ["XDG_CURRENT_DESKTOP"] = "Hyprland" }, OmarchyInstalled: true),
        new("Hyprland without Omarchy", new() { ["XDG_SESSION_TYPE"] = "wayland", ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc", ["XDG_CURRENT_DESKTOP"] = "Hyprland" }),
        new("GNOME", new() { ["XDG_SESSION_TYPE"] = "wayland", ["WAYLAND_DISPLAY"] = "wayland-0", ["XDG_CURRENT_DESKTOP"] = "GNOME" }),
        new("KDE", new() { ["XDG_SESSION_TYPE"] = "wayland", ["WAYLAND_DISPLAY"] = "wayland-0", ["XDG_CURRENT_DESKTOP"] = "KDE" }),
        new("Sway", new() { ["XDG_SESSION_TYPE"] = "wayland", ["WAYLAND_DISPLAY"] = "wayland-1", ["SWAYSOCK"] = "/run/sway", ["XDG_CURRENT_DESKTOP"] = "sway" }),
        new("X11", new() { ["XDG_SESSION_TYPE"] = "x11", ["DISPLAY"] = ":0", ["XDG_CURRENT_DESKTOP"] = "XFCE" }),
        new("Flatpak on Hyprland", new() { ["XDG_SESSION_TYPE"] = "wayland", ["HYPRLAND_INSTANCE_SIGNATURE"] = "abc", ["FLATPAK_ID"] = "com.xerahs.XerahS" }, FlatpakInfo: true)
    ];

    private static IEnumerable<TestCaseData> AllSessions() => Sessions.Select(s => new TestCaseData(s).SetName($"Profile_{s.Name.Replace(' ', '_')}"));

    private static LinuxDesktopFacts Facts(Session session) =>
        LinuxDesktopFacts.Detect(
            key => session.Env.GetValueOrDefault(key),
            dir => dir == "/usr/share/omarchy" && session.OmarchyInstalled,
            file => file == "/.flatpak-info" && session.FlatpakInfo);

    private static OmaSnapCapabilities UsableCaps() => OmaSnapCapabilities.Parse(
        "{\"schemaVersion\":1,\"ok\":true,\"version\":\"1.22.0\",\"hyprland\":true,\"extImageCopyCapture\":true,\"layerShell\":true,\"hostMode\":1,\"targets\":[\"smart\",\"region\",\"windows\",\"fullscreen\",\"scroll\"],\"editor\":[\"overlay\",\"window\"],\"pin\":true}");

    [TestCaseSource(nameof(AllSessions))]
    public async Task ProbeRunsOnlyOnNonSandboxedHyprland(Session session)
    {
        int probeCalls = 0;
        var facts = Facts(session);
        var profile = new LinuxDesktopProfile(facts, () => "/fake/omasnap", (_, _) =>
        {
            probeCalls++;
            return Task.FromResult(UsableCaps());
        });

        OmaSnapCapabilities caps = await profile.EnsureOmaSnapProbedAsync();
        await profile.EnsureOmaSnapProbedAsync();

        bool expectOmarchyLike = session.Name is "Omarchy" or "Omarchy (package)" or "Hyprland without Omarchy";
        Assert.Multiple(() =>
        {
            Assert.That(profile.IsOmarchyLike, Is.EqualTo(expectOmarchyLike), profile.Describe());
            Assert.That(caps.IsUsable, Is.EqualTo(expectOmarchyLike));
            Assert.That(probeCalls, Is.EqualTo(expectOmarchyLike ? 1 : 0), "probe is single-flight and never runs off Hyprland");
            Assert.That(profile.IsOmarchy, Is.EqualTo(session.Name.StartsWith("Omarchy", StringComparison.Ordinal)));
        });
    }

    [Test]
    public async Task MissingBinary_IsSilentFallback_WithoutProbe()
    {
        int probeCalls = 0;
        var profile = new LinuxDesktopProfile(Facts(Sessions[0]), () => null, (_, _) =>
        {
            probeCalls++;
            return Task.FromResult(UsableCaps());
        });

        OmaSnapCapabilities caps = await profile.EnsureOmaSnapProbedAsync();

        Assert.That(caps.IsUsable, Is.False);
        Assert.That(caps.Describe(), Does.Contain("not installed"));
        Assert.That(probeCalls, Is.Zero);
        Assert.That(profile.IsOmarchyLike, Is.False);
    }

    [Test]
    public async Task Engine_UsesFakeOmaSnapEndToEnd_WhenProbeSucceeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Linux-only.");
        }

        string runtime = Path.Combine(Path.GetTempPath(), $"xerahs-engine-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runtime);
        try
        {
            string fake = OmaSnapClientTests.FakeOmaSnapPath;
            var profile = new LinuxDesktopProfile(Facts(Sessions[0]), () => fake, (path, token) => new OmaSnapClient(path, () => runtime).ProbeAsync(token));
            var engine = new OmaSnapCaptureEngine(profile, path => new OmaSnapClient(path, () => runtime));

            Assert.That(engine.CurrentStatus.Available, Is.False, "not probed yet: never blocks, reports unavailable");
            HostedCaptureEngineStatus status = await engine.GetStatusAsync();
            HostedCaptureResult result = await engine.CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Fullscreen));

            Assert.That(status.Available, Is.True, status.Summary);
            Assert.That(status.Summary, Is.EqualTo("OmaSnap 1.22.0 · Hyprland · ready"));
            Assert.That(result.IsOk, Is.True, result.Error);
            engine.Release(result);
            Assert.That(File.Exists(result.ImagePath), Is.False);
        }
        finally
        {
            Directory.Delete(runtime, recursive: true);
        }
    }

    [Test]
    public async Task Engine_OnGnome_IsUnavailableAndStartsNothing()
    {
        var profile = new LinuxDesktopProfile(Facts(Sessions[3]), () => throw new AssertionException("must not locate"), (_, _) => throw new AssertionException("must not probe"));
        var engine = new OmaSnapCaptureEngine(profile);

        HostedCaptureResult result = await engine.CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Smart));

        Assert.That(result.Status, Is.EqualTo(HostedCaptureStatus.Unavailable));
        Assert.That(result.Error, Does.Contain("not a Hyprland session"));
    }
}

/// <summary>Golden test: the capture order is identical to the pre-XIP0088 policy for every non-OmaSnap selector.</summary>
[TestFixture]
public class WaterfallPolicyGoldenTests
{
    private static readonly LinuxInteractiveRegionSelectorPreference[] ExistingPreferences =
    [
        LinuxInteractiveRegionSelectorPreference.Automatic,
        LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
        LinuxInteractiveRegionSelectorPreference.DesktopNative,
        LinuxInteractiveRegionSelectorPreference.PortalDialog,
        LinuxInteractiveRegionSelectorPreference.Slurp
    ];

    private static readonly LinuxCaptureContext[] Contexts =
    [
        new(isWayland: true, desktop: "Hyprland", compositor: "Hyprland", isSandboxed: false, hasScreenshotPortal: true),
        new(isWayland: true, desktop: "GNOME", compositor: "Mutter", isSandboxed: false, hasScreenshotPortal: true),
        new(isWayland: true, desktop: "KDE", compositor: "KWin", isSandboxed: false, hasScreenshotPortal: true),
        new(isWayland: true, desktop: "sway", compositor: "sway", isSandboxed: false, hasScreenshotPortal: false),
        new(isWayland: false, desktop: "XFCE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: false),
        new(isWayland: false, desktop: "XFCE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true, prefersPortalForRegionCaptureOnX11: true),
        new(isWayland: true, desktop: "Hyprland", compositor: "Hyprland", isSandboxed: true, hasScreenshotPortal: true, isFlatpak: true)
    ];

    [Test]
    public void EveryExistingSelector_KeepsThePreXip0088Order()
    {
        var policy = new WaterfallCapturePolicy();
        foreach (var context in Contexts)
        {
            foreach (var kind in Enum.GetValues<LinuxCaptureKind>())
            {
                foreach (var preference in ExistingPreferences)
                {
                    var request = new LinuxCaptureRequest(kind, new CaptureOptions { LinuxRegionSelectorPreference = preference });
                    var actual = policy.GetStageOrder(request, context);
                    Assert.That(actual, Is.EqualTo(PreXip0088Order(request, context)),
                        $"{context.Desktop} wayland={context.IsWayland} sandboxed={context.IsSandboxed} {kind} {preference}");
                    Assert.That(actual, Does.Not.Contain(LinuxCaptureStage.OmaSnap));
                }
            }
        }
    }

    [Test]
    public void ExplicitOmaSnap_RegionOnWayland_TriesOmaSnapThenTheExistingChain()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, new CaptureOptions { LinuxRegionSelectorPreference = LinuxInteractiveRegionSelectorPreference.OmaSnap });

        Assert.That(policy.GetStageOrder(request, Contexts[0]), Is.EqualTo(new[]
        {
            LinuxCaptureStage.OmaSnap, LinuxCaptureStage.Portal, LinuxCaptureStage.DesktopDbus,
            LinuxCaptureStage.WaylandProtocol, LinuxCaptureStage.X11
        }));
        Assert.That(policy.GetStageOrder(request, Contexts[6]), Does.Not.Contain(LinuxCaptureStage.OmaSnap), "never in a sandbox");
        Assert.That(policy.GetStageOrder(new LinuxCaptureRequest(LinuxCaptureKind.FullScreen, request.Options), Contexts[0]),
            Does.Not.Contain(LinuxCaptureStage.OmaSnap), "full-screen crops rely on the whole desktop");
    }

    [Test]
    public void SelectorDiagnostics_AutomaticUnchanged_OmaSnapOfferedOnlyWhenProbed()
    {
        var support = new LinuxRegionCaptureSupportSnapshot(HasGnomeShellScreenshot: false, HasKdeScreenShot2: false, HasSlurp: true);
        var capability = LinuxRegionCaptureCapabilityDetector.Detect(Contexts[0], support);

        var without = LinuxRegionSelectorDiagnosticsDetector.Detect(Contexts[0], support, capability, omaSnapAvailable: false);
        var with = LinuxRegionSelectorDiagnosticsDetector.Detect(Contexts[0], support, capability, omaSnapAvailable: true);
        var gnome = LinuxRegionSelectorDiagnosticsDetector.Detect(Contexts[6], support, capability, omaSnapAvailable: true);

        Assert.Multiple(() =>
        {
            Assert.That(with.AutomaticPreference, Is.EqualTo(without.AutomaticPreference));
            Assert.That(without.AvailablePreferences, Does.Not.Contain(LinuxInteractiveRegionSelectorPreference.OmaSnap));
            Assert.That(with.AvailablePreferences, Does.Contain(LinuxInteractiveRegionSelectorPreference.OmaSnap));
            Assert.That(gnome.AvailablePreferences, Does.Not.Contain(LinuxInteractiveRegionSelectorPreference.OmaSnap));
        });
    }

    [Test]
    public void EnumValues_AreStable()
    {
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.Automatic, Is.EqualTo(0));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.XerahSOverlay, Is.EqualTo(1));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.DesktopNative, Is.EqualTo(2));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.PortalDialog, Is.EqualTo(3));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.Slurp, Is.EqualTo(4));
        Assert.That((int)LinuxInteractiveRegionSelectorPreference.OmaSnap, Is.EqualTo(5));
    }

    /// <summary>Frozen copy of WaterfallCapturePolicy.GetStageOrder as of v0.30.12 (before XIP0088).</summary>
    private static LinuxCaptureStage[] PreXip0088Order(LinuxCaptureRequest request, ILinuxCaptureContext context)
    {
        LinuxCaptureStage[] defaultOrder = [LinuxCaptureStage.Portal, LinuxCaptureStage.DesktopDbus, LinuxCaptureStage.WaylandProtocol, LinuxCaptureStage.X11];
        if (context.IsSandboxed)
        {
            return [LinuxCaptureStage.Portal];
        }

        if (!context.IsWayland && request.Kind == LinuxCaptureKind.Region)
        {
            switch (request.SelectorPreference)
            {
                case LinuxInteractiveRegionSelectorPreference.PortalDialog:
                    return [LinuxCaptureStage.Portal];
                case LinuxInteractiveRegionSelectorPreference.DesktopNative:
                    return [LinuxCaptureStage.DesktopDbus];
            }

            return context.PrefersPortalForRegionCaptureOnX11
                ? [LinuxCaptureStage.Portal, LinuxCaptureStage.DesktopDbus, LinuxCaptureStage.X11]
                : [LinuxCaptureStage.DesktopDbus, LinuxCaptureStage.X11, LinuxCaptureStage.Portal];
        }

        if (context.IsWayland && request.Kind == LinuxCaptureKind.Region)
        {
            return request.SelectorPreference switch
            {
                LinuxInteractiveRegionSelectorPreference.Slurp => [LinuxCaptureStage.WaylandProtocol],
                LinuxInteractiveRegionSelectorPreference.PortalDialog => [LinuxCaptureStage.Portal],
                _ => defaultOrder
            };
        }

        // Deliberate change after XIP0088: KDE Wayland whole-screen and window grabs go to KWin before the portal,
        // so the portal's permission dialog is only a fallback.
        if (context.IsWayland && string.Equals(context.Desktop, "KDE", StringComparison.OrdinalIgnoreCase) &&
            request.Kind is LinuxCaptureKind.FullScreen or LinuxCaptureKind.ActiveWindow)
        {
            return [LinuxCaptureStage.DesktopDbus, LinuxCaptureStage.Portal, LinuxCaptureStage.WaylandProtocol, LinuxCaptureStage.X11];
        }

        return defaultOrder;
    }
}
