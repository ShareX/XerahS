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

using System.Drawing;
using NUnit.Framework;
using XerahS.Platform.Windows.Recording;
using XerahS.Platform.Windows.Recording.Native;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.Platform.Windows;

[TestFixture]
public class WindowsNativeRecordingOptionsTests
{
    private static readonly Rectangle Primary = new(0, 0, 1920, 1080);
    private static readonly Rectangle Secondary = new(1920, -200, 2560, 1440);

    private static NativeRecordingOptions Map(RecordingOptions options, Rectangle? windowBounds = null) =>
        WindowsNativeRecordingOptionsMapper.Map(
            options,
            @"C:\out\test.mp4",
            _ => windowBounds,
            () => Primary,
            point => Secondary.Contains(point) ? Secondary : Primary.Contains(point) ? Primary : null,
            id => id == null ? null : "{endpoint}" + id);

    [Test]
    public void ComputeLayout_StitchesRegionAcrossMonitors()
    {
        var monitors = new List<(nint, Rectangle)> { (1, Primary), (2, Secondary) };
        var region = new Rectangle(1800, 100, 400, 300);

        var slices = GraphicsCapture.ComputeLayout(region, monitors);

        Assert.That(slices, Has.Count.EqualTo(2));
        Assert.That(slices[0].Monitor, Is.EqualTo((nint)1));
        Assert.That(slices[0].Source, Is.EqualTo(new Rectangle(1800, 100, 120, 300)));
        Assert.That(slices[0].Destination, Is.EqualTo(new Point(0, 0)));
        Assert.That(slices[1].Monitor, Is.EqualTo((nint)2));
        Assert.That(slices[1].Source, Is.EqualTo(new Rectangle(0, 300, 280, 300)));
        Assert.That(slices[1].Destination, Is.EqualTo(new Point(120, 0)));
    }

    [Test]
    public void ComputeLayout_LeavesOffDesktopAreaUncovered()
    {
        var monitors = new List<(nint, Rectangle)> { (1, Primary) };
        var slices = GraphicsCapture.ComputeLayout(new Rectangle(-100, -50, 300, 200), monitors);

        Assert.That(slices, Has.Count.EqualTo(1));
        Assert.That(slices[0].Source, Is.EqualTo(new Rectangle(0, 0, 200, 150)));
        Assert.That(slices[0].Destination, Is.EqualTo(new Point(100, 50)));
        Assert.That(GraphicsCapture.ComputeLayout(new Rectangle(5000, 5000, 10, 10), monitors), Is.Empty);
    }

    [Test]
    public void Map_ScreenMode_DefaultsToPrimaryAndHonoursRequestedDisplay()
    {
        Assert.That(Map(new RecordingOptions { Mode = CaptureMode.Screen }).Region, Is.EqualTo(Primary));
        Assert.That(Map(new RecordingOptions { Mode = CaptureMode.Screen, Region = new Rectangle(2000, 0, 100, 100) }).Region,
            Is.EqualTo(Secondary));
    }

    [Test]
    public void Map_RegionMode_KeepsDesktopCoordinatesAndSettings()
    {
        var native = Map(new RecordingOptions
        {
            Mode = CaptureMode.Region,
            Region = new Rectangle(1800, 100, 401, 301),
            Settings = new ScreenRecordingSettings
            {
                FPS = 60, BitrateKbps = 8000, ShowCursor = false,
                CaptureSystemAudio = true, CaptureMicrophone = true, MicrophoneDeviceId = "Mic"
            }
        });

        Assert.That(native.Region, Is.EqualTo(new Rectangle(1800, 100, 401, 301)));
        Assert.That(native.WindowHandle, Is.EqualTo((nint)0));
        Assert.That(native.TrackWindowHandle, Is.EqualTo((nint)0));
        Assert.That(native.FramesPerSecond, Is.EqualTo(60));
        Assert.That(native.VideoBitrate, Is.EqualTo(8_000_000));
        Assert.That(native.IncludeCursor, Is.False);
        Assert.That(native.CaptureSystemAudio, Is.True);
        Assert.That(native.CaptureMicrophone, Is.True);
        Assert.That(native.MicrophoneDeviceId, Is.EqualTo("{endpoint}Mic"));
        Assert.That(native.RequireHardwareEncoder, Is.False);
    }

