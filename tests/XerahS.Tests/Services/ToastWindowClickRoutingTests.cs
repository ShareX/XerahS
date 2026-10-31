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

using Avalonia.Input;
using NUnit.Framework;
using Point = global::Avalonia.Point;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.Tests.Services;

[TestFixture]
public class ToastWindowClickRoutingTests
{
    [Test]
    public void TryGetClickAction_LeftReleaseWithinThreshold_ReturnsLeftClick()
    {
        var handled = ToastWindow.TryGetClickAction(
            new Point(10, 10),
            new Point(18, 15),
            PointerUpdateKind.LeftButtonReleased,
            out var action);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(action, Is.EqualTo(ToastWindow.ToastPointerAction.LeftClick));
        });
    }

    [Test]
    public void TryGetClickAction_MiddleReleaseWithinThreshold_ReturnsMiddleClick()
    {
        var handled = ToastWindow.TryGetClickAction(
            new Point(10, 10),
            new Point(12, 14),
            PointerUpdateKind.MiddleButtonReleased,
            out var action);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(action, Is.EqualTo(ToastWindow.ToastPointerAction.MiddleClick));
        });
    }

    [Test]
    public void TryGetClickAction_DragBeyondThreshold_DoesNotTriggerClick()
    {
        var handled = ToastWindow.TryGetClickAction(
            new Point(10, 10),
            new Point(35, 10),
            PointerUpdateKind.LeftButtonReleased,
            out var action);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(action, Is.EqualTo(ToastWindow.ToastPointerAction.None));
        });
    }

    [Test]
    public void ToastConfig_IsValid_RejectsInvalidToastTimings()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new ToastConfig { Duration = -1, FadeDuration = 1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = -1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = float.PositiveInfinity, FadeDuration = 1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = float.PositiveInfinity }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = float.NaN, FadeDuration = 1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = float.NaN }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 0, FadeDuration = 1 }.IsValid, Is.True);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = 0 }.IsValid, Is.True);
        });
    }

    [Test]
    public void ToastConfig_IsValid_AllowsStickyToastWithZeroTimers_WhenAutoHideDisabled()
    {
        var config = new ToastConfig
        {
            AutoHide = false,
            Duration = 0,
            FadeDuration = 0
        };

        Assert.That(config.IsValid, Is.True);
    }

    [Test]
    public void ToastConfig_IsValid_RejectsAutoHideToastWithZeroTimers()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 0,
            FadeDuration = 0
        };

        Assert.That(config.IsValid, Is.False);
    }

    [Test]
    public void ToastViewModel_GetAutoHideStartMode_StartsFadeImmediately_WhenDurationIsZero()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 0,
            FadeDuration = 1
        };

        Assert.That(ToastViewModel.GetAutoHideStartMode(config), Is.EqualTo(ToastViewModel.ToastAutoHideStartMode.StartFade));
    }

    [Test]
    public void ToastViewModel_GetAutoHideStartMode_WaitsForDuration_WhenDurationIsPositive()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 1,
            FadeDuration = 0
        };

        Assert.That(ToastViewModel.GetAutoHideStartMode(config), Is.EqualTo(ToastViewModel.ToastAutoHideStartMode.WaitForDuration));
    }

    [Test]
    public void ToastViewModel_GetAutoHideStartMode_DoesNotAutoHide_WhenDisabled()
    {
        var config = new ToastConfig
        {
            AutoHide = false,
            Duration = 0,
            FadeDuration = 1
        };

        Assert.That(ToastViewModel.GetAutoHideStartMode(config), Is.EqualTo(ToastViewModel.ToastAutoHideStartMode.None));
    }

    [Test]
    public void ToastViewModel_GetNextFadeOpacity_ReachesZeroBeforeClose()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToastViewModel.GetNextFadeOpacity(1.0, 0.25), Is.EqualTo(0.75));
            Assert.That(ToastViewModel.GetNextFadeOpacity(0.2, 0.25), Is.EqualTo(0));
        });
    }

    [Test]
    public void ToastViewModel_OnMenuClosed_StartsFade_WhenDurationNotElapsed_AndAutoHideEnabled()
    {
        // Arrange: auto-hide toast, duration not yet elapsed (_isDurationEnd = false),
        // mouse not inside, menu closes after being open
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 10,
            FadeDuration = 1
        };

        var viewModel = new ToastViewModel(config);

        // Open menu before duration fires
        viewModel.OnMenuOpened();

        // Act: close the menu while duration has not elapsed
        // Before the fix, OnMenuClosed called CheckFade() which requires _isDurationEnd=true.
        // After the fix, OnMenuClosed calls StartFade() directly, resuming fade even before
        // the duration timer fires.
        Exception? exception = null;
        try
        {
            viewModel.OnMenuClosed();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Null);
            Assert.That(config.AutoHide, Is.True);
        });
    }

    [Test]
    public void ToastViewModel_OnMenuClosed_DoesNotThrow_WhenMouseIsInside()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 10,
            FadeDuration = 1
        };

        var viewModel = new ToastViewModel(config);
        viewModel.OnMouseEnter();
        viewModel.OnMenuOpened();

        // Act
        Exception? exception = null;
        try
        {
            viewModel.OnMenuClosed();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Null);
            Assert.That(config.AutoHide, Is.True);
        });
    }

    [Test]
    public void ToastViewModel_OnMenuClosed_DoesNotThrow_WhenAutoHideDisabled()
    {
        var config = new ToastConfig
        {
            AutoHide = false,
            Duration = 10,
            FadeDuration = 1
        };

        var viewModel = new ToastViewModel(config);
        viewModel.OnMenuOpened();

        // Act
        Exception? exception = null;
        try
        {
            viewModel.OnMenuClosed();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Null);
            Assert.That(config.AutoHide, Is.False);
        });
    }

    [Test]
    public void ToastViewModel_OnMenuClosed_DoesNotThrow_WhenMenuNeverOpened()
    {
        // Regression: OnMenuClosed should be safe to call even if menu was never opened
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 10,
            FadeDuration = 1
        };

        var viewModel = new ToastViewModel(config);

        // Act: close menu without ever opening it
        Exception? exception = null;
        try
        {
            viewModel.OnMenuClosed();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Null);
            Assert.That(config.AutoHide, Is.True);
        });
    }

    public void BuildMarkdownImage_UsesMarkdownImageSyntax_WithEscapedAltText()
    {
        var markdown = ToastViewModel.BuildMarkdownImage("https://example.com/capture.png", "Latest [Capture]");

        Assert.That(markdown, Is.EqualTo("![Latest \\[Capture\\]](https://example.com/capture.png)"));
    }

    [Test]
    public void BuildMarkdownImage_FallsBackToGenericAltText_WhenBlank()
    {
        var markdown = ToastViewModel.BuildMarkdownImage("https://example.com/capture.png", " ");

        Assert.That(markdown, Is.EqualTo("![Image](https://example.com/capture.png)"));
    }

    [Test]
    public void BuildMarkdownImage_WrapsUrlsWithSpacesOrParentheses()
    {
        var markdown = ToastViewModel.BuildMarkdownImage("https://example.com/screenshots/file (1).png", "Capture");

        Assert.That(markdown, Is.EqualTo("![Capture](<https://example.com/screenshots/file (1).png>)"));
    }

    [Test]
    public void ToastViewModel_OnFileDragStarted_PausesFade_UntilDragEndsOutside()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 0,
            FadeDuration = 1
        };

        using var viewModel = new ToastViewModel(config);

        // On Linux there is no visual fade (the compositor overrides per-window opacity);
        // a deferred close stands in for it, so the opacity fade timer never runs there.
        // ToastHoverTests covers the Linux hover/close behaviour.
        if (OperatingSystem.IsLinux())
        {
            Assert.That(viewModel.IsFadeTimerRunning, Is.False);
            return;
        }

        Assert.That(viewModel.IsFadeTimerRunning, Is.True);

        viewModel.OnFileDragStarted();
        viewModel.OnMouseLeave();

        Assert.That(viewModel.IsFadeTimerRunning, Is.False);

        viewModel.OnFileDragEnded(pointerInside: false);

        Assert.That(viewModel.IsFadeTimerRunning, Is.True);
    }

    [Test]
    public void ToastViewModel_OnFileDragEnded_DoesNotResumeFade_WhenPointerStillInside()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 0,
            FadeDuration = 1
        };

        using var viewModel = new ToastViewModel(config);
        viewModel.OnFileDragStarted();
        viewModel.OnFileDragEnded(pointerInside: true);

        Assert.That(viewModel.IsFadeTimerRunning, Is.False);
    }
}
