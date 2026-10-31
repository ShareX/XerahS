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

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public sealed class NativeRecordingMappingTests
{
    [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
    public void HardwarePolicy_RequiresBothHardwareAndD3D11Support(bool hardware, bool d3d11)
    {
        var encoder = new VideoEncoderInfo("test encoder", hardware, d3d11);
        var options = new NativeRecordingOptions { OutputPath = "test.mp4", RequireHardwareEncoder = true };
        if (hardware && d3d11) Assert.DoesNotThrow(() => options.ValidateEncoder(encoder));
        else Assert.Throws<NotSupportedException>(() => options.ValidateEncoder(encoder));
        Assert.DoesNotThrow(() => (options with { RequireHardwareEncoder = false }).ValidateEncoder(encoder));
    }

    [Test]
    public void MapsExplicitDisplayAudioDevicesGainsAndEncoderOptions()
    {
        var secondary = new Rectangle(-1920, -200, 1920, 1080);
        var options = new RecordingOptions { Settings = new ScreenRecordingSettings
        {
            DisplayId = "secondary", CaptureSystemAudio = true, CaptureMicrophone = true,
            SystemAudioDeviceId = "speaker", MicrophoneDeviceId = "microphone",
            SystemAudioGain = 0.5f, MicrophoneGain = 2, RequireHardwareEncoder = true,
            LowLatency = false, HideCaptureBorder = false
        }};
        var native = WindowsNativeRecordingOptionsMapper.Map(options, "test.mp4", _ => null,
            () => new Rectangle(0, 0, 1920, 1080), _ => null, id => id, id => id, _ => secondary);
        Assert.Multiple(() =>
        {
            Assert.That(native.Region, Is.EqualTo(secondary));
            Assert.That(native.SystemAudioDeviceId, Is.EqualTo("speaker"));
            Assert.That(native.MicrophoneDeviceId, Is.EqualTo("microphone"));
            Assert.That(native.SystemAudioGain, Is.EqualTo(0.5f));
            Assert.That(native.MicrophoneGain, Is.EqualTo(2));
            Assert.That(native.RequireHardwareEncoder, Is.True);
            Assert.That(native.LowLatency, Is.False);
            Assert.That(native.HideCaptureBorder, Is.False);
        });
    }

    [Test]
    public void MissingExplicitDisplay_IsRejectedInsteadOfRecordingAnotherDisplay()
    {
        Assert.Throws<ArgumentException>(() => WindowsNativeRecordingOptionsMapper.Map(
            new RecordingOptions { Settings = new() { DisplayId = "missing" } }, "test.mp4", _ => null,
            () => new Rectangle(0, 0, 1920, 1080), _ => null, getDisplayBounds: _ => null));
    }

    [Test]
    public void Layout_HandlesThreeMonitorsNegativeOriginsAndDesktopGaps()
    {
        var slices = RecordingRegionLayout.Compute(new Rectangle(-200, -100, 500, 400),
            [(1, new Rectangle(-1920, 0, 1920, 1080)), (2, new Rectangle(0, -200, 200, 1080)),
             (3, new Rectangle(250, 0, 1080, 1920))]);
        Assert.That(slices, Has.Count.EqualTo(3));
        Assert.That(slices[0].Destination, Is.EqualTo(new Point(0, 100)));
        Assert.That(slices[1].Destination, Is.EqualTo(new Point(200, 0)));
        Assert.That(slices[2].Destination, Is.EqualTo(new Point(450, 100)));
        Assert.That(slices.Sum(slice => slice.Source.Width * slice.Source.Height), Is.LessThan(500 * 400));
    }

    [Test]
    public void Dimensions_RoundToEvenAndRejectTextureLimits()
    {
        Assert.That(RecordingRegionLayout.Normalize(new Rectangle(-1, -1, 401, 301)), Is.EqualTo(new Rectangle(-1, -1, 400, 300)));
        Assert.Throws<ArgumentException>(() => RecordingRegionLayout.Normalize(new Rectangle(0, 0, 16386, 100)));
        Assert.Throws<ArgumentException>(() => RecordingRegionLayout.Normalize(new Rectangle(0, 0, 1, 100)));
    }
}