    [Test]
    public void Map_WindowMode_DefaultsToPopupInclusiveTracking()
    {
        var bounds = new Rectangle(100, 200, 800, 600);
        var native = Map(new RecordingOptions { Mode = CaptureMode.Window, TargetWindowHandle = 0x1234 }, bounds);

        Assert.That(native.Region, Is.EqualTo(bounds));
        Assert.That(native.TrackWindowHandle, Is.EqualTo((nint)0x1234));
        Assert.That(native.WindowHandle, Is.EqualTo((nint)0));
    }

    [Test]
    public void Map_WindowMode_WindowOnlyUsesDirectCapture()
    {
        var native = Map(new RecordingOptions
        {
            Mode = CaptureMode.Window,
            TargetWindowHandle = 0x1234,
            Settings = new ScreenRecordingSettings { WindowRecordingMode = WindowRecordingMode.WindowOnly }
        });

        Assert.That(native.WindowHandle, Is.EqualTo((nint)0x1234));
        Assert.That(native.TrackWindowHandle, Is.EqualTo((nint)0));
    }

    [Test]
    public void Map_RejectsUnsupportedRequests()
    {
        Assert.Throws<PlatformNotSupportedException>(() => Map(new RecordingOptions
        {
            Mode = CaptureMode.Region,
            Region = new Rectangle(0, 0, 100, 100),
            Settings = new ScreenRecordingSettings { Codec = VideoCodec.HEVC }
        }));
        Assert.Throws<ArgumentException>(() => Map(new RecordingOptions { Mode = CaptureMode.Region }));
        Assert.Throws<ArgumentException>(() => Map(new RecordingOptions { Mode = CaptureMode.Window }));
        Assert.Throws<ArgumentException>(() => Map(new RecordingOptions { Mode = CaptureMode.Window, TargetWindowHandle = 1 }, null));
    }

    [Test]
    public void Map_ClampsOutOfRangeSettings()
    {
        var native = Map(new RecordingOptions
        {
            Mode = CaptureMode.Screen,
            Settings = new ScreenRecordingSettings { FPS = 1000, BitrateKbps = 10 }
        });

        Assert.That(native.FramesPerSecond, Is.EqualTo(240));
        Assert.That(native.VideoBitrate, Is.EqualTo(100_000));
    }

    [Test]
    public void MatchAudioDevice_PrefersIdThenNameThenPartialName()
    {
        var devices = new[]
        {
            new AudioCaptureDevice("{0.0.1.00000000}.{a}", "Microphone (Realtek Audio)"),
            new AudioCaptureDevice("{0.0.1.00000000}.{b}", "Headset Microphone (USB)")
        };

        Assert.That(WindowsNativeRecordingService.MatchAudioDevice(devices, "{0.0.1.00000000}.{b}")?.Id, Is.EqualTo("{0.0.1.00000000}.{b}"));
        Assert.That(WindowsNativeRecordingService.MatchAudioDevice(devices, "Microphone (Realtek Audio)")?.Id, Is.EqualTo("{0.0.1.00000000}.{a}"));
        Assert.That(WindowsNativeRecordingService.MatchAudioDevice(devices, "Headset")?.Id, Is.EqualTo("{0.0.1.00000000}.{b}"));
        Assert.That(WindowsNativeRecordingService.MatchAudioDevice(devices, "Nothing like it"), Is.Null);
    }

    [Test]
    public void Service_ReportsNativePauseResume()
    {
        using var service = new WindowsNativeRecordingService();
        Assert.That(service.GetCapabilities(new RecordingOptions()).PauseBehavior, Is.EqualTo(RecordingPauseBehavior.NativePauseResume));
    }
}
