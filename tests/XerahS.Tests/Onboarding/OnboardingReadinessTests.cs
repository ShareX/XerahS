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
using XerahS.App;
using XerahS.Core;
using XerahS.UI.Onboarding;

namespace XerahS.Tests.Onboarding;

[TestFixture]
public class OnboardingReadinessTests
{
    private static ReadinessInput Healthy(Func<ReadinessInput, ReadinessInput>? change = null)
    {
        var input = new ReadinessInput(
            SaveFolder: "/home/me/Pictures/XerahS",
            SaveFolderWritable: true,
            HotkeysConfigured: 5,
            HotkeysFailed: 0,
            RegionHotkey: "Ctrl + Shift + X",
            IsMacOS: false,
            MacScreenRecordingGranted: null,
            IsLinux: false,
            LinuxSessionType: null,
            FfmpegAvailable: true);
        return change?.Invoke(input) ?? input;
    }

    [Test]
    public void HealthyMachine_ReportsOnlyOkItemsWithTheCaptureHotkey()
    {
        var items = OnboardingReadiness.Evaluate(Healthy());

        Assert.That(items.Select(i => i.Level), Is.All.EqualTo(ReadinessLevel.Ok));
        Assert.That(items.Any(i => i.Detail.Contains("/home/me/Pictures/XerahS")), Is.True, "says where captures go");
        Assert.That(items.Any(i => i.Detail.Contains("Ctrl + Shift + X")), Is.True, "says which key to press");
    }

    [Test]
    public void UnwritableSaveFolder_NeedsAttention()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { SaveFolderWritable = false }));

        var item = items.Single(i => i.NeedsAttention);
        Assert.That(item.Detail, Does.Contain("/home/me/Pictures/XerahS"));
    }

    [Test]
    public void FailedHotkeys_NeedAttentionAndPointToTheTrayAsAFallback()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { HotkeysFailed = 2 }));

        var item = items.Single(i => i.NeedsAttention);
        Assert.That(item.Detail, Does.Contain("2 of 5").And.Contain("tray"));
    }

    [Test]
    public void NoHotkeys_IsOnlyInformation()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { HotkeysConfigured = 0, RegionHotkey = null }));

        Assert.That(items.Any(i => i.NeedsAttention), Is.False);
        Assert.That(items.Any(i => i.Level == ReadinessLevel.Info && i.Detail.Contains("tray")), Is.True);
    }

    [Test]
    public void MacOsWithoutScreenRecording_NeedsAttentionAndNamesTheSetting()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { IsMacOS = true, MacScreenRecordingGranted = false }));

        var item = items.Single(i => i.NeedsAttention);
        Assert.That(item.Detail, Does.Contain("Screen Recording").And.Contain("restart"));
    }

    [Test]
    public void MacOsWithScreenRecording_StillMentionsAccessibilityForHotkeys()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { IsMacOS = true, MacScreenRecordingGranted = true }));

        Assert.That(items.Any(i => i.NeedsAttention), Is.False);
        Assert.That(items.Any(i => i.Detail.Contains("Accessibility")), Is.True);
    }

    [Test]
    public void Wayland_GetsAnHonestNoteAboutPortalsAndRecording()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { IsLinux = true, LinuxSessionType = "wayland" }));

        var note = items.Single(i => i.Title.Contains("Wayland"));
        Assert.That(note.Level, Is.EqualTo(ReadinessLevel.Info));
        Assert.That(note.Detail, Does.Contain("portal").And.Contain("recording"));
    }

    [Test]
    public void X11_HasNoWaylandNote()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { IsLinux = true, LinuxSessionType = "x11" }));

        Assert.That(items.Any(i => i.Title.Contains("Wayland")), Is.False);
    }

    [Test]
    public void MissingFfmpeg_IsInformationNotAnError()
    {
        var items = OnboardingReadiness.Evaluate(Healthy(i => i with { FfmpegAvailable = false }));

        var item = items.Single(i => i.Title.Contains("FFmpeg"));
        Assert.That(item.Level, Is.EqualTo(ReadinessLevel.Info));
    }

    [Test]
    public void DefaultWorkflows_DoNotMentionGdiOffWindows()
    {
        var workflows = WorkflowsConfig.GetDefaultWorkflowList();
        var descriptions = workflows.Select(w => w.TaskSettings.Description ?? string.Empty).ToList();

        Assert.That(descriptions, Does.Not.Contain("Record screen for game"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.That(descriptions.Any(d => d.Contains("GDI")), Is.False);
        }
    }

    [Test]
    public void StartupFailureMessage_NamesTheReasonTheLogAndWhereToReport()
    {
        string message = StartupFailureReporter.BuildMessage(
            new InvalidOperationException("outer", new DllNotFoundException("libfoo.so not found")),
            "/home/me/.local/state/xerahs/Logs/XerahS.log");

        Assert.That(message, Does.Contain("could not start"));
        Assert.That(message, Does.Contain("libfoo.so not found"), "shows the root cause, not the wrapper");
        Assert.That(message, Does.Contain("/home/me/.local/state/xerahs/Logs/XerahS.log"));
        Assert.That(message, Does.Contain(StartupFailureReporter.IssuesUrl));
    }

    [Test]
    public void StartupFailureMessage_CopesWithAMissingLogPathAndLongReasons()
    {
        string message = StartupFailureReporter.BuildMessage(new Exception(new string('x', 5000)), null);

        Assert.That(message.Length, Is.LessThan(900));
        Assert.That(message, Does.Contain("log folder"));
    }

    [Test]
    public void AppleScriptEscaping_CannotBreakOutOfTheString()
    {
        string escaped = StartupFailureReporter.EscapeForAppleScript("a \"quoted\" \\ path\nnext");

        Assert.That(escaped, Is.EqualTo("a \\\"quoted\\\" \\\\ path\\nnext"));
    }
}
