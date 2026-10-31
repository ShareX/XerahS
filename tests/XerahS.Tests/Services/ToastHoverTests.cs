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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.Tests.Services;

/// <summary>
/// Hover keeps the toast up. On Linux (X11/XWayland) a pointer leave is debounced and the
/// close that replaces the fade can be cancelled by hovering again; on Windows the original
/// behaviour (leave starts the fade at once, enter cancels it) is unchanged.
/// </summary>
[TestFixture]
public class ToastHoverTests
{
    private static ToastConfig AutoHideConfig(float duration = 10, float fade = 1) => new()
    {
        AutoHide = true,
        Duration = duration,
        FadeDuration = fade
    };

    private static ToastViewModel LinuxStyle(ToastConfig config, TimeSpan? grace = null)
    {
        var vm = new ToastViewModel(config)
        {
            UseLeaveGrace = true,
            UseDeferredClose = true
        };
        vm.LeaveGrace = grace ?? TimeSpan.FromMilliseconds(100);
        return vm;
    }

    private static ToastViewModel WindowsStyle(ToastConfig config) => new(config)
    {
        UseLeaveGrace = false,
        UseDeferredClose = false
    };

    private static async Task Pump(int milliseconds)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTest]
    public async Task Linux_FalseLeaveFollowedByEnter_KeepsHoverAndNeverHidesToolbar()
    {
        using var vm = LinuxStyle(AutoHideConfig());
        var hoverChanges = new List<bool>();
        vm.HoverChanged += (_, h) => hoverChanges.Add(h);

        vm.OnMouseEnter();
        vm.OnMouseLeave();
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsLeavePending, Is.True);
            Assert.That(vm.IsMouseInside, Is.True);
            Assert.That(vm.IsHovered, Is.True);
        });

        vm.OnMouseEnter();
        await Pump(250);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsLeavePending, Is.False);
            Assert.That(vm.IsMouseInside, Is.True);
            Assert.That(hoverChanges, Is.EqualTo(new[] { true }));
        });
    }

    [AvaloniaTest]
    public async Task Linux_PointerMotionCancelsPendingLeave()
    {
        using var vm = LinuxStyle(AutoHideConfig());
        vm.OnMouseEnter();
        vm.OnMouseLeave();

        vm.OnPointerMovedInside();
        await Pump(250);

        Assert.That(vm.IsMouseInside, Is.True);
        Assert.That(vm.IsHovered, Is.True);
    }

    [AvaloniaTest]
    public async Task Linux_RealLeave_IsCommittedAfterGrace()
    {
        using var vm = LinuxStyle(AutoHideConfig());
        var hoverChanges = new List<bool>();
        vm.HoverChanged += (_, h) => hoverChanges.Add(h);

        vm.OnMouseEnter();
        vm.OnMouseLeave();
        await Pump(300);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsLeavePending, Is.False);
            Assert.That(vm.IsMouseInside, Is.False);
            Assert.That(vm.IsHovered, Is.False);
            Assert.That(hoverChanges, Is.EqualTo(new[] { true, false }));
        });
    }

    [AvaloniaTest]
    public async Task Linux_HoverAfterDurationElapsed_KeepsToastOpen_ThenClosesAfterLeave()
    {
        // Duration 0 means the duration has already elapsed when the toast is created.
        using var vm = LinuxStyle(AutoHideConfig(duration: 0, fade: 0.3f));
        int closeRequests = 0;
        vm.CloseRequested += (_, _) => closeRequests++;
        Assert.That(vm.IsDeferredCloseRunning, Is.True, "auto-hide should be counting down");

        vm.OnMouseEnter();
        Assert.That(vm.IsDeferredCloseRunning, Is.False, "hover cancels the pending close");

        // A false leave while hovering must not close the toast.
        vm.OnMouseLeave();
        vm.OnPointerMovedInside();
        await Pump(800);
        Assert.That(closeRequests, Is.EqualTo(0), "toast closed while hovered");

        // Real leave: grace (100 ms) then the fade-length countdown (300 ms) restarts.
        vm.OnMouseLeave();
        await Pump(200);
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsDeferredCloseRunning, Is.True);
            Assert.That(closeRequests, Is.EqualTo(0));
        });

        // Coming back during the countdown cancels it, like hovering during the Windows fade.
        vm.OnMouseEnter();
        await Pump(500);
        Assert.That(closeRequests, Is.EqualTo(0));

        vm.OnMouseLeave();
        await Pump(700);
        Assert.That(closeRequests, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public void Windows_LeaveStartsFadeImmediately_AndEnterStopsIt()
    {
        using var vm = WindowsStyle(AutoHideConfig(duration: 10));
        var hoverChanges = new List<bool>();
        vm.HoverChanged += (_, h) => hoverChanges.Add(h);

        vm.OnMouseEnter();
        vm.OnMouseLeave();
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsLeavePending, Is.False);
            Assert.That(vm.IsMouseInside, Is.False);
            Assert.That(hoverChanges, Is.EqualTo(new[] { true, false }));
            // Duration has not elapsed yet, so no fade.
            Assert.That(vm.IsFadeTimerRunning, Is.False);
        });
    }

    [AvaloniaTest]
    public void Windows_AfterDuration_LeaveFadesAndEnterCancels()
    {
        // Re-create the "duration elapsed" state via a zero-duration toast.
        using var elapsed = new ToastViewModel(AutoHideConfig(duration: 0)) { UseLeaveGrace = false, UseDeferredClose = false };
        // The constructor ran StartFade with the platform default; reset by hovering.
        elapsed.OnMouseEnter();
        Assert.That(elapsed.IsFadeTimerRunning, Is.False);

        elapsed.OnMouseLeave();
        Assert.Multiple(() =>
        {
            Assert.That(elapsed.IsFadeTimerRunning, Is.True);
            Assert.That(elapsed.IsDeferredCloseRunning, Is.False);
        });

        elapsed.OnMouseEnter();
        Assert.That(elapsed.IsFadeTimerRunning, Is.False);
    }

    [AvaloniaTest]
    public async Task ToastWindow_HoverShowsToolbar_HidesChip_AndLeaveReverses()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 10,
            FadeDuration = 1,
            Size = new SizeI(400, 300),
            FilePath = "/tmp/xerahs-toast-hover-test.png",
            URL = "https://example.com/a.png",
            ActionButtons = [ToastClickAction.OpenFile, ToastClickAction.CopyFilePath],
            OutputKind = ToastOutputKind.Uploaded
        };

        var window = new ToastWindow();
        window.Initialize(config);
        window.Show();
        await Pump(50);

        var vm = (ToastViewModel)window.DataContext!;
        vm.LeaveGrace = TimeSpan.FromMilliseconds(100);
        var actions = window.FindControl<Border>("ActionsPanel")!;
        var chip = window.FindControl<Border>("OutputChip")!;
        Assert.Multiple(() =>
        {
            Assert.That(vm.HasActionButtons, Is.True);
            Assert.That(vm.HasOutputChip, Is.True);
            Assert.That(actions.Opacity, Is.EqualTo(0));
            Assert.That(chip.Opacity, Is.EqualTo(1));
        });

        // Real pointer input through the window: the toolbar appears and the chip fades.
        window.MouseMove(new Point(200, 150));
        await Pump(400); // the chip has a 120 ms opacity transition
        Assert.Multiple(() =>
        {
            Assert.That(window.IsPointerOver, Is.True, "headless pointer did not enter the toast");
            Assert.That(vm.IsHovered, Is.True);
            Assert.That(actions.Opacity, Is.EqualTo(1));
            Assert.That(actions.IsHitTestVisible, Is.True);
            Assert.That(chip.Opacity, Is.EqualTo(0));
        });

        // Leave through the view model (the headless platform has no "pointer left window").
        vm.OnMouseLeave();
        await Pump(vm.UseLeaveGrace ? 600 : 400);
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsHovered, Is.False);
            Assert.That(actions.Opacity, Is.EqualTo(0));
            Assert.That(actions.IsHitTestVisible, Is.False);
            Assert.That(chip.Opacity, Is.EqualTo(1));
        });

        window.Close();
    }
}
